using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Services;

public class WorkItemResolutionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkItemResolutionService> _logger;

    public WorkItemResolutionService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkItemResolutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public virtual async Task<ResolutionResult> ResolveAllAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var products = await context.Products
            .Include(p => p.BacklogRoots)
            .Where(p => p.ProductOwnerId == productOwnerId)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            _logger.LogWarning("No products found for ProductOwner {ProductOwnerId}", productOwnerId);
            return new ResolutionResult
            {
                Success = true,
                ResolvedCount = 0,
                OrphanCount = 0,
                ErrorCount = 0,
                Message = "No products configured."
            };
        }

        var workItems = await context.WorkItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (workItems.Count == 0)
        {
            _logger.LogWarning("No work items found for resolution");
            return new ResolutionResult
            {
                Success = true,
                ResolvedCount = 0,
                OrphanCount = 0,
                ErrorCount = 0,
                Message = "No work items available."
            };
        }

        var workItemsByTfsId = workItems.ToDictionary(w => w.TfsId, w => w);

        // Build sprint lookup by iteration path
        var sprints = await context.Sprints
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var sprintsByPath = sprints
            .GroupBy(s => s.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Build children lookup
        var childrenByParent = workItems
            .Where(w => w.ParentTfsId.HasValue)
            .GroupBy(w => w.ParentTfsId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(w => w.TfsId).ToList());

        var resolvedEntities = new List<ResolvedWorkItemEntity>();
        var now = DateTimeOffset.UtcNow;

        foreach (var product in products)
        {
            if (product.BacklogRoots.Count == 0)
                continue;

            // Walk the hierarchy from all backlog roots
            var visited = new HashSet<int>();
            var stack = new Stack<int>();
            foreach (var rootId in product.BacklogRoots.Select(r => r.WorkItemTfsId))
            {
                stack.Push(rootId);
            }

            while (stack.Count > 0)
            {
                var tfsId = stack.Pop();
                if (!visited.Add(tfsId))
                    continue;

                if (!workItemsByTfsId.TryGetValue(tfsId, out var wi))
                    continue;

                // Resolve ancestry: walk up the parent chain to find Epic and Feature
                var (epicId, featureId) = ResolveAncestry(tfsId, workItemsByTfsId);

                // Resolve sprint from iteration path
                int? sprintId = null;
                if (!string.IsNullOrWhiteSpace(wi.IterationPath) &&
                    sprintsByPath.TryGetValue(wi.IterationPath, out var sprint))
                {
                    sprintId = sprint.Id;
                }

                resolvedEntities.Add(new ResolvedWorkItemEntity
                {
                    WorkItemId = tfsId,
                    WorkItemType = wi.Type,
                    ResolvedProductId = product.Id,
                    ResolvedEpicId = epicId,
                    ResolvedFeatureId = featureId,
                    ResolvedSprintId = sprintId,
                    ResolutionStatus = ResolutionStatus.Resolved,
                    LastResolvedAt = now,
                    ResolvedAtRevision = 0
                });

                // Push children
                if (childrenByParent.TryGetValue(tfsId, out var children))
                {
                    foreach (var childId in children)
                    {
                        stack.Push(childId);
                    }
                }
            }
        }

        // Remove existing resolved items for this PO's products and replace
        var productIds = products.Select(p => p.Id).ToList();
        await context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId != null && productIds.Contains(r.ResolvedProductId.Value))
            .ExecuteDeleteAsync(cancellationToken);

        if (resolvedEntities.Count > 0)
        {
            await context.ResolvedWorkItems.AddRangeAsync(resolvedEntities, cancellationToken);
        }

        // Update cache state
        var cacheState = await context.ProductOwnerCacheStates
            .OrderBy(state => state.Id)
            .FirstOrDefaultAsync(state => state.ProductOwnerId == productOwnerId, cancellationToken);

        if (cacheState == null)
        {
            cacheState = new ProductOwnerCacheStateEntity
            {
                ProductOwnerId = productOwnerId,
                SyncStatus = CacheSyncStatus.Idle
            };
            context.ProductOwnerCacheStates.Add(cacheState);
        }

        cacheState.ResolutionAsOfUtc = now;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Resolved {ResolvedCount} work items for ProductOwner {ProductOwnerId} across {ProductCount} products",
            resolvedEntities.Count, productOwnerId, products.Count);

        return new ResolutionResult
        {
            Success = true,
            ResolvedCount = resolvedEntities.Count,
            OrphanCount = 0,
            ErrorCount = 0,
            Message = $"Resolved {resolvedEntities.Count} work items across {products.Count} products."
        };
    }

    /// <summary>
    /// Walks up the parent chain from a work item to find its Epic and Feature ancestors.
    /// </summary>
    internal static (int? EpicId, int? FeatureId) ResolveAncestry(
        int tfsId,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId)
    {
        int? epicId = null;
        int? featureId = null;

        if (!workItemsByTfsId.TryGetValue(tfsId, out var current))
            return (epicId, featureId);

        // If the item itself is an Epic or Feature, record it
        if (string.Equals(current.Type, WorkItemType.Epic, StringComparison.OrdinalIgnoreCase))
        {
            epicId = tfsId;
        }
        else if (string.Equals(current.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase))
        {
            featureId = tfsId;
        }

        // Walk up ancestors (max 50 to prevent infinite loops)
        var visited = new HashSet<int> { tfsId };
        var currentId = current.ParentTfsId;
        var depth = 0;

        while (currentId.HasValue && depth < 50)
        {
            if (!visited.Add(currentId.Value))
                break;

            if (!workItemsByTfsId.TryGetValue(currentId.Value, out var ancestor))
                break;

            if (string.Equals(ancestor.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase) && featureId == null)
            {
                featureId = currentId.Value;
            }
            else if (string.Equals(ancestor.Type, WorkItemType.Epic, StringComparison.OrdinalIgnoreCase) && epicId == null)
            {
                epicId = currentId.Value;
            }

            if (epicId.HasValue && featureId.HasValue)
                break;

            currentId = ancestor.ParentTfsId;
            depth++;
        }

        return (epicId, featureId);
    }
}

public record ResolutionResult
{
    public bool Success { get; init; }
    public int ResolvedCount { get; init; }
    public int OrphanCount { get; init; }
    public int ErrorCount { get; init; }
    public required string Message { get; init; }
}
