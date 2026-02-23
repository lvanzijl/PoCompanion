using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Services;

/// <summary>
/// Service for resolving work item hierarchies from cached revision data.
/// Resolves Task → PBI → Feature → Epic → Product relationships.
/// </summary>
public class WorkItemResolutionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkItemResolutionService> _logger;

    /// <summary>
    /// Relation type reference name for parent link (Hierarchy-Reverse means "is child of").
    /// </summary>
    private const string ParentRelationType = "System.LinkTypes.Hierarchy-Reverse";

    public WorkItemResolutionService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkItemResolutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Resolves all unresolved work items by computing their hierarchical relationships.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID to scope resolution to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the resolution operation.</returns>
    public async Task<ResolutionResult> ResolveAllAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var cacheState = await context.ProductOwnerCacheStates
            .FirstOrDefaultAsync(state => state.ProductOwnerId == productOwnerId, cancellationToken)
            ?? new ProductOwnerCacheStateEntity
        {
            ProductOwnerId = productOwnerId,
            SyncStatus = CacheSyncStatus.Idle
        };
        // REPLACE_WITH_ACTIVITY_SOURCE: resolve product/epic/feature/sprint lineage from activity events.
        if (cacheState.Id == 0)
        {
            context.ProductOwnerCacheStates.Add(cacheState);
        }
        cacheState.ResolutionAsOfUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return new ResolutionResult
        {
            Success = true,
            ResolvedCount = 0,
            OrphanCount = 0,
            ErrorCount = 0,
            Message = "Resolution skipped: no activity source configured."
        };
    }

    private async Task<Dictionary<int, RevisionHeaderEntity>> GetLatestRevisionsAsync(
        PoToolDbContext context,
        CancellationToken cancellationToken)
    {
        // Get the latest revision for each work item
        var latestRevisions = await context.RevisionHeaders
            .GroupBy(h => h.WorkItemId)
            .Select(g => g.OrderByDescending(h => h.RevisionNumber).First())
            .ToListAsync(cancellationToken);

        return latestRevisions.ToDictionary(r => r.WorkItemId, r => r);
    }

    private async Task<Dictionary<int, int?>> BuildParentChainAsync(
        PoToolDbContext context,
        int productOwnerId,
        List<int> workItemIds,
        CancellationToken cancellationToken)
    {
        var relationEdges = await context.WorkItemRelationshipEdges
            .Where(edge => edge.ProductOwnerId == productOwnerId)
            .Where(edge => workItemIds.Contains(edge.SourceWorkItemId) ||
                           (edge.TargetWorkItemId.HasValue && workItemIds.Contains(edge.TargetWorkItemId.Value)))
            .ToListAsync(cancellationToken);

        // Build current parent for each work item by replaying deltas
        var parentChain = new Dictionary<int, int?>();

        foreach (var edge in relationEdges)
        {
            if (!IsHierarchyRelation(edge.RelationType))
            {
                continue;
            }

            var relationType = edge.RelationType ?? string.Empty;

            if (relationType.Equals(ParentRelationType, StringComparison.OrdinalIgnoreCase))
            {
                parentChain[edge.SourceWorkItemId] = edge.TargetWorkItemId;
                continue;
            }

            if (relationType.Equals("System.LinkTypes.Hierarchy-Forward", StringComparison.OrdinalIgnoreCase)
                && edge.TargetWorkItemId.HasValue)
            {
                parentChain[edge.TargetWorkItemId.Value] = edge.SourceWorkItemId;
            }
        }

        return parentChain;
    }

    private WorkItemResolution ResolveWorkItem(
        int workItemId,
        RevisionHeaderEntity latestRevision,
        Dictionary<int, int?> parentChain,
        HashSet<int> rootWorkItemIds,
        Dictionary<int, int> productByRootId,
        Dictionary<string, int> sprintByPath,
        Dictionary<int, RevisionHeaderEntity> allRevisions)
    {
        var resolution = new WorkItemResolution
        {
            WorkItemId = workItemId,
            Status = ResolutionStatus.Pending
        };

        // Resolve sprint from iteration path
        if (!string.IsNullOrEmpty(latestRevision.IterationPath) && sprintByPath.TryGetValue(latestRevision.IterationPath, out var sprintId))
        {
            resolution.SprintId = sprintId;
        }

        // Walk up the parent chain to find Epic, Feature, and Product
        int? currentId = workItemId;
        var visited = new HashSet<int>();
        const int MaxDepth = 20;
        int depth = 0;

        while (currentId.HasValue && depth < MaxDepth)
        {
            if (!visited.Add(currentId.Value))
            {
                // Circular reference detected
                _logger.LogWarning("Circular parent reference detected for work item {WorkItemId}", workItemId);
                break;
            }

            // Check if we've reached a product root
            if (rootWorkItemIds.Contains(currentId.Value))
            {
                resolution.ProductId = productByRootId[currentId.Value];
                resolution.Status = ResolutionStatus.Resolved;
                return resolution;
            }

            // Get the current item's type and set appropriate IDs
            if (allRevisions.TryGetValue(currentId.Value, out var currentRevision))
            {
                var itemType = currentRevision.WorkItemType;

                if (itemType.Equals("Epic", StringComparison.OrdinalIgnoreCase) && resolution.EpicId == null)
                {
                    resolution.EpicId = currentId.Value;
                }
                else if (itemType.Equals("Feature", StringComparison.OrdinalIgnoreCase) && resolution.FeatureId == null)
                {
                    resolution.FeatureId = currentId.Value;
                }
            }

            // Move up to parent
            if (!parentChain.TryGetValue(currentId.Value, out var parentId) || parentId == null)
            {
                break;
            }

            currentId = parentId;
            depth++;
        }

        // If we couldn't reach a product root, mark as orphan
        resolution.Status = ResolutionStatus.Orphan;
        return resolution;
    }

    private static bool IsHierarchyRelation(string? relationType)
    {
        return !string.IsNullOrWhiteSpace(relationType) &&
               relationType.Contains("Hierarchy", StringComparison.OrdinalIgnoreCase);
    }

    private class WorkItemResolution
    {
        public int WorkItemId { get; set; }
        public int? ProductId { get; set; }
        public int? EpicId { get; set; }
        public int? FeatureId { get; set; }
        public int? SprintId { get; set; }
        public ResolutionStatus Status { get; set; }
    }
}

/// <summary>
/// Result of a work item resolution operation.
/// </summary>
public record ResolutionResult
{
    /// <summary>
    /// Whether the operation completed without errors.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Number of work items successfully resolved to a product.
    /// </summary>
    public int ResolvedCount { get; init; }

    /// <summary>
    /// Number of work items that could not be resolved to a product (orphans).
    /// </summary>
    public int OrphanCount { get; init; }

    /// <summary>
    /// Number of work items that encountered errors during resolution.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Human-readable summary of the resolution operation.
    /// Format: "Resolved {count} items, {orphan} orphans, {error} errors"
    /// </summary>
    public required string Message { get; init; }
}
