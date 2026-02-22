using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// V2 revision ingestor: streaming, token-only paging with no segmentation or cursor reseek.
/// Modeled after the validator Program.cs behavior.
/// </summary>
public sealed class RevisionIngestionServiceV2
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RevisionIngestionServiceV2> _logger;
    private readonly IOptionsMonitor<RevisionIngestionV2Options> _options;
    private readonly IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions> _persistenceOptions;
    private readonly IBackfillStartProvider _backfillStartProvider;
    private readonly TimeProvider _timeProvider;

    private const int DefaultFallbackDays = 180;

    public RevisionIngestionServiceV2(
        IServiceScopeFactory scopeFactory,
        ILogger<RevisionIngestionServiceV2> logger,
        IOptionsMonitor<RevisionIngestionV2Options> options,
        IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions> persistenceOptions,
        IBackfillStartProvider backfillStartProvider,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
        _persistenceOptions = persistenceOptions;
        _backfillStartProvider = backfillStartProvider;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Ingests work item revisions for a ProductOwner using V2 streaming token-only paging.
    /// </summary>
    public async Task<RevisionIngestionResult> IngestRevisionsAsync(
        int productOwnerId,
        Action<RevisionIngestionProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var config = _options.CurrentValue;
        var overallStart = Stopwatch.GetTimestamp();
        int totalPersisted = 0;
        int totalPages = 0;
        int totalRawRevisions = 0;
        int totalPagesWithoutScopedRevisions = 0;
        int totalEmptyRawPagesWithToken = 0;
        var runSegmentsProcessed = new HashSet<int>();
        var runSegmentsWithZeroScoped = new HashSet<int>();
        string? runTerminationReason = null;
        string? runTerminationMessage = null;

        try
        {
            // Phase 1: Resolve aggregated scope
            HashSet<int> allowedWorkItemIds;
            int productCount;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
                var tfsClient = scope.ServiceProvider.GetRequiredService<ITfsClient>();
                (allowedWorkItemIds, productCount) = await ResolveAllowedWorkItemIdsAsync(
                    context, tfsClient, productOwnerId, cancellationToken);
            }

            _logger.LogInformation(
                "REV_INGEST_V2_SCOPE products={ProductCount} workItems={WorkItemCount}",
                productCount, allowedWorkItemIds.Count);

            // Phase 2: Derive backfill start and determine windows
            var now = _timeProvider.GetUtcNow();
            var backfillStart = await DeriveBackfillStartAsync(
                allowedWorkItemIds, config, now, cancellationToken);
            var windows = BuildWindows(config, backfillStart, now);

            // Phase 2b: Load checkpoint and skip completed windows
            string? resumeToken = null;
            DateTimeOffset? checkpointWindowStart = null;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
                var watermark = await context.RevisionIngestionWatermarks
                    .FirstOrDefaultAsync(w => w.ProductOwnerId == productOwnerId, cancellationToken);

                if (watermark?.ContinuationToken != null
                    && watermark.LastSyncStartDateTime != null
                    && string.Equals(watermark.LastRunOutcome, "V2_InProgress", StringComparison.Ordinal))
                {
                    resumeToken = watermark.ContinuationToken;
                    checkpointWindowStart = watermark.LastSyncStartDateTime;

                    _logger.LogInformation(
                        "REV_INGEST_V2_CHECKPOINT_RESUME productOwnerId={ProductOwnerId} " +
                        "windowStart={WindowStart} tokenHash={TokenHash}",
                        productOwnerId, checkpointWindowStart,
                        HashToken(resumeToken));
                }
            }

            // Skip windows that completed before the checkpoint
            if (checkpointWindowStart != null)
            {
                var skipCount = windows.FindIndex(w => w.Start >= checkpointWindowStart.Value);
                if (skipCount > 0)
                {
                    windows = windows.GetRange(skipCount, windows.Count - skipCount);
                }
            }

            // Phase 3: Process each window
            foreach (var window in windows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Use resume token only for the window that matches the checkpoint
                string? initialToken = null;
                if (resumeToken != null && checkpointWindowStart != null
                    && window.Start == checkpointWindowStart.Value)
                {
                    initialToken = resumeToken;
                    resumeToken = null; // consume: only use once
                }

                if (initialToken != null)
                {
                    _logger.LogInformation(
                        "REV_INGEST_V2_WINDOW_START start={WindowStart} end={WindowEnd} resumeTokenHash={ResumeTokenHash}",
                        window.Start, window.End, HashToken(initialToken));
                }
                else
                {
                    _logger.LogInformation(
                        "REV_INGEST_V2_WINDOW_START start={WindowStart} end={WindowEnd}",
                        window.Start, window.End);
                }

                var windowResult = await ProcessWindowAsync(
                    productOwnerId, allowedWorkItemIds, window, config,
                    initialToken, cancellationToken);

                totalPersisted += windowResult.Persisted;
                totalPages += windowResult.Pages;
                totalRawRevisions += windowResult.TotalRawRevisions;
                totalPagesWithoutScopedRevisions += windowResult.PagesWithoutScopedRevisions;
                totalEmptyRawPagesWithToken += windowResult.EmptyRawPagesWithToken;
                runSegmentsProcessed.UnionWith(windowResult.SegmentsProcessed);
                runSegmentsWithZeroScoped.UnionWith(windowResult.SegmentsWithZeroScoped);

                if (!windowResult.Success)
                {
                    _logger.LogWarning(
                        "REV_INGEST_V2_WINDOW_FAIL reason={Reason} tokenHash={TokenHash} " +
                        "consecutiveEmptyPages={ConsecutiveEmpty} " +
                        "windowStartUtc={WindowStartUtc} windowEndUtc={WindowEndUtc}",
                        windowResult.StallReason, windowResult.LastTokenHash,
                        windowResult.ConsecutiveEmptyPages,
                        window.Start, window.End);

                    return new RevisionIngestionResult
                    {
                        Success = false,
                        RunOutcome = RevisionIngestionRunOutcome.Failed,
                        RevisionsIngested = totalPersisted,
                        PagesProcessed = totalPages,
                        ErrorMessage = $"Window failed: {windowResult.StallReason}",
                        Message = $"V2 ingestion failed at window [{window.Start} - {window.End}): {windowResult.StallReason}"
                    };
                }

                _logger.LogInformation(
                    "REV_INGEST_V2_WINDOW_SUMMARY windowStart={WindowStart} windowEnd={WindowEnd} pagesFetched={PagesFetched} totalRawRevisions={TotalRawRevisions} totalPersistedRevisions={TotalPersistedRevisions} pagesWithoutScopedRevisions={PagesWithoutScopedRevisions} emptyRawPagesWithToken={EmptyRawPagesWithToken} segmentsProcessed={SegmentsProcessed} segmentsWithZeroScoped={SegmentsWithZeroScoped} terminationReason={TerminationReason} terminationMessage={TerminationMessage} durationMs={DurationMs}",
                    window.Start,
                    window.End,
                    windowResult.Pages,
                    windowResult.TotalRawRevisions,
                    windowResult.Persisted,
                    windowResult.PagesWithoutScopedRevisions,
                    windowResult.EmptyRawPagesWithToken,
                    windowResult.SegmentsProcessed.Count,
                    windowResult.SegmentsWithZeroScoped.Count,
                    windowResult.TerminationReason,
                    windowResult.TerminationMessage,
                    windowResult.DurationMs);

                // Mark window as completed in checkpoint so resume skips it
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
                    await ClearCheckpointAsync(
                        context,
                        productOwnerId,
                        windowResult.WasTerminatedEarly ? "V2_CompletedWithAnomaly" : "V2_Completed",
                        cancellationToken);
                }

                if (windowResult.WasTerminatedEarly)
                {
                    runTerminationReason = windowResult.TerminationReason;
                    runTerminationMessage = windowResult.TerminationMessage;
                    break;
                }
            }

            var runOutcome = runTerminationReason is null
                ? RevisionIngestionRunOutcome.CompletedNormally
                : RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly;
            _logger.LogInformation(
                "REV_INGEST_V2_RUN_SUMMARY pagesFetched={PagesFetched} totalRawRevisions={TotalRawRevisions} totalPersistedRevisions={TotalPersistedRevisions} pagesWithoutScopedRevisions={PagesWithoutScopedRevisions} emptyRawPagesWithToken={EmptyRawPagesWithToken} segmentsProcessed={SegmentsProcessed} segmentsWithZeroScoped={SegmentsWithZeroScoped} terminationReason={TerminationReason} terminationMessage={TerminationMessage} durationMs={DurationMs}",
                totalPages,
                totalRawRevisions,
                totalPersisted,
                totalPagesWithoutScopedRevisions,
                totalEmptyRawPagesWithToken,
                runSegmentsProcessed.Count,
                runSegmentsWithZeroScoped.Count,
                runTerminationReason,
                runTerminationMessage,
                GetElapsedMs(overallStart));

            return new RevisionIngestionResult
            {
                Success = true,
                HasWarnings = runOutcome == RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly,
                WasTerminatedEarly = runTerminationReason is not null,
                RunOutcome = runOutcome,
                RevisionsIngested = totalPersisted,
                PagesProcessed = totalPages,
                TerminationReason = Enum.TryParse<ReportingRevisionsTerminationReason>(runTerminationReason, out var parsedReason)
                    ? parsedReason
                    : null,
                TerminationMessage = runTerminationMessage,
                Message = runOutcome == RevisionIngestionRunOutcome.CompletedNormally
                    ? $"V2 ingestion completed. Persisted={totalPersisted} Pages={totalPages}"
                    : $"V2 ingestion completed with pagination anomaly. Persisted={totalPersisted} Pages={totalPages} Reason={runTerminationReason}"
            };
        }
        catch (OperationCanceledException)
        {
            return new RevisionIngestionResult
            {
                Success = false,
                WasCancelled = true,
                RunOutcome = RevisionIngestionRunOutcome.Failed,
                RevisionsIngested = totalPersisted,
                PagesProcessed = totalPages,
                Message = "V2 ingestion was cancelled."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REV_INGEST_V2 unhandled exception for ProductOwner {ProductOwnerId}", productOwnerId);
            return new RevisionIngestionResult
            {
                Success = false,
                RunOutcome = RevisionIngestionRunOutcome.Failed,
                RevisionsIngested = totalPersisted,
                PagesProcessed = totalPages,
                ErrorMessage = ex.Message,
                Message = $"V2 ingestion failed with exception: {ex.Message}"
            };
        }
    }

    private async Task<WindowResult> ProcessWindowAsync(
        int productOwnerId,
        HashSet<int> allowedWorkItemIds,
        IngestionWindow window,
        RevisionIngestionV2Options config,
        string? initialContinuationToken,
        CancellationToken cancellationToken)
    {
        var windowStart = Stopwatch.GetTimestamp();
        string? continuationToken = initialContinuationToken;
        int pageIndex = 0;
        int totalPersisted = 0;
        int totalRawRevisions = 0;
        int pagesWithoutScopedRevisions = 0;
        int emptyRawPagesWithToken = 0;
        int consecutiveEmptyPages = 0;
        var seenTokenHashes = new HashSet<string>();
        var segmentsProcessed = new HashSet<int>();
        var segmentsWithZeroScoped = new HashSet<int>();
        int emptyWithTokenLogCount = 0;
        string? terminationReason = null;
        string? terminationMessage = null;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Defensive: detect token cycles (any repeated token, not just immediate repeat)
            var continuationTokenHash = HashToken(continuationToken);
            if (continuationTokenHash != null)
            {
                if (!seenTokenHashes.Add(continuationTokenHash))
                {
                    _logger.LogWarning(
                        "REV_INGEST_V2_WINDOW_FAIL reason=RepeatedTokenCycle tokenHash={TokenHash} " +
                        "pageIndex={PageIndex} seenCount={SeenCount} " +
                        "windowStartUtc={WindowStartUtc} windowEndUtc={WindowEndUtc} " +
                        "allowedWorkItemIds={AllowedCount} pageSize={PageSize} " +
                        "hasContinuationToken={HasToken}",
                        continuationTokenHash, pageIndex, seenTokenHashes.Count,
                        window.Start, window.End,
                        allowedWorkItemIds.Count, config.V2PageSize,
                        continuationToken != null);

                    return new WindowResult(
                        Success: false,
                        Persisted: totalPersisted,
                        Pages: pageIndex,
                        StallReason: "RepeatedTokenCycle",
                        LastTokenHash: continuationTokenHash,
                        ConsecutiveEmptyPages: consecutiveEmptyPages,
                        TotalRawRevisions: totalRawRevisions,
                        PagesWithoutScopedRevisions: pagesWithoutScopedRevisions,
                        EmptyRawPagesWithToken: emptyRawPagesWithToken,
                        SegmentsProcessed: segmentsProcessed,
                        SegmentsWithZeroScoped: segmentsWithZeroScoped,
                        WasTerminatedEarly: false,
                        TerminationReason: null,
                        TerminationMessage: null,
                        DurationMs: GetElapsedMs(windowStart));
                }
            }

            ReportingRevisionsResult page;
            using (var scope = _scopeFactory.CreateScope())
            {
                var revisionSource = scope.ServiceProvider.GetRequiredService<IWorkItemRevisionSource>();
                page = await revisionSource.GetRevisionsForScopeAsync(
                    allowedWorkItemIds.ToArray(),
                    window.Start,
                    continuationToken,
                    ReportingExpandMode.None,
                    window.End,
                    cancellationToken);
            }

            var rawCount = page.Revisions.Count;
            var nextToken = page.ContinuationToken;
            var nextTokenHash = HashToken(nextToken);
            totalRawRevisions += rawCount;
            var activeSegmentIndex =
                ResolveSegmentIndexFromContinuationToken(continuationToken) ??
                ResolveSegmentIndexFromContinuationToken(nextToken);
            if (activeSegmentIndex.HasValue)
            {
                segmentsProcessed.Add(activeSegmentIndex.Value);
            }

            // Empty page with non-null token: advance the server token
            if (rawCount == 0 && nextToken != null)
            {
                consecutiveEmptyPages++;
                pagesWithoutScopedRevisions++;
                emptyRawPagesWithToken++;
                if (activeSegmentIndex.HasValue)
                {
                    segmentsWithZeroScoped.Add(activeSegmentIndex.Value);
                }

                // Rate-limited logging: first 3, then every 50
                emptyWithTokenLogCount++;
                if (emptyWithTokenLogCount <= 3 || emptyWithTokenLogCount % 50 == 0)
                {
                    _logger.LogWarning(
                        "REV_INGEST_V2_EMPTY_WITH_TOKEN page={PageIndex} consecutiveEmpty={ConsecutiveEmpty} " +
                        "tokenHash={TokenHash} nextTokenHash={NextTokenHash} " +
                        "windowStartUtc={WindowStartUtc} windowEndUtc={WindowEndUtc} " +
                        "allowedWorkItemIds={AllowedCount} pageSize={PageSize} " +
                        "hasContinuationToken={HasToken} rawCount={RawCount} scopedCount=0 inWindowCount=0",
                        pageIndex, consecutiveEmptyPages,
                        continuationTokenHash, nextTokenHash,
                        window.Start, window.End,
                        allowedWorkItemIds.Count, config.V2PageSize,
                        true, rawCount);
                }

                if (nextToken != null &&
                    !string.Equals(nextToken, continuationToken, StringComparison.Ordinal))
                {
                    await SaveCheckpointOnTokenAdvanceAsync(
                        productOwnerId,
                        window,
                        nextToken,
                        pageIndex,
                        cancellationToken);
                }

                continuationToken = nextToken;
                pageIndex++;
                continue;
            }

            consecutiveEmptyPages = 0;

            // Filter and persist
            var scoped = page.Revisions
                .Where(r => allowedWorkItemIds.Contains(r.WorkItemId))
                .ToList();

            var inWindow = scoped
                .Where(r => r.ChangedDate >= window.Start && r.ChangedDate < window.End)
                .ToList();
            if (scoped.Count == 0)
            {
                pagesWithoutScopedRevisions++;
                if (activeSegmentIndex.HasValue)
                {
                    segmentsWithZeroScoped.Add(activeSegmentIndex.Value);
                }
            }

            int persisted = 0;
            var rejectsDuplicate = 0;
            var rejectsMissing = 0;
            var rejectsOther = 0;

            if (inWindow.Count > 0)
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
                var persistResult = await PersistRevisionsAsync(context, inWindow, cancellationToken);
                persisted = persistResult.Persisted;
                rejectsDuplicate = persistResult.Duplicates;
                rejectsMissing = persistResult.MissingRequired;
                rejectsOther = persistResult.Other;

            }

            if (nextToken != null &&
                !string.Equals(nextToken, continuationToken, StringComparison.Ordinal))
            {
                await SaveCheckpointOnTokenAdvanceAsync(
                    productOwnerId,
                    window,
                    nextToken,
                    pageIndex,
                    cancellationToken);
            }

            totalPersisted += persisted;

            _logger.LogInformation(
                "REV_INGEST_V2_PAGE page={PageIndex} raw={Raw} scoped={Scoped} inWindow={InWindow} " +
                "persistAttempt={PersistAttempt} persisted={Persisted} rejects_duplicate={RejectsDuplicate} " +
                "rejects_missing={RejectsMissing} rejects_other={RejectsOther} " +
                "token={TokenHash} next={NextTokenHash}",
                pageIndex, rawCount, scoped.Count, inWindow.Count,
                inWindow.Count, persisted,
                rejectsDuplicate, rejectsMissing, rejectsOther,
                continuationTokenHash, nextTokenHash);

            if (scoped.Count > 0 && inWindow.Count == 0)
            {
                _logger.LogWarning(
                    "REV_INGEST_V2_PERSIST_GATE_ZERO scoped={Scoped} reason=AllOutsideWindow " +
                    "windowStart={WindowStart} windowEnd={WindowEnd}",
                    scoped.Count, window.Start, window.End);
            }

            continuationToken = nextToken;
            pageIndex++;
            if (page.WasTerminatedEarly)
            {
                terminationReason = page.Termination?.Reason.ToString();
                terminationMessage = page.Termination?.Message;
                _logger.LogWarning(
                    "REV_INGEST_V2_WINDOW_TERMINATED_EARLY windowStart={WindowStart} windowEnd={WindowEnd} pageIndex={PageIndex} reason={TerminationReason} message={TerminationMessage}",
                    window.Start,
                    window.End,
                    pageIndex,
                    terminationReason,
                    terminationMessage);
                break;
            }

        } while (continuationToken != null);

        return new WindowResult(
            Success: true,
            Persisted: totalPersisted,
            Pages: pageIndex,
            StallReason: null,
            LastTokenHash: null,
            ConsecutiveEmptyPages: consecutiveEmptyPages,
            TotalRawRevisions: totalRawRevisions,
            PagesWithoutScopedRevisions: pagesWithoutScopedRevisions,
            EmptyRawPagesWithToken: emptyRawPagesWithToken,
            SegmentsProcessed: segmentsProcessed,
            SegmentsWithZeroScoped: segmentsWithZeroScoped,
            WasTerminatedEarly: terminationReason is not null,
            TerminationReason: terminationReason,
            TerminationMessage: terminationMessage,
            DurationMs: GetElapsedMs(windowStart));
    }

    private async Task<PersistResult> PersistRevisionsAsync(
        PoToolDbContext context,
        IReadOnlyList<WorkItemRevision> revisions,
        CancellationToken cancellationToken)
    {
        if (revisions.Count == 0)
        {
            return new PersistResult(0, 0, 0, 0);
        }

        var options = _persistenceOptions.CurrentValue;
        var autoDetectChangesEnabled = context.ChangeTracker.AutoDetectChangesEnabled;
        IDbContextTransaction? transaction = null;
        int persisted = 0;
        int duplicates = 0;
        int missingRequired = 0;
        int other = 0;

        try
        {
            if (context.Database.IsRelational())
            {
                transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            }

            if (options.Enabled)
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;
            }

            var workItemIds = revisions.Select(r => r.WorkItemId).Distinct().ToList();
            var existingKeys = new HashSet<(int WorkItemId, int RevisionNumber)>();

            if (workItemIds.Count > 0)
            {
                var existingRevisions = await context.RevisionHeaders.AsNoTracking()
                    .Where(h => workItemIds.Contains(h.WorkItemId))
                    .Select(h => new { h.WorkItemId, h.RevisionNumber })
                    .ToListAsync(cancellationToken);

                foreach (var existing in existingRevisions)
                {
                    existingKeys.Add((existing.WorkItemId, existing.RevisionNumber));
                }
            }

            var headers = new List<RevisionHeaderEntity>(revisions.Count);

            foreach (var revision in revisions)
            {
                if (revision.WorkItemId <= 0 || revision.ChangedDate == default || revision.RevisionNumber <= 0)
                {
                    missingRequired++;
                    continue;
                }

                if (existingKeys.Contains((revision.WorkItemId, revision.RevisionNumber)))
                {
                    duplicates++;
                    continue;
                }

                headers.Add(new RevisionHeaderEntity
                {
                    WorkItemId = revision.WorkItemId,
                    RevisionNumber = revision.RevisionNumber,
                    WorkItemType = revision.WorkItemType,
                    Title = revision.Title,
                    State = revision.State,
                    Reason = revision.Reason,
                    IterationPath = revision.IterationPath,
                    AreaPath = revision.AreaPath,
                    CreatedDate = revision.CreatedDate,
                    ChangedDate = revision.ChangedDate,
                    ClosedDate = revision.ClosedDate,
                    Effort = revision.Effort,
                    BusinessValue = revision.BusinessValue,
                    Tags = revision.Tags,
                    Severity = revision.Severity,
                    ChangedBy = revision.ChangedBy,
                    IngestedAt = DateTimeOffset.UtcNow
                });

                persisted++;
            }

            if (headers.Count > 0)
            {
                context.RevisionHeaders.AddRange(headers);
            }

            if (options.Enabled)
            {
                context.ChangeTracker.DetectChanges();
            }

            await context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        catch (DbUpdateException)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            if (options.Enabled)
            {
                context.ChangeTracker.AutoDetectChangesEnabled = autoDetectChangesEnabled;
            }

            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }

        return new PersistResult(persisted, duplicates, missingRequired, other);
    }

    private async Task SaveCheckpointAsync(
        PoToolDbContext context,
        int productOwnerId,
        IngestionWindow window,
        string? continuationToken,
        int pageIndex,
        CancellationToken cancellationToken)
    {
        var watermark = await context.RevisionIngestionWatermarks
            .FirstOrDefaultAsync(w => w.ProductOwnerId == productOwnerId, cancellationToken);

        if (watermark == null)
        {
            watermark = new RevisionIngestionWatermarkEntity
            {
                ProductOwnerId = productOwnerId,
                IsInitialBackfillComplete = false
            };
            context.RevisionIngestionWatermarks.Add(watermark);
        }

        watermark.ContinuationToken = continuationToken;
        watermark.LastIngestionStartedAt = DateTimeOffset.UtcNow;
        watermark.LastSyncStartDateTime = window.Start;
        watermark.LastRunOutcome = "V2_InProgress";
        watermark.LastStableContinuationTokenHash = HashToken(continuationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveCheckpointOnTokenAdvanceAsync(
        int productOwnerId,
        IngestionWindow window,
        string continuationToken,
        int pageIndex,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        await SaveCheckpointAsync(
            context,
            productOwnerId,
            window,
            continuationToken,
            pageIndex,
            cancellationToken);
    }

    private async Task ClearCheckpointAsync(
        PoToolDbContext context,
        int productOwnerId,
        string completedOutcome,
        CancellationToken cancellationToken)
    {
        var watermark = await context.RevisionIngestionWatermarks
            .FirstOrDefaultAsync(w => w.ProductOwnerId == productOwnerId, cancellationToken);

        if (watermark != null)
        {
            watermark.ContinuationToken = null;
            watermark.LastRunOutcome = completedOutcome;
            watermark.LastIngestionCompletedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<(HashSet<int> AllowedIds, int ProductCount)> ResolveAllowedWorkItemIdsAsync(
        PoToolDbContext context,
        ITfsClient tfsClient,
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        var productOwner = await context.Profiles
            .Include(profile => profile.Products)
            .FirstOrDefaultAsync(profile => profile.Id == productOwnerId, cancellationToken);

        if (productOwner == null)
        {
            throw new InvalidOperationException($"ProductOwner {productOwnerId} was not found.");
        }

        if (productOwner.Products.Count == 0)
        {
            throw new InvalidOperationException($"ProductOwner {productOwnerId} has no products configured.");
        }

        var rootWorkItemIds = productOwner.Products
            .Select(p => p.BacklogRootWorkItemId)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (rootWorkItemIds.Length == 0)
        {
            throw new InvalidOperationException($"ProductOwner {productOwnerId} has no valid backlog root work item IDs.");
        }

        var workItems = await tfsClient.GetWorkItemsByRootIdsAsync(
            rootWorkItemIds, null, null, cancellationToken);

        var descendantWorkItems = WorkItemHierarchyHelper.FilterDescendants(rootWorkItemIds, workItems);
        var allowedIds = descendantWorkItems
            .Select(w => w.TfsId)
            .ToHashSet();

        if (allowedIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"ProductOwner {productOwnerId} has no work items under configured backlog roots.");
        }

        return (allowedIds, productOwner.Products.Count);
    }

    private async Task<DateTimeOffset> DeriveBackfillStartAsync(
        IReadOnlyCollection<int> allowedWorkItemIds,
        RevisionIngestionV2Options config,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DateTimeOffset? derivedStart = null;
        bool fallbackUsed = false;
        string reason;

        try
        {
            derivedStart = await _backfillStartProvider.GetEarliestChangedDateUtcAsync(
                allowedWorkItemIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "REV_INGEST_V2_BACKFILL_PROVIDER_ERROR");
        }

        DateTimeOffset backfillStart;

        if (derivedStart != null && derivedStart.Value <= now)
        {
            backfillStart = derivedStart.Value;
            reason = "DerivedFromWorkItems";
        }
        else
        {
            var fallbackDays = Math.Max(config.V2WindowDays * 2, DefaultFallbackDays);
            backfillStart = now.AddDays(-fallbackDays);
            fallbackUsed = true;
            reason = derivedStart == null ? "ProviderReturnedNull" : "DerivedDateInFuture";
        }

        _logger.LogInformation(
            "REV_INGEST_V2_BACKFILL_START derivedStart={DerivedStart} fallbackUsed={FallbackUsed} reason={Reason}",
            backfillStart, fallbackUsed, reason);

        return backfillStart;
    }

    private static List<IngestionWindow> BuildWindows(
        RevisionIngestionV2Options config,
        DateTimeOffset backfillStart,
        DateTimeOffset now)
    {
        var windows = new List<IngestionWindow>();

        if (!config.V2EnableWindowing)
        {
            windows.Add(new IngestionWindow(backfillStart, now));
            return windows;
        }

        var cursor = backfillStart;
        var windowSpan = TimeSpan.FromDays(config.V2WindowDays);

        while (cursor < now)
        {
            var windowEnd = cursor + windowSpan;
            if (windowEnd > now)
            {
                windowEnd = now;
            }

            windows.Add(new IngestionWindow(cursor, windowEnd));
            cursor = windowEnd;
        }

        return windows;
    }

    internal static string? HashToken(string? token)
    {
        if (token == null)
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes)[..12];
    }

    private static long GetElapsedMs(long startTimestamp)
    {
        return (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
    }

    private static int? ResolveSegmentIndexFromContinuationToken(string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken) ||
            !continuationToken.StartsWith("seg:", StringComparison.Ordinal))
        {
            return null;
        }

        var payload = continuationToken["seg:".Length..];
        var separator = payload.IndexOf('|');
        if (separator <= 0)
        {
            return null;
        }

        var indexSlice = payload[..separator];
        return int.TryParse(indexSlice, out var segmentIndex) && segmentIndex >= 0
            ? segmentIndex
            : null;
    }

    private sealed record IngestionWindow(DateTimeOffset Start, DateTimeOffset End);

    private sealed record WindowResult(
        bool Success,
        int Persisted,
        int Pages,
        string? StallReason,
        string? LastTokenHash,
        int ConsecutiveEmptyPages,
        int TotalRawRevisions,
        int PagesWithoutScopedRevisions,
        int EmptyRawPagesWithToken,
        HashSet<int> SegmentsProcessed,
        HashSet<int> SegmentsWithZeroScoped,
        bool WasTerminatedEarly,
        string? TerminationReason,
        string? TerminationMessage,
        long DurationMs);

    private sealed record PersistResult(
        int Persisted,
        int Duplicates,
        int MissingRequired,
        int Other);
}
