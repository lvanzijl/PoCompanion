using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Planning.Queries;
using PlanningDtos = PoTool.Shared.Planning;
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
            .Select(p => new { p.Id, p.Name, p.BacklogRootWorkItemId })
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return [];
        }

        // Get all placed epic IDs
        var placedEpicIds = await _dbContext.PlanningEpicPlacements
            .Select(p => p.EpicId)
            .ToHashSetAsync(cancellationToken);

        // Get all epics under the product backlog roots that are not placed
        var productIds = products.Select(p => p.Id).ToHashSet();
        var rootWorkItemIds = products.Select(p => p.BacklogRootWorkItemId).ToHashSet();

        // Get epics that are children of the product backlogs
        var epics = await _dbContext.WorkItems
            .Where(w => w.Type == "Epic" && !placedEpicIds.Contains(w.TfsId))
            .Where(w => rootWorkItemIds.Contains(w.ParentTfsId ?? 0) || 
                        // Also check if parent is a feature under the backlog root
                        _dbContext.WorkItems.Any(parent => 
                            parent.TfsId == w.ParentTfsId && 
                            rootWorkItemIds.Contains(parent.ParentTfsId ?? 0)))
            .Select(w => new
            {
                w.TfsId,
                w.Title,
                w.Effort,
                w.State,
                w.ParentTfsId
            })
            .ToListAsync(cancellationToken);

        // Map epics to their products based on parent hierarchy
        var result = new List<UnplannedEpicDto>();

        foreach (var epic in epics)
        {
            // Find which product this epic belongs to
            var product = products.FirstOrDefault(p => p.BacklogRootWorkItemId == epic.ParentTfsId);
            if (product == null && epic.ParentTfsId.HasValue)
            {
                // Check if parent's parent is a backlog root
                var parentWorkItem = await _dbContext.WorkItems
                    .FirstOrDefaultAsync(w => w.TfsId == epic.ParentTfsId, cancellationToken);
                
                if (parentWorkItem?.ParentTfsId != null)
                {
                    product = products.FirstOrDefault(p => p.BacklogRootWorkItemId == parentWorkItem.ParentTfsId);
                }
            }

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

        return result
            .OrderBy(e => e.ProductName)
            .ThenBy(e => e.Title)
            .ToList();
    }
}
