using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Planning.Commands;
using PlanningDtos = PoTool.Shared.Planning;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for MovePlanningEpicCommand.
/// Moves an epic placement to a different row within the same product column.
/// </summary>
public sealed class MovePlanningEpicCommandHandler : ICommandHandler<MovePlanningEpicCommand, PlacementOperationResultDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<MovePlanningEpicCommandHandler> _logger;

    public MovePlanningEpicCommandHandler(
        PoToolDbContext dbContext,
        ILogger<MovePlanningEpicCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<PlacementOperationResultDto> Handle(
        MovePlanningEpicCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Moving placement {PlacementId} to row {NewRowId}", 
            command.PlacementId, command.NewRowId);

        var placement = await _dbContext.PlanningEpicPlacements
            .FirstOrDefaultAsync(p => p.Id == command.PlacementId, cancellationToken);

        if (placement == null)
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Placement {command.PlacementId} not found"
            };
        }

        var newRow = await _dbContext.BoardRows
            .FirstOrDefaultAsync(r => r.Id == command.NewRowId, cancellationToken);

        if (newRow == null)
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Row {command.NewRowId} not found"
            };
        }

        // Verify the row is not a marker row
        if (newRow.RowType == Persistence.Entities.BoardRowType.Marker)
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = "Cannot move epics to marker rows"
            };
        }

        // Update placement
        placement.RowId = command.NewRowId;
        placement.OrderInCell = command.NewOrderInCell;
        placement.LastModified = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Moved placement {PlacementId} to row {NewRowId}", 
            command.PlacementId, command.NewRowId);

        return new PlacementOperationResultDto
        {
            Success = true,
            PlacementId = placement.Id
        };
    }
}
