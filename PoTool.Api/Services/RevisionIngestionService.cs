using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
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

    // Concurrency control: one ingestion per ProductOwner
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _ingestionLocks = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeIngestions = new();

    public RevisionIngestionService(
        IServiceScopeFactory scopeFactory,
        ILogger<RevisionIngestionService> logger,
        RevisionIngestionDiagnostics diagnostics,
        TfsRequestThrottler throttler)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _diagnostics = diagnostics;
        _throttler = throttler;
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
            string? continuationToken = watermark.ContinuationToken;
            DateTimeOffset? startDateTime = watermark.IsInitialBackfillComplete 
                ? watermark.LastSyncStartDateTime 
                : null;

            var runStartUtc = DateTimeOffset.UtcNow;
            using var runScope = _diagnostics.StartRun(
                productOwnerId,
                isBackfill: !watermark.IsInitialBackfillComplete,
                startDateTime,
                runStartUtc,
                _throttler.ReadConcurrency,
                _throttler.WriteConcurrency,
                RelationRevisionHydrator.MaxConcurrentFetches,
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
            int pageCount = 0;
            var impactedWorkItemIds = new HashSet<int>();

            while (hasMore && !cts.Token.IsCancellationRequested)
            {
                pageCount++;
                _logger.LogDebug(
                    "Fetching revision page {PageNumber} for ProductOwner {ProductOwnerId}",
                    pageCount, productOwnerId);

                var logPerPageSummary = runContext.IsEnabled && runContext.LogPerPageSummary;
                var pageStartTimestamp = logPerPageSummary ? Stopwatch.GetTimestamp() : 0;
                using var pageScope = _diagnostics.BeginPageScope(runContext, pageCount);
                var pageWorkItemIds = logPerPageSummary ? new HashSet<int>() : null;

                var result = await revisionClient.GetReportingRevisionsAsync(
                    startDateTime,
                    continuationToken,
                    expandMode: ReportingExpandMode.None,
                    cts.Token);

                var persistMetrics = runContext.IsEnabled && (runContext.LogEfSaveChangesDetails || logPerPageSummary)
                    ? new PersistMetrics()
                    : null;
                var persistStartTimestamp = logPerPageSummary ? Stopwatch.GetTimestamp() : 0;

                // Process and persist revisions
                var persistedCount = await PersistRevisionsAsync(context, result.Revisions, persistMetrics, cts.Token);
                totalRevisions += persistedCount;

                var persistDurationMs = logPerPageSummary
                    ? RevisionIngestionDiagnostics.GetElapsedMilliseconds(persistStartTimestamp)
                    : 0;

                // Track impacted work item IDs for relation hydration
                foreach (var revision in result.Revisions)
                {
                    impactedWorkItemIds.Add(revision.WorkItemId);
                    pageWorkItemIds?.Add(revision.WorkItemId);
                }

                _logger.LogInformation(
                    "Persisted {Count} revisions (page {Page}) for ProductOwner {ProductOwnerId}",
                    persistedCount, pageCount, productOwnerId);

                if (persistMetrics != null && runContext.LogEfSaveChangesDetails)
                {
                    _diagnostics.LogSaveChangesDetails(
                        runContext,
                        persistMetrics.SaveChangesDurationMs,
                        persistMetrics.RevisionHeaderCount,
                        persistMetrics.FieldDeltaCount,
                        persistMetrics.RelationDeltaCount);
                }

                // Update continuation token
                continuationToken = result.ContinuationToken;
                watermark.ContinuationToken = continuationToken;
                await context.SaveChangesAsync(cts.Token);

                progressCallback?.Invoke(new RevisionIngestionProgress
                {
                    Stage = watermark.IsInitialBackfillComplete ? "Incremental Sync" : "Initial Backfill",
                    PercentComplete = result.IsComplete ? 100 : 0, // Keep at 0% until the final page completes because total pages are unknown
                    RevisionsProcessed = totalRevisions,
                    CurrentPage = pageCount
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
                        result.Revisions.Count,
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

                hasMore = !result.IsComplete;
            }

            // Hydrate relations for impacted work items
            if (impactedWorkItemIds.Count > 0)
            {
                _logger.LogInformation(
                    "Hydrating relations for {Count} impacted work items",
                    impactedWorkItemIds.Count);

                progressCallback?.Invoke(new RevisionIngestionProgress
                {
                    Stage = "Hydrating Relations",
                    PercentComplete = 90,
                    RevisionsProcessed = totalRevisions
                });

                // Resolve IRelationRevisionHydrator from scope
                using var hydrationScope = _scopeFactory.CreateScope();
                var relationHydrator = hydrationScope.ServiceProvider.GetRequiredService<IRelationRevisionHydrator>();

                var hydrationResult = await relationHydrator.HydrateAsync(
                    impactedWorkItemIds,
                    cts.Token);

                if (!hydrationResult.Success)
                {
                    _logger.LogWarning(
                        "Relation hydration completed with errors: {ErrorMessage}",
                        hydrationResult.ErrorMessage);
                }
                else
                {
                    _logger.LogInformation(
                        "Successfully hydrated relations for {WorkItems} work items ({Revisions} revisions)",
                        hydrationResult.WorkItemsProcessed,
                        hydrationResult.RevisionsHydrated);
                }
            }

            // Mark ingestion complete
            watermark.LastIngestionCompletedAt = DateTimeOffset.UtcNow;
            watermark.LastIngestionRevisionCount = totalRevisions;
            watermark.ContinuationToken = null; // Clear token on successful completion

            if (!watermark.IsInitialBackfillComplete)
            {
                watermark.IsInitialBackfillComplete = true;
                _logger.LogInformation("Initial backfill completed for ProductOwner {ProductOwnerId}", productOwnerId);
            }

            // Update watermark for next incremental sync
            watermark.LastSyncStartDateTime = syncStartTime;
            await context.SaveChangesAsync(cts.Token);

            progressCallback?.Invoke(new RevisionIngestionProgress
            {
                Stage = "Complete",
                PercentComplete = 100,
                RevisionsProcessed = totalRevisions,
                IsComplete = true
            });

            _logger.LogInformation(
                "Revision ingestion completed for ProductOwner {ProductOwnerId}: {TotalRevisions} revisions in {Pages} pages",
                productOwnerId, totalRevisions, pageCount);

            return new RevisionIngestionResult
            {
                Success = true,
                RevisionsIngested = totalRevisions,
                PagesProcessed = pageCount,
                Message = $"Successfully ingested {totalRevisions} revisions"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Revision ingestion cancelled for ProductOwner {ProductOwnerId}", productOwnerId);
            return new RevisionIngestionResult
            {
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

        Task SaveChangesAsync()
        {
            if (metrics == null)
            {
                return context.SaveChangesAsync(cancellationToken);
            }

            var saveStart = Stopwatch.GetTimestamp();
            return SaveChangesWithMetricsAsync(saveStart);
        }

        async Task SaveChangesWithMetricsAsync(long saveStart)
        {
            await context.SaveChangesAsync(cancellationToken);
            metrics.SaveChangesDurationMs += RevisionIngestionDiagnostics.GetElapsedMilliseconds(saveStart);
        }

        foreach (var revision in revisions)
        {
            // Check if revision already exists (idempotency)
            var existing = await context.RevisionHeaders
                .FirstOrDefaultAsync(
                    h => h.WorkItemId == revision.WorkItemId && h.RevisionNumber == revision.RevisionNumber,
                    cancellationToken);

            if (existing != null)
            {
                // Already ingested, skip
                continue;
            }

            // Create revision header
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

            context.RevisionHeaders.Add(header);
            metrics?.IncrementRevisionHeader();
            
            // Save immediately to get header ID needed for related entities
            await SaveChangesAsync();

            // Add field deltas
            if (revision.FieldDeltas != null)
            {
                foreach (var (fieldName, delta) in revision.FieldDeltas)
                {
                    context.RevisionFieldDeltas.Add(new RevisionFieldDeltaEntity
                    {
                        RevisionHeaderId = header.Id,
                        FieldName = delta.FieldName,
                        OldValue = delta.OldValue,
                        NewValue = delta.NewValue
                    });
                    metrics?.IncrementFieldDelta();
                }
            }

            // Add relation deltas
            if (revision.RelationDeltas != null)
            {
                foreach (var delta in revision.RelationDeltas)
                {
                    context.RevisionRelationDeltas.Add(new RevisionRelationDeltaEntity
                    {
                        RevisionHeaderId = header.Id,
                        ChangeType = (PoTool.Api.Persistence.Entities.RelationChangeType)(int)delta.ChangeType,
                        RelationType = delta.RelationType,
                        TargetWorkItemId = delta.TargetWorkItemId
                    });
                    metrics?.IncrementRelationDelta();
                }
            }

            persistedCount++;
            
            // Batch save: commit every BatchSaveSize revisions to reduce database round-trips
            // while maintaining checkpoint progress during large backfills
            if (persistedCount % BatchSaveSize == 0)
            {
                await SaveChangesAsync();
            }
        }

        // Final save for any remaining changes
        if (context.ChangeTracker.HasChanges())
        {
            await SaveChangesAsync();
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
    
    /// <summary>
    /// Number of revisions to persist before committing a batch save.
    /// </summary>
    private const int BatchSaveSize = 50;

    private sealed class PersistMetrics
    {
        public int RevisionHeaderCount { get; private set; }
        public int FieldDeltaCount { get; private set; }
        public int RelationDeltaCount { get; private set; }
        public long SaveChangesDurationMs { get; set; }

        public void IncrementRevisionHeader() => RevisionHeaderCount++;
        public void IncrementFieldDelta() => FieldDeltaCount++;
        public void IncrementRelationDelta() => RelationDeltaCount++;
    }
}

/// <summary>
/// Result of a revision ingestion operation.
/// </summary>
public record RevisionIngestionResult
{
    public bool Success { get; init; }
    public bool IsAlreadyRunning { get; init; }
    public bool WasCancelled { get; init; }
    public int RevisionsIngested { get; init; }
    public int PagesProcessed { get; init; }
    public string? ErrorMessage { get; init; }
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
