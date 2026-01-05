using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for ReorderEpicsInRowCommand.
/// Reorders Epics within a single row.
/// </summary>
public sealed class ReorderEpicsInRowCommandHandler : ICommandHandler<ReorderEpicsInRowCommand, EpicPlacementResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<ReorderEpicsInRowCommandHandler> _logger;

    public ReorderEpicsInRowCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<ReorderEpicsInRowCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<EpicPlacementResultDto> Handle(
        ReorderEpicsInRowCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling ReorderEpicsInRowCommand for Lane {LaneId}, Row {RowIndex}", 
            command.LaneId, command.RowIndex);

        // Get all placements in the specified lane and row
        var lanePlacements = await _repository.GetPlacementsByLaneIdAsync(command.LaneId, cancellationToken);
        var rowPlacements = lanePlacements
            .Where(p => p.RowIndex == command.RowIndex)
            .ToList();

        // Verify all provided placement IDs are in this row
        var rowPlacementIds = rowPlacements.Select(p => p.Id).ToHashSet();
        var providedIds = command.PlacementIdsInOrder.ToHashSet();

        if (!providedIds.SetEquals(rowPlacementIds))
        {
            return new EpicPlacementResultDto
            {
                Success = false,
                ErrorMessage = "Provided placement IDs do not match the placements in the specified row"
            };
        }

        // Update OrderInRow for each placement
        for (int i = 0; i < command.PlacementIdsInOrder.Count; i++)
        {
            var placementId = command.PlacementIdsInOrder[i];
            await _repository.UpdatePlacementAsync(placementId, command.RowIndex, i, cancellationToken);
        }

        return new EpicPlacementResultDto
        {
            Success = true
        };
    }
}
