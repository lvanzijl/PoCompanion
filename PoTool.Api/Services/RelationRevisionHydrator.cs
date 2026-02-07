using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;

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

    // Concurrency control for relation fetching
    private const int MaxConcurrentFetches = 4;
    private readonly SemaphoreSlim _concurrencySemaphore = new(MaxConcurrentFetches, MaxConcurrentFetches);

    // Cache of last hydrated revision per work item
    private readonly ConcurrentDictionary<int, int> _lastHydratedRevision = new();

    public RelationRevisionHydrator(
        IServiceScopeFactory scopeFactory,
        ILogger<RelationRevisionHydrator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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
            int totalRevisionsHydrated = 0;
            int workItemsProcessed = 0;

            // Process work items with concurrency control
            var tasks = distinctWorkItemIds.Select(async workItemId =>
            {
                await _concurrencySemaphore.WaitAsync(cancellationToken);
                try
                {
                    var revisionsHydrated = await HydrateWorkItemAsync(workItemId, cancellationToken);
                    Interlocked.Add(ref totalRevisionsHydrated, revisionsHydrated);
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

    private async Task<int> HydrateWorkItemAsync(int workItemId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var revisionClient = scope.ServiceProvider.GetRequiredService<IRevisionTfsClient>();

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
                return 0;
            }

            if (lastRevisionInDb == 0)
            {
                // No revisions in database yet for this work item - skip hydration
                _logger.LogDebug(
                    "No revisions found in database for work item {WorkItemId}, skipping hydration",
                    workItemId);
                return 0;
            }

            _logger.LogDebug(
                "Hydrating relations for work item {WorkItemId} up to revision {Revision}",
                workItemId, lastRevisionInDb);

            // Fetch all revisions with relations from TFS
            var revisions = await revisionClient.GetWorkItemRevisionsAsync(workItemId, cancellationToken);

            int revisionsHydrated = 0;

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
                            ChangeType = (PoTool.Api.Persistence.Entities.RelationChangeType)(int)delta.ChangeType,
                            RelationType = delta.RelationType,
                            TargetWorkItemId = delta.TargetWorkItemId
                        });
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

            return revisionsHydrated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate relations for work item {WorkItemId}", workItemId);
            throw;
        }
    }
}
