using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Planning.Queries;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for GetPlanningBoardQuery.
/// Retrieves the complete Planning Board state for a Product Owner.
/// </summary>
public sealed class GetPlanningBoardQueryHandler : IQueryHandler<GetPlanningBoardQuery, PlanningBoardDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<GetPlanningBoardQueryHandler> _logger;

    public GetPlanningBoardQueryHandler(
        PoToolDbContext dbContext,
        ILogger<GetPlanningBoardQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<PlanningBoardDto> Handle(
        GetPlanningBoardQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving Planning Board for Product Owner {ProductOwnerId}", query.ProductOwnerId);

        // Get settings or create default
        var settings = await _dbContext.PlanningBoardSettings
            .FirstOrDefaultAsync(s => s.ProductOwnerId == query.ProductOwnerId, cancellationToken);

        var scope = settings?.Scope ?? PlanningBoardScope.AllProducts;
        var selectedProductId = settings?.SelectedProductId;
        var hiddenProductIds = ParseHiddenProductIds(settings?.HiddenProductIdsJson);

        // Get all products for this Product Owner
        var products = await _dbContext.Products
            .Where(p => p.ProductOwnerId == query.ProductOwnerId)
            .OrderBy(p => p.Order)
            .Select(p => new ProductColumnDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                IsVisible = !hiddenProductIds.Contains(p.Id),
                DisplayOrder = p.Order
            })
            .ToListAsync(cancellationToken);

        // Get all board rows
        var rows = await _dbContext.BoardRows
            .OrderBy(r => r.DisplayOrder)
            .Select(r => new BoardRowDto
            {
                Id = r.Id,
                DisplayOrder = r.DisplayOrder,
                RowType = (PoTool.Shared.Planning.BoardRowType)(int)r.RowType,
                MarkerType = r.MarkerRowType.HasValue ? (PoTool.Shared.Planning.MarkerRowType)(int)r.MarkerRowType : null,
                MarkerLabel = r.MarkerLabel
            })
            .ToListAsync(cancellationToken);

        // If no rows exist, return empty board (client should trigger initialization)
        if (rows.Count == 0)
        {
            return new PlanningBoardDto
            {
                Scope = (BoardScope)(int)scope,
                SelectedProductId = selectedProductId,
                ProductColumns = products,
                Rows = [],
                Placements = [],
                HiddenColumnCount = hiddenProductIds.Count
            };
        }

        // Get all epic placements with work item info
        var placements = await _dbContext.PlanningEpicPlacements
            .Include(p => p.Row)
            .Join(
                _dbContext.WorkItems,
                p => p.EpicId,
                w => w.TfsId,
                (p, w) => new EpicPlacementDto
                {
                    Id = p.Id,
                    EpicId = p.EpicId,
                    EpicTitle = w.Title ?? $"Epic {p.EpicId}",
                    ProductId = p.ProductId,
                    RowId = p.RowId,
                    OrderInCell = p.OrderInCell,
                    IsSelected = false,
                    Effort = w.Effort,
                    State = w.State ?? "New"
                })
            .ToListAsync(cancellationToken);

        return new PlanningBoardDto
        {
            Scope = (BoardScope)(int)scope,
            SelectedProductId = selectedProductId,
            ProductColumns = products,
            Rows = rows,
            Placements = placements,
            HiddenColumnCount = hiddenProductIds.Count
        };
    }

    private static HashSet<int> ParseHiddenProductIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var ids = System.Text.Json.JsonSerializer.Deserialize<List<int>>(json);
            return ids != null ? new HashSet<int>(ids) : [];
        }
        catch
        {
            return [];
        }
    }
}
