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

        _logger.LogInformation("Starting work item resolution for ProductOwner {ProductOwnerId}", productOwnerId);

        // Get all products for this ProductOwner
        var products = await context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            _logger.LogWarning("No products found for ProductOwner {ProductOwnerId}", productOwnerId);
            return new ResolutionResult { Success = true, Message = "No products to resolve" };
        }

        // Get all sprints for teams linked to these products
        var productIds = products.Select(p => p.Id).ToList();
        var sprints = await context.Sprints
            .Include(s => s.Team)
            .ThenInclude(t => t.ProductTeamLinks)
            .Where(s => s.Team.ProductTeamLinks.Any(ptl => productIds.Contains(ptl.ProductId)))
            .ToListAsync(cancellationToken);

        // Build lookup structures
        var rootWorkItemIds = products.Select(p => p.BacklogRootWorkItemId).ToHashSet();
        var productByRootId = products.ToDictionary(p => p.BacklogRootWorkItemId, p => p.Id);
        var sprintByPath = sprints.ToDictionary(s => s.Path, s => s.Id, StringComparer.OrdinalIgnoreCase);

        // Get the latest revision for each work item
        var latestRevisions = await GetLatestRevisionsAsync(context, cancellationToken);

        // Build parent chain lookup from relation deltas
        var parentChain = await BuildParentChainAsync(context, latestRevisions.Keys.ToList(), cancellationToken);

        var resolvedCount = 0;
        var orphanCount = 0;
        var errorCount = 0;

        foreach (var (workItemId, latestRevision) in latestRevisions)
        {
            try
            {
                var resolution = ResolveWorkItem(
                    workItemId,
                    latestRevision,
                    parentChain,
                    rootWorkItemIds,
                    productByRootId,
                    sprintByPath,
                    latestRevisions);

                // Upsert resolution
                var existing = await context.ResolvedWorkItems
                    .FirstOrDefaultAsync(r => r.WorkItemId == workItemId, cancellationToken);

                if (existing != null)
                {
                    // Update existing
                    existing.WorkItemType = latestRevision.WorkItemType;
                    existing.ResolvedProductId = resolution.ProductId;
                    existing.ResolvedEpicId = resolution.EpicId;
                    existing.ResolvedFeatureId = resolution.FeatureId;
                    existing.ResolvedSprintId = resolution.SprintId;
                    existing.ResolutionStatus = resolution.Status;
                    existing.LastResolvedAt = DateTimeOffset.UtcNow;
                    existing.ResolvedAtRevision = latestRevision.RevisionNumber;
                }
                else
                {
                    // Create new
                    context.ResolvedWorkItems.Add(new ResolvedWorkItemEntity
                    {
                        WorkItemId = workItemId,
                        WorkItemType = latestRevision.WorkItemType,
                        ResolvedProductId = resolution.ProductId,
                        ResolvedEpicId = resolution.EpicId,
                        ResolvedFeatureId = resolution.FeatureId,
                        ResolvedSprintId = resolution.SprintId,
                        ResolutionStatus = resolution.Status,
                        LastResolvedAt = DateTimeOffset.UtcNow,
                        ResolvedAtRevision = latestRevision.RevisionNumber
                    });
                }

                if (resolution.Status == ResolutionStatus.Resolved)
                {
                    resolvedCount++;
                }
                else if (resolution.Status == ResolutionStatus.Orphan)
                {
                    orphanCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve work item {WorkItemId}", workItemId);
                errorCount++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Resolution complete for ProductOwner {ProductOwnerId}: {Resolved} resolved, {Orphan} orphans, {Error} errors",
            productOwnerId, resolvedCount, orphanCount, errorCount);

        return new ResolutionResult
        {
            Success = true,
            ResolvedCount = resolvedCount,
            OrphanCount = orphanCount,
            ErrorCount = errorCount,
            Message = $"Resolved {resolvedCount} items, {orphanCount} orphans, {errorCount} errors"
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
        List<int> workItemIds,
        CancellationToken cancellationToken)
    {
        // Get all relation deltas for parent links
        var relationDeltas = await context.RevisionRelationDeltas
            .Where(rd => workItemIds.Contains(rd.RevisionHeader.WorkItemId))
            .Where(rd => rd.RelationType == ParentRelationType)
            .Include(rd => rd.RevisionHeader)
            .OrderBy(rd => rd.RevisionHeader.RevisionNumber)
            .ToListAsync(cancellationToken);

        // Build current parent for each work item by replaying deltas
        var parentChain = new Dictionary<int, int?>();

        foreach (var delta in relationDeltas)
        {
            var workItemId = delta.RevisionHeader.WorkItemId;

            if (delta.ChangeType == RelationChangeType.Added)
            {
                parentChain[workItemId] = delta.TargetWorkItemId;
            }
            else if (delta.ChangeType == RelationChangeType.Removed)
            {
                if (parentChain.TryGetValue(workItemId, out var currentParent) && currentParent == delta.TargetWorkItemId)
                {
                    parentChain[workItemId] = null;
                }
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
