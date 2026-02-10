using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Configuration;
using PoTool.Core.WorkItems;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Integrations.Tfs.Diagnostics;

namespace PoTool.Api.Services;

/// <summary>
/// Service for ingesting work item revisions from TFS.
/// Implements single-flight behavior to prevent concurrent ingestion for the same ProductOwner.
/// </summary>
public class RevisionIngestionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RevisionIngestionService> _logger;
    private readonly RevisionIngestionDiagnostics _diagnostics;
    private readonly TfsRequestThrottler _throttler;
    private readonly IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions> _persistenceOptions;
    private readonly IOptionsMonitor<RevisionIngestionPaginationOptions> _paginationOptions;
    private readonly IDataProtector _tokenProtector;

    private const string ContinuationTokenProtectorPurpose = "RevisionIngestionContinuationToken";
    private const int ContinuationTokenHashLength = 12;
    // Clamp backfill start dates to year 2000 to avoid pathological values from invalid data.
    private static readonly DateTimeOffset BackfillStartMinimumUtc =
        new(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    private static readonly TimeSpan BackfillFallbackWindow = TimeSpan.FromDays(180);

    private sealed record WorkItemDateSnapshot(
        int TfsId,
        int? ParentTfsId,
        DateTimeOffset? CreatedDate,
        DateTimeOffset? TfsChangedDate);

    // Concurrency control: one ingestion per ProductOwner
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _ingestionLocks = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeIngestions = new();

    public RevisionIngestionService(
        IServiceScopeFactory scopeFactory,
        ILogger<RevisionIngestionService> logger,
        RevisionIngestionDiagnostics diagnostics,
        TfsRequestThrottler throttler,
        IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions> persistenceOptions,
        IOptionsMonitor<RevisionIngestionPaginationOptions> paginationOptions,
        IDataProtectionProvider dataProtectionProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _diagnostics = diagnostics;
        _throttler = throttler;
        _persistenceOptions = persistenceOptions;
        _paginationOptions = paginationOptions;
        _tokenProtector = dataProtectionProvider.CreateProtector(ContinuationTokenProtectorPurpose);
    }

    /// <summary>
    /// Ingests work item revisions for a ProductOwner.
    /// Performs initial backfill if not done, otherwise incremental sync.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="progressCallback">Optional callback for progress reporting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the ingestion operation.</returns>
    public async Task<RevisionIngestionResult> IngestRevisionsAsync(
        int productOwnerId,
        Action<RevisionIngestionProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var semaphore = _ingestionLocks.GetOrAdd(productOwnerId, _ => new SemaphoreSlim(1, 1));

        // Try to acquire lock - if ingestion is already running, report and exit
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Revision ingestion already in progress for ProductOwner {ProductOwnerId}", productOwnerId);
            return new RevisionIngestionResult
            {
                RunOutcome = RevisionIngestionRunOutcome.CompletedNormally,
                Success = false,
                IsAlreadyRunning = true,
                Message = "Revision ingestion already in progress"
            };
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeIngestions[productOwnerId] = cts;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
            var revisionClient = scope.ServiceProvider.GetRequiredService<IRevisionTfsClient>();
            var tfsClient = scope.ServiceProvider.GetRequiredService<ITfsClient>();
            var allowedWorkItemIds = await ResolveAllowedWorkItemIdsForProductOwnerAsync(
                context,
                tfsClient,
                productOwnerId,
                cts.Token);

            // Get or create watermark record
            var watermark = await GetOrCreateWatermarkAsync(context, productOwnerId, cts.Token);

            _logger.LogInformation(
                "Starting revision ingestion for ProductOwner {ProductOwnerId}. IsBackfillComplete: {IsBackfillComplete}",
                productOwnerId, watermark.IsInitialBackfillComplete);

            // Update watermark to indicate ingestion started
            watermark.LastIngestionStartedAt = DateTimeOffset.UtcNow;
            watermark.LastErrorMessage = null;
            watermark.LastErrorAt = null;
            await context.SaveChangesAsync(cts.Token);

            progressCallback?.Invoke(new RevisionIngestionProgress
            {
                Stage = watermark.IsInitialBackfillComplete ? "Incremental Sync" : "Initial Backfill",
                PercentComplete = 0,
                RevisionsProcessed = 0
            });

            int totalRevisions = 0;
            string? continuationToken = UnprotectContinuationToken(watermark.ContinuationToken);
            // For incremental sync, use last sync start time
            // For backfill with no last sync time, infer from cached work items
            DateTimeOffset? startDateTime = watermark.IsInitialBackfillComplete 
                ? watermark.LastSyncStartDateTime 
                : await InferBackfillStartDateTimeAsync(context, productOwnerId, cts.Token);
            ReportingRevisionsTermination? termination = null;
            var runOutcome = RevisionIngestionRunOutcome.CompletedNormally;
            var paginationOptions = _paginationOptions.CurrentValue;
            var maxTotalPages = Math.Max(1, paginationOptions.MaxTotalPages);
            var pageTracker = new ReportingRevisionsPageTracker();
            var scopedFieldDumpLogged = false;

            var runStartUtc = DateTimeOffset.UtcNow;
            using var runScope = _diagnostics.StartRun(
                productOwnerId,
                isBackfill: !watermark.IsInitialBackfillComplete,
                startDateTime,
                runStartUtc,
                _throttler.ReadConcurrency,
                _throttler.WriteConcurrency,
                hydrationConcurrency: 0,
                out var runContext);

            if (runContext.IsEnabled)
            {
                var dbProvider = context.Database.ProviderName ?? "Unknown";
                var connectionMode = GetConnectionMode(context);
                _diagnostics.LogRunDatabase(runContext, dbProvider, connectionMode);
            }

            // Record sync start time for next incremental sync
            var syncStartTime = DateTimeOffset.UtcNow;

            // Ingest revisions in batches
            bool hasMore = true;

            while (hasMore && !cts.Token.IsCancellationRequested)
            {
                var pageIndex = pageTracker.NextPageIndex();
                _logger.LogDebug(
                    "Fetching revision page {PageNumber} for ProductOwner {ProductOwnerId}",
                    pageIndex, productOwnerId);

                var logPerPageSummary = runContext.IsEnabled && runContext.LogPerPageSummary;
                var pageStartTimestamp = logPerPageSummary ? Stopwatch.GetTimestamp() : 0;
                using var pageScope = _diagnostics.BeginPageScope(runContext, pageIndex);
                var pageWorkItemIds = logPerPageSummary ? new HashSet<int>() : null;

                var pageRequestStartTimestamp = Stopwatch.GetTimestamp();
                var result = await revisionClient.GetReportingRevisionsAsync(
                    startDateTime,
                    continuationToken,
                    expandMode: ReportingExpandMode.None,
                    cts.Token);

                var rawRevisionCount = result.Revisions.Count;
                var scopedRevisions = result.Revisions
                    .Where(revision => allowedWorkItemIds.Contains(revision.WorkItemId))
                    .ToList();
                var scopedRevisionCount = scopedRevisions.Count;
                var scopedRevisionsPersistedCount = 0; // Will be set after persistence
                var pageContinuationToken = result.ContinuationToken;
                var pageRequestDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(pageRequestStartTimestamp);
                var tokenAdvanced = pageTracker.IsTokenAdvanced(pageContinuationToken);
                var tokenTracking = pageTracker.TrackToken(pageContinuationToken);
                var hasMoreResults = result.HasMoreResults;
                if (result.Termination != null)
                {
                    termination = result.Termination;
                    _logger.LogWarning(
                        "Reporting revisions pagination terminated early. Reason={Reason} Message={Message}",
                        termination.Reason,
                        termination.Message);
                }

                // Emit a single diagnostic dump of whitelisted fields for the first scoped page only.
                if (!scopedFieldDumpLogged && scopedRevisionCount > 0)
                {
                    LogScopedRevisionFieldSnapshot(pageIndex, scopedRevisions);
                    scopedFieldDumpLogged = true;
                }

                var persistMetrics = new PersistMetrics();

                // Process and persist revisions
                var persistedCount = await PersistRevisionsAsync(context, scopedRevisions, persistMetrics, cts.Token);
                scopedRevisionsPersistedCount = persistedCount;
                totalRevisions += persistedCount;

                var persistDurationMs = logPerPageSummary ? persistMetrics.PersistDurationMs : 0;
                if (logPerPageSummary && _logger.IsEnabled(LogLevel.Information))
                {
                    var memoryMb = GC.GetTotalMemory(false) / (1024d * 1024d);
                    _logger.LogInformation(
                        "Reporting revisions page summary. PageIndex={PageIndex} RawRevisionCount={RawRevisionCount} ScopedRevisionCount={ScopedRevisionCount} PersistedCount={PersistedCount} HasMoreResults={HasMoreResults} ContinuationTokenHash={ContinuationTokenHash} TokenAdvanced={TokenAdvanced} SeenTokenRepeated={SeenTokenRepeated} DurationMs={DurationMs} TotalPersistedSoFar={TotalPersistedSoFar} MemoryMb={MemoryMb}",
                        pageIndex,
                        rawRevisionCount,
                        scopedRevisionCount,
                        scopedRevisionsPersistedCount,
                        hasMoreResults,
                        tokenTracking.TokenHash,
                        tokenAdvanced,
                        tokenTracking.SeenTokenRepeated,
                        pageRequestDurationMs,
                        totalRevisions,
                        memoryMb);
                }

                // Track impacted work item IDs for relation hydration
                foreach (var revision in scopedRevisions)
                {
                    pageWorkItemIds?.Add(revision.WorkItemId);
                }

                _logger.LogInformation(
                    "Persisted {Count} revisions (page {Page}) for ProductOwner {ProductOwnerId}",
                    persistedCount, pageIndex, productOwnerId);

                _logger.LogInformation(
                    "Revision ingestion page persist. TransactionUsed={TransactionUsed} PersistDurationMs={PersistDurationMs} SaveChangesDurationMs={SaveChangesDurationMs} CommitDurationMs={CommitDurationMs} RevisionHeaderCount={RevisionHeaderCount} FieldDeltaCount={FieldDeltaCount} RelationDeltaCount={RelationDeltaCount}",
                    persistMetrics.TransactionUsed,
                    persistMetrics.PersistDurationMs,
                    persistMetrics.SaveChangesDurationMs,
                    persistMetrics.CommitDurationMs ?? -1,
                    persistMetrics.RevisionHeaderCount,
                    persistMetrics.FieldDeltaCount,
                    persistMetrics.RelationDeltaCount);

                if (runContext.LogEfSaveChangesDetails)
                {
                    _diagnostics.LogSaveChangesDetails(
                        runContext,
                        persistMetrics.SaveChangesDurationMs,
                        persistMetrics.RevisionHeaderCount,
                        persistMetrics.FieldDeltaCount,
                        persistMetrics.RelationDeltaCount);
                }

                if (runContext.IsEnabled &&
                    runContext.LogGcStatsEveryNPages > 0 &&
                    pageIndex % runContext.LogGcStatsEveryNPages == 0)
                {
                    var trackedEntries = context.ChangeTracker.Entries().Count();
                    _diagnostics.LogGcStats(
                        runContext,
                        GC.GetTotalMemory(false),
                        GC.CollectionCount(0),
                        GC.CollectionCount(1),
                        GC.CollectionCount(2),
                        trackedEntries);
                }

                context.ChangeTracker.Clear();
                context.RevisionIngestionWatermarks.Attach(watermark);

                continuationToken = pageContinuationToken;

                if (pageContinuationToken != null || rawRevisionCount > 0)
                {
                    watermark.ContinuationToken = ProtectContinuationToken(pageContinuationToken);
                    context.Entry(watermark).State = EntityState.Modified;
                    await context.SaveChangesAsync(cts.Token);
                }

                progressCallback?.Invoke(new RevisionIngestionProgress
                {
                    Stage = watermark.IsInitialBackfillComplete ? "Incremental Sync" : "Initial Backfill",
                    PercentComplete = result.HasMoreResults ? 0 : 100, // Keep at 0% until the final page completes because total pages are unknown
                    RevisionsProcessed = totalRevisions,
                    CurrentPage = pageIndex
                });

                if (logPerPageSummary)
                {
                    var continuationTokenPresent = !string.IsNullOrEmpty(result.ContinuationToken);
                    var continuationTokenLength = continuationTokenPresent ? result.ContinuationToken!.Length : 0;
                    var httpDurationMs = result.HttpDurationMs ?? 0;
                    var dbSlow = persistDurationMs >= runContext.SlowDbThresholdMs;
                    var httpSlow = httpDurationMs >= runContext.SlowHttpThresholdMs;
                    var totalPageDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(pageStartTimestamp);
                    var pageSlow = totalPageDurationMs >= runContext.SlowPageThresholdMs;
                    var memoryBytes = GC.GetTotalMemory(false);

                    _diagnostics.LogPageRequest(
                        runContext,
                        continuationTokenPresent,
                        continuationTokenLength,
                        result.HttpDurationMs,
                        result.HttpStatusCode,
                        result.ParseDurationMs,
                        result.TransformDurationMs);

                    _diagnostics.LogPageCounts(
                        runContext,
                        rawRevisionCount,
                        scopedRevisionCount,
                        pageWorkItemIds?.Count ?? 0,
                        persistMetrics?.RevisionHeaderCount ?? 0,
                        persistMetrics?.FieldDeltaCount ?? 0,
                        persistMetrics?.RelationDeltaCount ?? 0);

                    _diagnostics.LogPagePersistence(
                        runContext,
                        persistDurationMs,
                        totalPageDurationMs,
                        pageSlow,
                        httpSlow,
                        dbSlow,
                        memoryBytes);
                }

                if (termination == null && result.Termination != null)
                {
                    termination = result.Termination;
                    runOutcome = RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly;
                }

                var paginationAnomaly = termination != null;

                // Apply pagination stop/continue rules in priority order:
                // 1. Explicit termination from TFS client
                if (paginationAnomaly)
                {
                    hasMore = false;
                }
                // 2. Token repeated (safety: infinite loop prevention)
                else if (tokenTracking.SeenTokenRepeated)
                {
                    termination = new ReportingRevisionsTermination(
                        ReportingRevisionsTerminationReason.RepeatedContinuationToken,
                        $"Continuation token repeated on page {pageIndex}.");
                    LogEarlyTermination(
                        "TokenRepeated",
                        pageIndex,
                        tokenTracking.TokenHash,
                        totalRevisions);
                    paginationAnomaly = true;
                    hasMore = false;
                }
                // 3. No more results from TFS (normal completion)
                else if (!hasMoreResults)
                {
                    hasMore = false;
                }
                // 4. Dead page or stalled pagination (API anomaly)
                else if (rawRevisionCount == 0 && (hasMoreResults || !tokenAdvanced || tokenTracking.SeenTokenRepeated))
                {
                    termination = new ReportingRevisionsTermination(
                        ReportingRevisionsTerminationReason.ProgressWithoutData,
                        $"Reporting revisions returned no data on page {pageIndex} while indicating more results.");
                    LogEarlyTermination(
                        "DeadPageNoData",
                        pageIndex,
                        tokenTracking.TokenHash,
                        totalRevisions);
                    paginationAnomaly = true;
                    hasMore = false;
                }
                // 5. Token did not advance (safety: infinite loop prevention)
                else if (!tokenAdvanced)
                {
                    termination = new ReportingRevisionsTermination(
                        ReportingRevisionsTerminationReason.RepeatedContinuationToken,
                        $"Continuation token did not advance on page {pageIndex}.");
                    LogEarlyTermination(
                        "TokenNotAdvanced",
                        pageIndex,
                        tokenTracking.TokenHash,
                        totalRevisions);
                    paginationAnomaly = true;
                    hasMore = false;
                }
                // 6. MaxTotalPages exceeded (safety: prevent runaway pagination)
                else if (pageIndex >= maxTotalPages)
                {
                    termination = new ReportingRevisionsTermination(
                        ReportingRevisionsTerminationReason.MaxTotalPages,
                        $"Exceeded maximum total pages ({maxTotalPages}) on page {pageIndex}.");
                    LogEarlyTermination(
                        "MaxTotalPages",
                        pageIndex,
                        tokenTracking.TokenHash,
                        totalRevisions);
                    paginationAnomaly = true;
                    hasMore = false;
                }
                // 7. Continue to next page (only when data returned and token advanced)
                else
                {
                    pageTracker.CommitToken(pageContinuationToken);
                    hasMore = true;
                }

                if (paginationAnomaly)
                {
                    runOutcome = RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly;
                }

                pageWorkItemIds?.Clear();
            }

            // Mark ingestion complete
            context.ChangeTracker.Clear();
            context.RevisionIngestionWatermarks.Attach(watermark);
            watermark.LastIngestionCompletedAt = DateTimeOffset.UtcNow;
            watermark.LastIngestionRevisionCount = totalRevisions;
            watermark.LastRunOutcome = runOutcome.ToString();
            if (termination == null)
            {
                watermark.ContinuationToken = null; // Clear token on successful completion
            }

            if (termination != null)
            {
                watermark.LastErrorMessage = $"Reporting revisions pagination ended early ({termination.Reason}): {termination.Message}";
                watermark.LastErrorAt = DateTimeOffset.UtcNow;
            }

            if (!watermark.IsInitialBackfillComplete && termination == null)
            {
                watermark.IsInitialBackfillComplete = true;
                _logger.LogInformation("Initial backfill completed for ProductOwner {ProductOwnerId}", productOwnerId);
            }

            if (termination == null)
            {
                // Update watermark for next incremental sync
                watermark.LastSyncStartDateTime = syncStartTime;
            }
            context.Entry(watermark).State = EntityState.Modified;
            await context.SaveChangesAsync(cts.Token);
            context.ChangeTracker.Clear();

            progressCallback?.Invoke(new RevisionIngestionProgress
            {
                Stage = "Complete",
                PercentComplete = 100,
                RevisionsProcessed = totalRevisions,
                IsComplete = true
            });

            _logger.LogInformation(
                "Revision ingestion completed for ProductOwner {ProductOwnerId}: {TotalRevisions} revisions in {Pages} pages. Outcome={Outcome}",
                productOwnerId, totalRevisions, pageTracker.PageIndex, runOutcome);

            return new RevisionIngestionResult
            {
                RunOutcome = runOutcome,
                Success = true,
                RevisionsIngested = totalRevisions,
                PagesProcessed = pageTracker.PageIndex,
                WasTerminatedEarly = termination != null,
                TerminationReason = termination?.Reason,
                TerminationMessage = termination?.Message,
                Message = termination != null
                    ? $"Ingestion terminated early after {totalRevisions} revisions: {termination.Message}"
                    : $"Successfully ingested {totalRevisions} revisions"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Revision ingestion cancelled for ProductOwner {ProductOwnerId}", productOwnerId);
            return new RevisionIngestionResult
            {
                RunOutcome = RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly,
                Success = false,
                WasCancelled = true,
                Message = "Ingestion was cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Revision ingestion failed for ProductOwner {ProductOwnerId}", productOwnerId);

            // Record error in watermark
            try
            {
                using var errorScope = _scopeFactory.CreateScope();
                var errorContext = errorScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
                var watermark = await errorContext.RevisionIngestionWatermarks
                    .FirstOrDefaultAsync(w => w.ProductOwnerId == productOwnerId, CancellationToken.None);

                if (watermark != null)
                {
                    watermark.LastErrorMessage = ex.Message;
                    watermark.LastErrorAt = DateTimeOffset.UtcNow;
                    await errorContext.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception recordEx)
            {
                _logger.LogWarning(recordEx, "Failed to record ingestion error for ProductOwner {ProductOwnerId}", productOwnerId);
            }

            return new RevisionIngestionResult
            {
                RunOutcome = RevisionIngestionRunOutcome.CompletedWithPaginationAnomaly,
                Success = false,
                ErrorMessage = ex.Message,
                Message = $"Ingestion failed: {ex.Message}"
            };
        }
        finally
        {
            _activeIngestions.TryRemove(productOwnerId, out _);
            semaphore.Release();
        }
    }

    /// <summary>
    /// Cancels an in-progress ingestion.
    /// </summary>
    public void CancelIngestion(int productOwnerId)
    {
        if (_activeIngestions.TryGetValue(productOwnerId, out var cts))
        {
            _logger.LogInformation("Cancelling revision ingestion for ProductOwner {ProductOwnerId}", productOwnerId);
            cts.Cancel();
        }
    }

    /// <summary>
    /// Checks if ingestion is currently running.
    /// </summary>
    public bool IsIngestionRunning(int productOwnerId)
    {
        return _activeIngestions.ContainsKey(productOwnerId);
    }

    /// <summary>
    /// Infers the backfill start date from the earliest cached work item for a product owner.
    /// Returns null if no cached work items exist (will scan all history).
    /// Subtracts a 1-day buffer to ensure we don't miss revisions.
    /// </summary>
    private async Task<DateTimeOffset?> InferBackfillStartDateTimeAsync(
        PoToolDbContext context,
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        // Get the product owner and their product roots
        var productOwner = await context.Profiles
            .Include(profile => profile.Products)
            .FirstOrDefaultAsync(profile => profile.Id == productOwnerId, cancellationToken);

        if (productOwner == null || productOwner.Products.Count == 0)
        {
            _logger.LogDebug("No products found for ProductOwner {ProductOwnerId}, backfill will scan all history", productOwnerId);
            return null;
        }

        var rootWorkItemIds = productOwner.Products
            .Select(product => product.BacklogRootWorkItemId)
            .Where(rootId => rootId > 0)
            .Distinct()
            .ToList();

        if (rootWorkItemIds.Count == 0)
        {
            _logger.LogDebug("No valid backlog root IDs for ProductOwner {ProductOwnerId}, backfill will scan all history", productOwnerId);
            return null;
        }

        // Query cached work items and find the earliest date on the client side
        // Note: SQLite doesn't support Min/OrderBy on DateTimeOffset, so we need client-side evaluation
        // To minimize memory usage, we select only the date fields and use ToListAsync with a small projection
        var workItemDates = await context.WorkItems
            .Select(wi => new WorkItemDateSnapshot(wi.TfsId, wi.ParentTfsId, wi.CreatedDate, wi.TfsChangedDate))
            .ToListAsync(cancellationToken);

        if (workItemDates.Count == 0)
        {
            _logger.LogWarning(
                "No cached work items available for ProductOwner {ProductOwnerId}, backfill will scan all history",
                productOwnerId);
            return null;
        }

        var scopedWorkItems = FilterWorkItemsToScope(workItemDates, rootWorkItemIds);

        if (scopedWorkItems.Count == 0)
        {
            _logger.LogWarning(
                "No cached work items found within product root hierarchy for ProductOwner {ProductOwnerId}, backfill will scan all history",
                productOwnerId);
            return null;
        }

        // Find the earliest CreatedDate on the client side
        var earliestCreatedDate = scopedWorkItems
            .Where(wi => wi.CreatedDate.HasValue)
            .Select(wi => wi.CreatedDate!.Value.ToUniversalTime())
            .OrderBy(d => d)
            .FirstOrDefault();

        if (earliestCreatedDate != default(DateTimeOffset))
        {
            // Subtract 1 day buffer to ensure we don't miss revisions
            var inferredStartDate = ClampBackfillStartDate(earliestCreatedDate.AddDays(-1), productOwnerId, "CreatedDate");
            _logger.LogInformation(
                "Inferred backfill start date from earliest CreatedDate for ProductOwner {ProductOwnerId}: {StartDate} (1 day before {EarliestDate})",
                productOwnerId,
                inferredStartDate,
                earliestCreatedDate);
            return inferredStartDate;
        }

        // Fallback to TfsChangedDate if CreatedDate is not available
        var earliestChangedDate = scopedWorkItems
            .Where(wi => wi.TfsChangedDate.HasValue)
            .Select(wi => wi.TfsChangedDate!.Value.ToUniversalTime())
            .OrderBy(d => d)
            .FirstOrDefault();

        if (earliestChangedDate != default(DateTimeOffset))
        {
            // Subtract 1 day buffer to ensure we don't miss revisions
            var inferredStart = ClampBackfillStartDate(earliestChangedDate.AddDays(-1), productOwnerId, "TfsChangedDate");
            _logger.LogInformation(
                "Inferred backfill start date from earliest TfsChangedDate for ProductOwner {ProductOwnerId}: {StartDate} (1 day before {EarliestDate})",
                productOwnerId,
                inferredStart,
                earliestChangedDate);
            return inferredStart;
        }

        _logger.LogWarning(
            "No valid CreatedDate or TfsChangedDate values found in scoped work items for ProductOwner {ProductOwnerId}, backfill will scan all history",
            productOwnerId);
        return null;
    }

    private static IReadOnlyList<WorkItemDateSnapshot> FilterWorkItemsToScope(
        IReadOnlyList<WorkItemDateSnapshot> workItems,
        IReadOnlyCollection<int> rootWorkItemIds)
    {
        var childrenLookup = workItems
            .Where(wi => wi.ParentTfsId.HasValue)
            .GroupBy(wi => wi.ParentTfsId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var scopedIds = new HashSet<int>(rootWorkItemIds);
        var queue = new Queue<int>(rootWorkItemIds);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (!childrenLookup.TryGetValue(parentId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (scopedIds.Add(child.TfsId))
                {
                    queue.Enqueue(child.TfsId);
                }
            }
        }

        return workItems
            .Where(item => scopedIds.Contains(item.TfsId))
            .ToList();
    }

    private DateTimeOffset GetFallbackBackfillStartDate(int productOwnerId, string reason)
    {
        var fallback = DateTimeOffset.UtcNow.Subtract(BackfillFallbackWindow);
        var normalizedFallback = ClampBackfillStartDate(fallback, productOwnerId, "Fallback");
        _logger.LogWarning(
            "Using fallback backfill start date for ProductOwner {ProductOwnerId}: {StartDate}. Reason: {Reason}",
            productOwnerId,
            normalizedFallback,
            reason);
        return normalizedFallback;
    }

    private DateTimeOffset ClampBackfillStartDate(DateTimeOffset dateTime, int productOwnerId, string source)
    {
        var utc = dateTime.ToUniversalTime();
        if (utc < BackfillStartMinimumUtc)
        {
            _logger.LogWarning(
                "Inferred backfill start date {StartDate} from {Source} for ProductOwner {ProductOwnerId} is earlier than minimum {Minimum}. Clamping.",
                utc,
                source,
                productOwnerId,
                BackfillStartMinimumUtc);
            return BackfillStartMinimumUtc;
        }

        return utc;
    }


    private async Task<RevisionIngestionWatermarkEntity> GetOrCreateWatermarkAsync(
        PoToolDbContext context,
        int productOwnerId,
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
            await context.SaveChangesAsync(cancellationToken);
        }

        return watermark;
    }

    private async Task<HashSet<int>> ResolveAllowedWorkItemIdsForProductOwnerAsync(
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
            .Select(product => product.BacklogRootWorkItemId)
            .Where(rootId => rootId > 0)
            .Distinct()
            .ToArray();

        if (rootWorkItemIds.Length == 0)
        {
            throw new InvalidOperationException($"ProductOwner {productOwnerId} has no valid backlog root work item IDs.");
        }

        var workItems = await tfsClient.GetWorkItemsByRootIdsAsync(
            rootWorkItemIds,
            null,
            null,
            cancellationToken);

        var descendantWorkItems = WorkItemHierarchyHelper.FilterDescendants(rootWorkItemIds, workItems);
        var allowedWorkItemIds = descendantWorkItems
            .Select(workItem => workItem.TfsId)
            .ToHashSet();

        if (allowedWorkItemIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"ProductOwner {productOwnerId} has no work items under configured backlog roots.");
        }

        return allowedWorkItemIds;
    }

    private void LogScopedRevisionFieldSnapshot(int pageIndex, IReadOnlyCollection<WorkItemRevision> revisions)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        foreach (var revision in revisions)
        {
            _logger.LogInformation(
                "Scoped revision field snapshot. PageIndex={PageIndex} WorkItemId={WorkItemId} RevisionNumber={RevisionNumber} Fields={Fields}",
                pageIndex,
                revision.WorkItemId,
                revision.RevisionNumber,
                new
                {
                    revision.WorkItemType,
                    revision.Title,
                    revision.State,
                    revision.Reason,
                    revision.IterationPath,
                    revision.AreaPath,
                    revision.CreatedDate,
                    revision.ChangedDate,
                    revision.ClosedDate,
                    revision.Effort,
                    revision.Tags,
                    revision.Severity,
                    revision.ChangedBy
                });
        }
    }

    private async Task<int> PersistRevisionsAsync(
        PoToolDbContext context,
        IReadOnlyList<WorkItemRevision> revisions,
        PersistMetrics? metrics,
        CancellationToken cancellationToken)
    {
        if (revisions.Count == 0)
        {
            return 0;
        }

        var persistedCount = 0;
        var persistStart = Stopwatch.GetTimestamp();
        var options = _persistenceOptions.CurrentValue;
        var autoDetectChangesEnabled = context.ChangeTracker.AutoDetectChangesEnabled;
        IDbContextTransaction? transaction = null;
        var transactionUsed = false;
        List<int>? workItemIds = null;
        HashSet<(int WorkItemId, int RevisionNumber)>? existingKeys = null;
        List<RevisionHeaderEntity>? headers = null;

        try
        {
            if (context.Database.IsRelational())
            {
                transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                transactionUsed = true;
            }

            if (options.Enabled)
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;
            }

            workItemIds = revisions.Select(revision => revision.WorkItemId).Distinct().ToList();
            existingKeys = new HashSet<(int WorkItemId, int RevisionNumber)>();

            if (workItemIds.Count > 0)
            {
                var existingRevisions = await context.RevisionHeaders.AsNoTracking()
                    .Where(header => workItemIds.Contains(header.WorkItemId))
                    .Select(header => new { header.WorkItemId, header.RevisionNumber })
                    .ToListAsync(cancellationToken);

                foreach (var existing in existingRevisions)
                {
                    existingKeys.Add((existing.WorkItemId, existing.RevisionNumber));
                }
            }

            headers = new List<RevisionHeaderEntity>(revisions.Count);

            foreach (var revision in revisions)
            {
                if (existingKeys.Contains((revision.WorkItemId, revision.RevisionNumber)))
                {
                    continue;
                }

                var header = new RevisionHeaderEntity
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
                    Tags = revision.Tags,
                    Severity = revision.Severity,
                    ChangedBy = revision.ChangedBy,
                    IngestedAt = DateTimeOffset.UtcNow
                };

                headers.Add(header);
                metrics?.IncrementRevisionHeader();

                persistedCount++;
            }

            if (headers != null && headers.Count > 0)
            {
                context.RevisionHeaders.AddRange(headers);
            }

            if (options.Enabled)
            {
                context.ChangeTracker.DetectChanges();
            }

            var saveStart = Stopwatch.GetTimestamp();
            await context.SaveChangesAsync(cancellationToken);

            if (metrics != null)
            {
                metrics.SaveChangesDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(saveStart);
            }

            if (transaction != null)
            {
                var commitStart = Stopwatch.GetTimestamp();
                await transaction.CommitAsync(cancellationToken);
                if (metrics != null)
                {
                    metrics.CommitDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(commitStart);
                }
            }
            else if (metrics != null)
            {
                metrics.CommitDurationMs = null;
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
        catch
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

            if (metrics != null)
            {
                metrics.PersistDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(persistStart);
                metrics.TransactionUsed = transactionUsed;
            }

            headers?.Clear();
            workItemIds?.Clear();
            existingKeys?.Clear();
        }

        return persistedCount;
    }

    private static string? GetConnectionMode(PoToolDbContext context)
    {
        var connectionString = context.Database.GetDbConnection().ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (builder.TryGetValue("Mode", out var mode))
            {
                return mode?.ToString();
            }

            if (builder.TryGetValue("ApplicationIntent", out var intent))
            {
                return intent?.ToString();
            }
        }
        catch (ArgumentException)
        {
            return null;
        }

        return null;
    }

    private string? ProtectContinuationToken(string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return null;
        }

        return _tokenProtector.Protect(continuationToken);
    }

    private string? UnprotectContinuationToken(string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return null;
        }

        try
        {
            return _tokenProtector.Unprotect(continuationToken);
        }
        catch (CryptographicException)
        {
            _logger.LogWarning(
                "Failed to unprotect continuation token; ignoring stored token and restarting pagination from the last sync start.");
            return null;
        }
    }

    private void LogEarlyTermination(
        string reason,
        int pageIndex,
        string? continuationTokenHash,
        int totalPersistedSoFar)
    {
        if (!_logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        _logger.LogWarning(
            "Reporting revisions pagination terminated early. Reason={Reason} PageIndex={PageIndex} ContinuationTokenHash={ContinuationTokenHash} TotalPersistedSoFar={TotalPersistedSoFar}",
            reason,
            pageIndex,
            continuationTokenHash,
            totalPersistedSoFar);
    }

    private sealed class ReportingRevisionsPageTracker
    {
        private readonly HashSet<string> _seenTokenHashes = new(StringComparer.Ordinal);
        private string? _previousToken;

        public int PageIndex { get; private set; }

        public int NextPageIndex()
        {
            PageIndex++;
            return PageIndex;
        }

        public bool IsTokenAdvanced(string? newToken)
        {
            return !string.Equals(newToken, _previousToken, StringComparison.Ordinal);
        }

        public TokenTrackingSnapshot TrackToken(string? newToken)
        {
            var tokenHash = HashContinuationToken(newToken);
            var seenTokenRepeated = tokenHash != null && _seenTokenHashes.Contains(tokenHash);
            if (tokenHash != null)
            {
                _seenTokenHashes.Add(tokenHash);
            }

            return new TokenTrackingSnapshot(tokenHash, seenTokenRepeated);
        }

        public void CommitToken(string? newToken)
        {
            _previousToken = newToken;
        }

        private static string? HashContinuationToken(string? continuationToken)
        {
            if (string.IsNullOrWhiteSpace(continuationToken))
            {
                return null;
            }

            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(continuationToken));
            var hashHex = Convert.ToHexString(hashBytes);
            // SHA256 hashes are always 32 bytes; hex encoding uses two characters per byte (64 chars total).
            // 12 characters keeps logs compact while still offering low collision risk for diagnostics.
            return hashHex[..ContinuationTokenHashLength];
        }
    }

    private sealed record TokenTrackingSnapshot(string? TokenHash, bool SeenTokenRepeated);
    
    private sealed class PersistMetrics
    {
        public int RevisionHeaderCount { get; private set; }
        public int FieldDeltaCount { get; private set; }
        public int RelationDeltaCount { get; private set; }
        public long SaveChangesDurationMs { get; set; }
        public long? CommitDurationMs { get; set; }
        public long PersistDurationMs { get; set; }
        public bool TransactionUsed { get; set; }

        public void IncrementRevisionHeader() => RevisionHeaderCount++;
        public void IncrementFieldDelta() => FieldDeltaCount++;
        public void IncrementRelationDelta() => RelationDeltaCount++;
    }
}

public enum RevisionIngestionRunOutcome
{
    CompletedNormally = 0,
    CompletedWithPaginationAnomaly = 1
}

/// <summary>
/// Result of a revision ingestion operation.
/// </summary>
public record RevisionIngestionResult
{
    public RevisionIngestionRunOutcome RunOutcome { get; init; } = RevisionIngestionRunOutcome.CompletedNormally;
    public bool Success { get; init; }
    public bool IsAlreadyRunning { get; init; }
    public bool WasCancelled { get; init; }
    public bool WasTerminatedEarly { get; init; }
    public int RevisionsIngested { get; init; }
    public int PagesProcessed { get; init; }
    public string? ErrorMessage { get; init; }
    public ReportingRevisionsTerminationReason? TerminationReason { get; init; }
    public string? TerminationMessage { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Progress update during revision ingestion.
/// </summary>
public record RevisionIngestionProgress
{
    public required string Stage { get; init; }
    public int PercentComplete { get; init; }
    public int RevisionsProcessed { get; init; }
    public int CurrentPage { get; init; }
    public bool IsComplete { get; init; }
}
