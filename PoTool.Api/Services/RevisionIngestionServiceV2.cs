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

    private static readonly DateTimeOffset BackfillStartMinimumUtc =
        new(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    public RevisionIngestionServiceV2(
        IServiceScopeFactory scopeFactory,
        ILogger<RevisionIngestionServiceV2> logger,
        IOptionsMonitor<RevisionIngestionV2Options> options,
        IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions> persistenceOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
        _persistenceOptions = persistenceOptions;
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

            // Phase 2: Determine windows
            var windows = BuildWindows(config);

            // Phase 3: Process each window
            foreach (var window in windows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation(
                    "REV_INGEST_V2_WINDOW_START start={WindowStart} end={WindowEnd}",
                    window.Start, window.End);

                var windowResult = await ProcessWindowAsync(
                    productOwnerId, allowedWorkItemIds, window, config, cancellationToken);

                totalPersisted += windowResult.Persisted;
                totalPages += windowResult.Pages;

                if (!windowResult.Success)
                {
                    _logger.LogWarning(
                        "REV_INGEST_V2_WINDOW_FAIL reason={Reason} tokenHash={TokenHash} retries={Retries}",
                        windowResult.StallReason, windowResult.LastTokenHash, windowResult.RetryCount);

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
                    "REV_INGEST_V2_WINDOW_END persistedTotal={Persisted} pages={Pages} duration={DurationMs}ms",
                    windowResult.Persisted, windowResult.Pages, windowResult.DurationMs);
            }

            return new RevisionIngestionResult
            {
                Success = true,
                RunOutcome = RevisionIngestionRunOutcome.CompletedNormally,
                RevisionsIngested = totalPersisted,
                PagesProcessed = totalPages,
                Message = $"V2 ingestion completed. Persisted={totalPersisted} Pages={totalPages}"
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
        CancellationToken cancellationToken)
    {
        var windowStart = Stopwatch.GetTimestamp();
        string? continuationToken = null;
        string? previousToken = null;
        int pageIndex = 0;
        int totalPersisted = 0;
        int consecutiveEmptyPages = 0;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Defensive: detect repeated token
            if (continuationToken != null && continuationToken == previousToken)
            {
                return new WindowResult(
                    Success: false,
                    Persisted: totalPersisted,
                    Pages: pageIndex,
                    StallReason: "RepeatedToken",
                    LastTokenHash: HashToken(continuationToken),
                    RetryCount: 0,
                    DurationMs: GetElapsedMs(windowStart));
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

            // Empty page with token: bounded retry
            if (rawCount == 0 && nextToken != null)
            {
                consecutiveEmptyPages++;

                if (consecutiveEmptyPages > config.V2MaxEmptyPageRetries)
                {
                    return new WindowResult(
                        Success: false,
                        Persisted: totalPersisted,
                        Pages: pageIndex + 1,
                        StallReason: "EmptyPageWithToken",
                        LastTokenHash: HashToken(nextToken),
                        RetryCount: consecutiveEmptyPages,
                        DurationMs: GetElapsedMs(windowStart));
                }

                // Small backoff before retry
                await Task.Delay(500 * consecutiveEmptyPages, cancellationToken);

                _logger.LogWarning(
                    "REV_INGEST_V2_EMPTY_PAGE_RETRY page={PageIndex} retryCount={RetryCount} tokenHash={TokenHash}",
                    pageIndex, consecutiveEmptyPages, HashToken(nextToken));

                previousToken = continuationToken;
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

                // Checkpoint after successful persistence
                await SaveCheckpointAsync(
                    context, productOwnerId, window, nextToken, pageIndex, cancellationToken);
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
                HashToken(continuationToken), HashToken(nextToken));

            if (scoped.Count > 0 && inWindow.Count == 0)
            {
                _logger.LogWarning(
                    "REV_INGEST_V2_PERSIST_GATE_ZERO scoped={Scoped} reason=AllOutsideWindow " +
                    "windowStart={WindowStart} windowEnd={WindowEnd}",
                    scoped.Count, window.Start, window.End);
            }

            previousToken = continuationToken;
            continuationToken = nextToken;
            pageIndex++;

        } while (continuationToken != null);

        return new WindowResult(
            Success: true,
            Persisted: totalPersisted,
            Pages: pageIndex,
            StallReason: null,
            LastTokenHash: null,
            RetryCount: 0,
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

    private static List<IngestionWindow> BuildWindows(RevisionIngestionV2Options config)
    {
        var now = DateTimeOffset.UtcNow;
        var windows = new List<IngestionWindow>();

        if (!config.V2EnableWindowing)
        {
            windows.Add(new IngestionWindow(BackfillStartMinimumUtc, now));
            return windows;
        }

        var cursor = BackfillStartMinimumUtc;
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

    private sealed record IngestionWindow(DateTimeOffset Start, DateTimeOffset End);

    private sealed record WindowResult(
        bool Success,
        int Persisted,
        int Pages,
        string? StallReason,
        string? LastTokenHash,
        int RetryCount,
        long DurationMs);

    private sealed record PersistResult(
        int Persisted,
        int Duplicates,
        int MissingRequired,
        int Other);
}
