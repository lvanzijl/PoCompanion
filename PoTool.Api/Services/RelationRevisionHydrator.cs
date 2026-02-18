using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Diagnostics;

namespace PoTool.Api.Services;

/// <summary>
/// Service for hydrating relation deltas for work items.
/// Relations are NOT available from the reporting revisions endpoint,
/// so they must be fetched separately via the per-item revisions endpoint.
/// </summary>
public class RelationRevisionHydrator : IRelationRevisionHydrator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RelationRevisionHydrator> _logger;
    private readonly RevisionIngestionDiagnostics _diagnostics;

    // Concurrency control for relation fetching
    private const int MaxConcurrentFetches = 4;
    private readonly SemaphoreSlim _concurrencySemaphore = new(MaxConcurrentFetches, MaxConcurrentFetches);

    public static int HydrationConcurrency => MaxConcurrentFetches;

    // Cache of last hydrated revision per work item
    private readonly ConcurrentDictionary<int, int> _lastHydratedRevision = new();

    public RelationRevisionHydrator(
        IServiceScopeFactory scopeFactory,
        ILogger<RelationRevisionHydrator> logger,
        RevisionIngestionDiagnostics diagnostics)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _diagnostics = diagnostics;
    }

    /// <inheritdoc />
    public async Task<RelationHydrationResult> HydrateAsync(
        IEnumerable<int> workItemIds,
        CancellationToken cancellationToken = default)
    {
        var distinctWorkItemIds = workItemIds.Distinct().ToList();

        if (distinctWorkItemIds.Count == 0)
        {
            _logger.LogDebug("No work items to hydrate");
            return new RelationHydrationResult
            {
                Success = true,
                WorkItemsProcessed = 0,
                RevisionsHydrated = 0
            };
        }

            _logger.LogInformation(
                "Starting relation hydration for {Count} work items",
                distinctWorkItemIds.Count);

            try
            {
                var hasDiagnostics = _diagnostics.TryGetCurrentRun(out var runContext);
                var collectMetrics = hasDiagnostics && runContext.IsEnabled;
                var hydrationStart = collectMetrics ? Stopwatch.GetTimestamp() : 0;
                var totalRelationDeltas = 0;
                var totalCalls = 0;
                long totalCallDurationMs = 0;
                var totalSkipped = 0;
                int totalRevisionsHydrated = 0;
                int workItemsProcessed = 0;

                // Process work items with concurrency control
                var tasks = distinctWorkItemIds.Select(async workItemId =>
                {
                    await _concurrencySemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var result = await HydrateWorkItemAsync(workItemId, runContext, cancellationToken);
                        Interlocked.Add(ref totalRevisionsHydrated, result.RevisionsHydrated);
                        if (collectMetrics)
                        {
                            if (result.CallMade)
                            {
                                Interlocked.Increment(ref totalCalls);
                                Interlocked.Add(ref totalCallDurationMs, result.CallDurationMs);
                            }

                            if (result.SkippedDueToCache)
                            {
                                Interlocked.Increment(ref totalSkipped);
                            }

                            Interlocked.Add(ref totalRelationDeltas, result.RelationDeltaCount);
                        }
                        Interlocked.Increment(ref workItemsProcessed);
                    }
                    finally
                    {
                    _concurrencySemaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                "Relation hydration completed: {WorkItems} work items, {Revisions} revisions hydrated",
                workItemsProcessed, totalRevisionsHydrated);

            if (collectMetrics)
            {
                var totalDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(hydrationStart);
                var avgCallMs = totalCalls > 0 ? totalCallDurationMs / (double)totalCalls : 0;
                _diagnostics.LogRelationHydrationSummary(
                    runContext,
                    distinctWorkItemIds.Count,
                    totalCalls,
                    totalDurationMs,
                    avgCallMs,
                    totalRelationDeltas,
                    totalSkipped);
            }

            return new RelationHydrationResult
            {
                Success = true,
                WorkItemsProcessed = workItemsProcessed,
                RevisionsHydrated = totalRevisionsHydrated
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Relation hydration failed");
            return new RelationHydrationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<HydrationWorkItemResult> HydrateWorkItemAsync(
        int workItemId,
        RevisionIngestionRunContext runContext,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var revisionSourceSelector = scope.ServiceProvider.GetRequiredService<IWorkItemRevisionSourceSelector>();
        var revisionSource = await revisionSourceSelector.GetSourceAsync(cancellationToken);
        var logPerWorkItem = runContext.IsEnabled && runContext.LogPerWorkItemHydration;
        var captureCallTiming = runContext.IsEnabled;
        var perItemStart = logPerWorkItem ? Stopwatch.GetTimestamp() : 0;
        var relationDeltaCount = 0;
        var callDurationMs = 0L;

        try
        {
            // Get the last revision we already have for this work item
            var lastRevisionInDb = await context.RevisionHeaders
                .Where(h => h.WorkItemId == workItemId)
                .OrderByDescending(h => h.RevisionNumber)
                .Select(h => h.RevisionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            // Check if we've already hydrated this work item up to the latest revision
            if (_lastHydratedRevision.TryGetValue(workItemId, out var lastHydrated) && lastHydrated >= lastRevisionInDb)
            {
                _logger.LogDebug(
                    "Work item {WorkItemId} already hydrated up to revision {Revision}",
                    workItemId, lastHydrated);
                LogPerWorkItem(runContext, logPerWorkItem, workItemId, 0, false, perItemStart, 0);
                return new HydrationWorkItemResult(0, 0, 0, callDurationMs, true, false);
            }

            if (lastRevisionInDb == 0)
            {
                // No revisions in database yet for this work item - skip hydration
                _logger.LogDebug(
                    "No revisions found in database for work item {WorkItemId}, skipping hydration",
                    workItemId);
                LogPerWorkItem(runContext, logPerWorkItem, workItemId, 0, false, perItemStart, 0);
                return new HydrationWorkItemResult(0, 0, 0, callDurationMs, false, false);
            }

            _logger.LogDebug(
                "Hydrating relations for work item {WorkItemId} up to revision {Revision}",
                workItemId, lastRevisionInDb);

            // Fetch all revisions with relations from TFS
            var callStart = captureCallTiming ? Stopwatch.GetTimestamp() : 0;
            var revisions = await revisionSource.GetWorkItemRevisionsAsync(workItemId, cancellationToken);
            if (captureCallTiming)
            {
                callDurationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(callStart);
            }

            int revisionsHydrated = 0;
            var relationsPresent = logPerWorkItem &&
                                   revisions.Any(revision => revision.RelationDeltas != null && revision.RelationDeltas.Count > 0);

            // Update relation deltas for each revision
            foreach (var revision in revisions)
            {
                // Only process revisions we have in our database
                if (revision.RevisionNumber > lastRevisionInDb)
                {
                    continue;
                }

                // Check if we already have relation deltas for this revision
                var existingHeader = await context.RevisionHeaders
                    .Include(h => h.RelationDeltas)
                    .FirstOrDefaultAsync(
                        h => h.WorkItemId == workItemId && h.RevisionNumber == revision.RevisionNumber,
                        cancellationToken);

                if (existingHeader == null)
                {
                    _logger.LogWarning(
                        "Revision header not found for work item {WorkItemId} revision {RevisionNumber}",
                        workItemId, revision.RevisionNumber);
                    continue;
                }

                // Only update if we don't already have relation deltas
                // Note: This check provides idempotent hydration - if a revision already has
                // relation deltas, we skip it to avoid duplicate entries.
                // The hydration process is designed to be safely re-runnable.
                if (existingHeader.RelationDeltas.Count > 0)
                {
                    continue;
                }

                // Add relation deltas from the hydrated revision
                if (revision.RelationDeltas != null && revision.RelationDeltas.Count > 0)
                {
                    foreach (var delta in revision.RelationDeltas)
                    {
                        context.RevisionRelationDeltas.Add(new RevisionRelationDeltaEntity
                        {
                            RevisionHeaderId = existingHeader.Id,
                            ChangeType = MapRelationChangeType(delta.ChangeType),
                            RelationType = delta.RelationType,
                            TargetWorkItemId = delta.TargetWorkItemId
                        });
                        relationDeltaCount++;
                    }

                    revisionsHydrated++;
                }
            }

            // Save all changes for this work item
            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            // Update cache
            _lastHydratedRevision[workItemId] = lastRevisionInDb;

            _logger.LogDebug(
                "Hydrated {Count} revisions for work item {WorkItemId}",
                revisionsHydrated, workItemId);

            LogPerWorkItem(runContext, logPerWorkItem, workItemId, revisions.Count, relationsPresent, perItemStart, relationDeltaCount);
            return new HydrationWorkItemResult(revisionsHydrated, revisions.Count, relationDeltaCount, callDurationMs, false, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate relations for work item {WorkItemId}", workItemId);
            throw;
        }
    }

    /// <summary>
    /// Maps RelationChangeType from Core.Contracts to Persistence.Entities.
    /// This ensures explicit, safe conversion between enum types.
    /// </summary>
    private static PoTool.Api.Persistence.Entities.RelationChangeType MapRelationChangeType(
        Core.Contracts.RelationChangeType changeType)
    {
        return changeType switch
        {
            Core.Contracts.RelationChangeType.Added => PoTool.Api.Persistence.Entities.RelationChangeType.Added,
            Core.Contracts.RelationChangeType.Removed => PoTool.Api.Persistence.Entities.RelationChangeType.Removed,
            _ => throw new ArgumentOutOfRangeException(nameof(changeType), changeType, "Unknown RelationChangeType")
        };
    }

    private void LogPerWorkItem(
        RevisionIngestionRunContext runContext,
        bool logPerWorkItem,
        int workItemId,
        int revisionsFetched,
        bool relationsPresent,
        long startTimestamp,
        int relationDeltaCount)
    {
        if (!logPerWorkItem)
        {
            return;
        }

        var durationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(startTimestamp);
        _diagnostics.LogRelationWorkItem(
            runContext,
            workItemId,
            revisionsFetched,
            relationsPresent,
            durationMs,
            relationDeltaCount);
    }

    /// <summary>
    /// Captures per-work-item hydration metrics for diagnostics aggregation.
    /// </summary>
    /// <param name="RevisionsHydrated">Number of revisions with relation deltas persisted.</param>
    /// <param name="RevisionsFetched">Total number of revisions fetched from the per-item API.</param>
    /// <param name="RelationDeltaCount">Total number of relation deltas written.</param>
    /// <param name="CallDurationMs">Elapsed duration in milliseconds for the work item hydration attempt.</param>
    /// <param name="SkippedDueToCache">True when hydration was skipped due to cached completion.</param>
    /// <param name="CallMade">True when the per-item revisions API was called.</param>
    private readonly record struct HydrationWorkItemResult(
        int RevisionsHydrated,
        int RevisionsFetched,
        int RelationDeltaCount,
        long CallDurationMs,
        bool SkippedDueToCache,
        bool CallMade);
}
