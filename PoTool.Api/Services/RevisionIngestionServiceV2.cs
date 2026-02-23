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
    private const int TokenPrefixLength = 24;

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
                        "tokenLen={TokenLen} lastUrlSource={LastUrlSource} lastHost={LastHost} lastPath={LastPath} " +
                        "consecutiveEmptyPages={ConsecutiveEmpty} " +
                        "windowStartUtc={WindowStartUtc} windowEndUtc={WindowEndUtc}",
                        windowResult.StallReason, windowResult.LastTokenHash,
                        windowResult.LastTokenLength,
                        windowResult.LastUrlSource,
                        windowResult.LastHost,
                        windowResult.LastPath,
                        windowResult.ConsecutiveEmptyPages,
                        window.Start, window.End);

                    return new RevisionIngestionResult
                    {
                        Success = false,
                        RunOutcome = RevisionIngestionRunOutcome.Failed,
                        RevisionsIngested = totalPersisted,
                        PagesProcessed = totalPages,
                        ErrorMessage = $"Window failed: reasonCode={windowResult.StallReason} consecutiveEmptyPages={windowResult.ConsecutiveEmptyPages} lastTokenHash={windowResult.LastTokenHash} lastTokenLength={windowResult.LastTokenLength} lastUrlSource={windowResult.LastUrlSource} lastHost={windowResult.LastHost} lastPath={windowResult.LastPath}",
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
                        LastTokenLength: Len(continuationToken),
                        LastUrlSource: ResolveUrlSource(continuationToken),
                        LastHost: TryResolveHostPath(continuationToken).Host,
                        LastPath: TryResolveHostPath(continuationToken).Path,
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
            var continuationSegmentIndex = ResolveSegmentIndexFromContinuationToken(
                continuationToken,
                allowedWorkItemIds,
                out var continuationTokenFormat,
                out var continuationExactMatch,
                out var continuationFallbackIndex);
            if (string.Equals(continuationTokenFormat, "Boundaries", StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "REV_INGEST_V2_SEGMENT_TOKEN_BOUNDARY tokenKind={TokenKind} exactMatch={ExactMatch} fallbackIndex={FallbackIndex}",
                    "Continuation",
                    continuationExactMatch,
                    continuationFallbackIndex);
            }

            var nextSegmentIndex = ResolveSegmentIndexFromContinuationToken(
                nextToken,
                allowedWorkItemIds,
                out var nextTokenFormat,
                out var nextExactMatch,
                out var nextFallbackIndex);
            if (string.Equals(nextTokenFormat, "Boundaries", StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "REV_INGEST_V2_SEGMENT_TOKEN_BOUNDARY tokenKind={TokenKind} exactMatch={ExactMatch} fallbackIndex={FallbackIndex}",
                    "Next",
                    nextExactMatch,
                    nextFallbackIndex);
            }

            var activeSegmentIndex = continuationSegmentIndex ?? nextSegmentIndex;
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
                        "segmentIndex={SegmentIndex} " +
                        "tokenHash={TokenHash} tokenLen={TokenLen} tokenPrefix={TokenPrefix} " +
                        "nextTokenHash={NextTokenHash} nextTokenLen={NextTokenLen} nextTokenPrefix={NextTokenPrefix} " +
                        "windowStartUtc={WindowStartUtc} windowEndUtc={WindowEndUtc} " +
                        "allowedWorkItemIds={AllowedCount} pageSize={PageSize} " +
                        "hasContinuationToken={HasToken} rawCount={RawCount} scopedCount=0 inWindowCount=0",
                        pageIndex, consecutiveEmptyPages,
                        activeSegmentIndex,
                        continuationTokenHash, Len(continuationToken), Prefix(continuationToken),
                        nextTokenHash, Len(nextToken), Prefix(nextToken),
                        window.Start, window.End,
                        allowedWorkItemIds.Count, config.V2PageSize,
                        true, rawCount);
                }

                var emptyWithTokenDumpThreshold = Math.Max(1, config.V2EmptyWithTokenDumpThreshold);
                var emptyWithTokenDumpRepeatInterval = Math.Max(1, config.V2EmptyWithTokenDumpRepeatInterval);
                var maxConsecutiveEmptyWithTokenPages = Math.Max(1, config.V2MaxConsecutiveEmptyPages);
                if (consecutiveEmptyPages >= emptyWithTokenDumpThreshold &&
                    (consecutiveEmptyPages == emptyWithTokenDumpThreshold ||
                     (consecutiveEmptyPages - emptyWithTokenDumpThreshold) % emptyWithTokenDumpRepeatInterval == 0))
                {
                    _logger.LogWarning(
                        "REV_INGEST_V2_STALL_DUMP windowStartUtc={WindowStartUtc} windowEndUtc={WindowEndUtc} " +
                        "pageIndex={PageIndex} segmentIndex={SegmentIndex} " +
                        "tokenHash={TokenHash} tokenLen={TokenLen} tokenPrefix={TokenPrefix} " +
                        "nextTokenHash={NextTokenHash} nextTokenLen={NextTokenLen} nextTokenPrefix={NextTokenPrefix} " +
                        "allowedWorkItemIdsCount={AllowedWorkItemIdsCount}",
                        window.Start,
                        window.End,
                        pageIndex,
                        activeSegmentIndex,
                        continuationTokenHash,
                        Len(continuationToken),
                        Prefix(continuationToken),
                        nextTokenHash,
                        Len(nextToken),
                        Prefix(nextToken),
                        allowedWorkItemIds.Count);
                }

                if (consecutiveEmptyPages >= maxConsecutiveEmptyWithTokenPages)
                {
                    var lastUrlSource = ResolveUrlSource(nextToken);
                    var lastHostPath = TryResolveHostPath(nextToken);
                    _logger.LogWarning(
                        "REV_INGEST_V2_WINDOW_FAIL reason=EmptyWithTokenStall pageIndex={PageIndex} segmentIndex={SegmentIndex} consecutiveEmpty={ConsecutiveEmpty} maxConsecutiveEmpty={MaxConsecutiveEmpty} tokenHash={TokenHash} tokenLen={TokenLen} tokenPrefix={TokenPrefix} nextTokenHash={NextTokenHash} nextTokenLen={NextTokenLen} nextTokenPrefix={NextTokenPrefix} lastUrlSource={LastUrlSource} lastHost={LastHost} lastPath={LastPath} windowStartUtc={WindowStartUtc} windowEndUtc={WindowEndUtc}",
                        pageIndex,
                        activeSegmentIndex,
                        consecutiveEmptyPages,
                        maxConsecutiveEmptyWithTokenPages,
                        continuationTokenHash,
                        Len(continuationToken),
                        Prefix(continuationToken),
                        nextTokenHash,
                        Len(nextToken),
                        Prefix(nextToken),
                        lastUrlSource,
                        lastHostPath.Host,
                        lastHostPath.Path,
                        window.Start,
                        window.End);

                    return new WindowResult(
                        Success: false,
                        Persisted: totalPersisted,
                        Pages: pageIndex,
                        StallReason: "EmptyWithTokenStall",
                        LastTokenHash: nextTokenHash ?? continuationTokenHash,
                        LastTokenLength: Len(nextToken),
                        LastUrlSource: lastUrlSource,
                        LastHost: lastHostPath.Host,
                        LastPath: lastHostPath.Path,
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

                await SaveCheckpointIfTokenAdvancedAsync(
                    productOwnerId,
                    window,
                    continuationToken,
                    nextToken,
                    pageIndex,
                    cancellationToken);

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

            await SaveCheckpointIfTokenAdvancedAsync(
                productOwnerId,
                window,
                continuationToken,
                nextToken,
                pageIndex,
                cancellationToken);

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
            LastTokenLength: 0,
            LastUrlSource: null,
            LastHost: null,
            LastPath: null,
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

    private async Task SaveCheckpointIfTokenAdvancedAsync(
        int productOwnerId,
        IngestionWindow window,
        string? previousToken,
        string? nextToken,
        int pageIndex,
        CancellationToken cancellationToken)
    {
        if (nextToken == null ||
            string.Equals(nextToken, previousToken, StringComparison.Ordinal))
        {
            return;
        }

        await SaveCheckpointOnTokenAdvanceAsync(
            productOwnerId,
            window,
            nextToken,
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
        var earliestChangedWorkItemId = descendantWorkItems
            .Where(workItem => workItem.ChangedDate != null)
            .MinBy(workItem => workItem.ChangedDate)
            ?.TfsId ?? 0;
        _logger.LogInformation(
            "REV_INGEST_V2_SCOPE_EARLIEST_WORKITEM earliestChangedWorkItemId={EarliestChangedWorkItemId}",
            earliestChangedWorkItemId);
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
            "REV_INGEST_V2_BACKFILL_START derivedStartCandidate={DerivedStartCandidate} chosenBackfillStart={ChosenBackfillStart} fallbackUsed={FallbackUsed} reason={Reason}",
            derivedStart, backfillStart, fallbackUsed, reason);

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

    private static string? Prefix(string? value)
    {
        if (value == null)
        {
            return null;
        }

        return value[..Math.Min(TokenPrefixLength, value.Length)];
    }

    private static int Len(string? value)
    {
        return value?.Length ?? 0;
    }

    private static string ResolveUrlSource(string? continuationToken)
    {
        var resolvedToken = ResolveInnerContinuationToken(continuationToken);
        if (resolvedToken is null)
        {
            return "InitialCanonical";
        }

        if (resolvedToken.StartsWith("next:", StringComparison.Ordinal) ||
            Uri.IsWellFormedUriString(resolvedToken, UriKind.Absolute))
        {
            return "NextLinkVerbatim";
        }

        if (resolvedToken.StartsWith("seek:", StringComparison.Ordinal))
        {
            return "SeekVerbatim";
        }

        return "InitialCanonical";
    }

    private static (string? Host, string? Path) TryResolveHostPath(string? continuationToken)
    {
        var resolvedToken = ResolveInnerContinuationToken(continuationToken);
        if (resolvedToken is null)
        {
            return (null, null);
        }

        if (resolvedToken.StartsWith("next:", StringComparison.Ordinal) ||
            resolvedToken.StartsWith("seek:", StringComparison.Ordinal))
        {
            var payload = resolvedToken[5..];
            var separator = payload.IndexOf('|');
            var encodedUrl = separator >= 0 ? payload[..separator] : payload;
            try
            {
                var decodedUrl = Encoding.UTF8.GetString(Convert.FromBase64String(encodedUrl));
                if (Uri.TryCreate(decodedUrl, UriKind.Absolute, out var decodedUri))
                {
                    return (decodedUri.Host, decodedUri.AbsolutePath);
                }
            }
            catch
            {
                return (null, null);
            }
        }

        if (Uri.TryCreate(resolvedToken, UriKind.Absolute, out var absoluteUri))
        {
            return (absoluteUri.Host, absoluteUri.AbsolutePath);
        }

        return (null, null);
    }

    private static string? ResolveInnerContinuationToken(string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return null;
        }

        if (!continuationToken.StartsWith("seg:", StringComparison.Ordinal))
        {
            return continuationToken;
        }

        var payload = continuationToken["seg:".Length..];
        var separator = payload.IndexOf('|');
        if (separator < 0 || separator == payload.Length - 1)
        {
            return continuationToken;
        }

        var encodedInner = payload[(separator + 1)..];
        try
        {
            var decodedInner = Encoding.UTF8.GetString(Convert.FromBase64String(encodedInner));
            return string.IsNullOrWhiteSpace(decodedInner) ? null : decodedInner;
        }
        catch
        {
            return continuationToken;
        }
    }

    private static long GetElapsedMs(long startTimestamp)
    {
        return (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
    }

    internal static int? ResolveSegmentIndexFromContinuationToken(
        string? continuationToken,
        IReadOnlyCollection<int>? allowedWorkItemIds,
        out string tokenFormat,
        out bool exactMatchFound,
        out int? orderedFallbackIndex)
    {
        tokenFormat = "None";
        exactMatchFound = false;
        orderedFallbackIndex = null;

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

        var segmentSlice = payload[..separator];
        if (int.TryParse(segmentSlice, out var legacySegmentIndex) && legacySegmentIndex >= 0)
        {
            tokenFormat = "Index";
            exactMatchFound = true;
            return legacySegmentIndex;
        }

        var segmentParts = segmentSlice.Split(':');
        if (segmentParts.Length != 2 ||
            !int.TryParse(segmentParts[0], out var segmentStart) ||
            !int.TryParse(segmentParts[1], out var segmentEnd))
        {
            return null;
        }

        tokenFormat = "Boundaries";
        var diagnosticSegments = BuildDiagnosticSegments(allowedWorkItemIds);
        if (diagnosticSegments.Count == 0)
        {
            orderedFallbackIndex = 0;
            return 0;
        }

        for (var i = 0; i < diagnosticSegments.Count; i++)
        {
            var diagnosticSegment = diagnosticSegments[i];
            if (diagnosticSegment.Start == segmentStart &&
                diagnosticSegment.End == segmentEnd)
            {
                exactMatchFound = true;
                return i;
            }
        }

        orderedFallbackIndex = diagnosticSegments
            .TakeWhile(diagnosticSegment => diagnosticSegment.End < segmentStart)
            .Count();
        return orderedFallbackIndex;
    }

    private static IReadOnlyList<DiagnosticSegment> BuildDiagnosticSegments(IReadOnlyCollection<int>? allowedWorkItemIds)
    {
        if (allowedWorkItemIds == null || allowedWorkItemIds.Count == 0)
        {
            return [];
        }

        var orderedIds = allowedWorkItemIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        if (orderedIds.Length == 0)
        {
            return [];
        }

        const int maxSpan = 500;
        var segments = new List<DiagnosticSegment>();
        var rangeStart = orderedIds[0];
        var rangeEnd = orderedIds[0];

        for (var i = 1; i < orderedIds.Length; i++)
        {
            var candidate = orderedIds[i];
            var gap = candidate - rangeEnd - 1;
            if (gap == 0)
            {
                rangeEnd = candidate;
                continue;
            }

            AddSplitDiagnosticSegments(segments, rangeStart, rangeEnd, maxSpan);
            rangeStart = candidate;
            rangeEnd = candidate;
        }

        AddSplitDiagnosticSegments(segments, rangeStart, rangeEnd, maxSpan);
        return segments;
    }

    private static void AddSplitDiagnosticSegments(List<DiagnosticSegment> segments, int start, int end, int maxSpan)
    {
        var current = start;
        while (current <= end)
        {
            var segmentEnd = Math.Min(end, current + maxSpan - 1);
            segments.Add(new DiagnosticSegment(current, segmentEnd));
            current = segmentEnd + 1;
        }
    }

    private sealed record IngestionWindow(DateTimeOffset Start, DateTimeOffset End);
    private readonly record struct DiagnosticSegment(int Start, int End);

    private sealed record WindowResult(
        bool Success,
        int Persisted,
        int Pages,
        string? StallReason,
        string? LastTokenHash,
        int LastTokenLength,
        string? LastUrlSource,
        string? LastHost,
        string? LastPath,
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
