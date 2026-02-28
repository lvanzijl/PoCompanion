using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Planning.Queries;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for GetUnplannedEpicsQuery.
/// Retrieves epics that are not yet placed on the Planning Board.
/// </summary>
public sealed class GetUnplannedEpicsQueryHandler : IQueryHandler<GetUnplannedEpicsQuery, IReadOnlyList<UnplannedEpicDto>>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<GetUnplannedEpicsQueryHandler> _logger;

    public GetUnplannedEpicsQueryHandler(
        PoToolDbContext dbContext,
        ILogger<GetUnplannedEpicsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyList<UnplannedEpicDto>> Handle(
        GetUnplannedEpicsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving unplanned epics for Product Owner {ProductOwnerId}", query.ProductOwnerId);

        // Get products for this Product Owner
        var productsQuery = _dbContext.Products
            .Where(p => p.ProductOwnerId == query.ProductOwnerId);

        if (query.ProductId.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.Id == query.ProductId.Value);
        }

        var products = await productsQuery
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return [];
        }

        var productIds = products.Select(p => p.Id).ToList();

        // Get all placed epic IDs
        var placedEpicIds = await _dbContext.PlanningEpicPlacements
            .Select(p => p.EpicId)
            .ToHashSetAsync(cancellationToken);

        // Load all backlog roots for these products and build root → productId map
        var rootsByProduct = await _dbContext.ProductBacklogRoots
            .Where(r => productIds.Contains(r.ProductId))
            .Select(r => new { r.ProductId, r.WorkItemTfsId })
            .ToListAsync(cancellationToken);

        var rootWorkItemIds = rootsByProduct.Select(r => r.WorkItemTfsId).ToHashSet();

        // Build a mapping of root TfsId to productId (first product wins if roots are shared)
        var rootToProductId = rootsByProduct
            .GroupBy(r => r.WorkItemTfsId)
            .ToDictionary(g => g.Key, g => g.First().ProductId);

        // Get all work items that are descendants of the backlog roots
        // Load all potentially relevant work items in one query to avoid N+1
        var relevantWorkItemIds = await _dbContext.WorkItems
            .Where(w => rootWorkItemIds.Contains(w.TfsId) || rootWorkItemIds.Contains(w.ParentTfsId ?? 0))
            .Select(w => w.TfsId)
            .ToHashSetAsync(cancellationToken);

        // Build a mapping of work item TfsId to its ancestor backlog root TfsId by traversing up the hierarchy
        var workItemToRoot = new Dictionary<int, int>();
        
        foreach (var root in rootWorkItemIds)
        {
            workItemToRoot[root] = root;
        }

        // Load parent information for epics in batch
        var parentInfo = await _dbContext.WorkItems
            .Where(w => relevantWorkItemIds.Contains(w.TfsId))
            .Select(w => new { w.TfsId, w.ParentTfsId })
            .ToDictionaryAsync(w => w.TfsId, w => w.ParentTfsId, cancellationToken);

        // Add immediate children
        foreach (var wi in parentInfo)
        {
            if (wi.Value.HasValue && workItemToRoot.ContainsKey(wi.Value.Value))
            {
                workItemToRoot[wi.Key] = workItemToRoot[wi.Value.Value];
            }
        }

        // Get all epics that are not placed
        var epics = await _dbContext.WorkItems
            .Where(w => w.Type == "Epic" && !placedEpicIds.Contains(w.TfsId))
            .Select(w => new
            {
                w.TfsId,
                w.Title,
                w.Effort,
                w.State,
                w.ParentTfsId
            })
            .ToListAsync(cancellationToken);

        // Map epics to their products
        var result = new List<UnplannedEpicDto>();

        foreach (var epic in epics)
        {
            // Try to find which backlog root this epic belongs to
            int? backlogRoot = null;
            
            // Check if epic's parent is in our mapping
            if (epic.ParentTfsId.HasValue && workItemToRoot.TryGetValue(epic.ParentTfsId.Value, out var rootId))
            {
                backlogRoot = rootId;
            }
            else if (epic.ParentTfsId.HasValue && parentInfo.TryGetValue(epic.ParentTfsId.Value, out var grandParentId))
            {
                // Check if grandparent is in our mapping
                if (grandParentId.HasValue && workItemToRoot.TryGetValue(grandParentId.Value, out rootId))
                {
                    backlogRoot = rootId;
                }
            }

            if (backlogRoot.HasValue && rootToProductId.TryGetValue(backlogRoot.Value, out var productId))
            {
                var product = products.FirstOrDefault(p => p.Id == productId);
                if (product != null)
                {
                    result.Add(new UnplannedEpicDto
                    {
                        EpicId = epic.TfsId,
                        Title = epic.Title ?? $"Epic {epic.TfsId}",
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Effort = epic.Effort,
                        State = epic.State ?? "New"
                    });
                }
            }
        }

        return result
            .OrderBy(e => e.ProductName)
            .ThenBy(e => e.Title)
            .ToList();
    }
}
