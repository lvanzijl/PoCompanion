using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Planning.Commands;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for CreatePlanningEpicPlacementCommand.
/// Places an epic on the Planning Board.
/// </summary>
public sealed class CreatePlanningEpicPlacementCommandHandler : ICommandHandler<CreatePlanningEpicPlacementCommand, PlacementOperationResultDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<CreatePlanningEpicPlacementCommandHandler> _logger;

    public CreatePlanningEpicPlacementCommandHandler(
        PoToolDbContext dbContext,
        ILogger<CreatePlanningEpicPlacementCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<PlacementOperationResultDto> Handle(
        CreatePlanningEpicPlacementCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Placing Epic {EpicId} in row {RowId}", command.EpicId, command.RowId);

        // Verify the row exists
        var row = await _dbContext.BoardRows
            .FirstOrDefaultAsync(r => r.Id == command.RowId, cancellationToken);

        if (row == null)
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Row {command.RowId} not found"
            };
        }

        // Verify the row is not a marker row
        if (row.RowType == PoTool.Api.Persistence.Entities.BoardRowType.Marker)
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = "Cannot place epics in marker rows"
            };
        }

        // Verify the epic exists
        var epic = await _dbContext.WorkItems
            .FirstOrDefaultAsync(w => w.TfsId == command.EpicId, cancellationToken);

        if (epic == null)
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Epic {command.EpicId} not found"
            };
        }

        if (!epic.Type.Equals("Epic", StringComparison.OrdinalIgnoreCase))
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Work item {command.EpicId} is not an Epic (type: {epic.Type})"
            };
        }

        // Check if epic is already placed
        var existingPlacement = await _dbContext.PlanningEpicPlacements
            .FirstOrDefaultAsync(p => p.EpicId == command.EpicId, cancellationToken);

        if (existingPlacement != null)
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Epic {command.EpicId} is already placed on the board"
            };
        }

        // Find the product this epic belongs to
        var productId = await FindProductForEpicAsync(epic.TfsId, epic.ParentTfsId, cancellationToken);

        if (!productId.HasValue)
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Could not determine product for Epic {command.EpicId}"
            };
        }

        var placement = new PlanningEpicPlacementEntity
        {
            EpicId = command.EpicId,
            ProductId = productId.Value,
            RowId = command.RowId,
            OrderInCell = command.OrderInCell,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        };

        _dbContext.PlanningEpicPlacements.Add(placement);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Placed Epic {EpicId} in row {RowId} for product {ProductId}", 
            command.EpicId, command.RowId, productId.Value);

        return new PlacementOperationResultDto
        {
            Success = true,
            PlacementId = placement.Id
        };
    }

    private async Task<int?> FindProductForEpicAsync(int epicId, int? parentId, CancellationToken cancellationToken)
    {
        // Look for a product whose BacklogRootWorkItemIds contain the epic's parent hierarchy
        if (!parentId.HasValue)
        {
            return null;
        }

        // Check if parent is a backlog root
        var product = await _dbContext.ProductBacklogRoots
            .Where(r => r.WorkItemTfsId == parentId.Value)
            .Select(r => r.ProductId)
            .FirstOrDefaultAsync(cancellationToken);

        if (product != default)
        {
            return product;
        }

        // Check parent's parent
        var parentWorkItem = await _dbContext.WorkItems
            .FirstOrDefaultAsync(w => w.TfsId == parentId.Value, cancellationToken);

        if (parentWorkItem?.ParentTfsId.HasValue == true)
        {
            return await FindProductForEpicAsync(epicId, parentWorkItem.ParentTfsId, cancellationToken);
        }

        return null;
    }
}
