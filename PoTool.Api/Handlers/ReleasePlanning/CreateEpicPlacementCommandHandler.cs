using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for CreateEpicPlacementCommand.
/// Places an Epic on the Release Planning Board.
/// </summary>
public sealed class CreateEpicPlacementCommandHandler : ICommandHandler<CreateEpicPlacementCommand, EpicPlacementResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly ILogger<CreateEpicPlacementCommandHandler> _logger;

    public CreateEpicPlacementCommandHandler(
        IReleasePlanningRepository repository,
        IWorkItemRepository workItemRepository,
        ILogger<CreateEpicPlacementCommandHandler> logger)
    {
        _repository = repository;
        _workItemRepository = workItemRepository;
        _logger = logger;
    }

    public async ValueTask<EpicPlacementResultDto> Handle(
        CreateEpicPlacementCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling CreateEpicPlacementCommand for Epic {EpicId} in Lane {LaneId}",
            command.EpicId, command.LaneId);

        // Verify the Epic exists and is of type Epic
        var epic = await _workItemRepository.GetByTfsIdAsync(command.EpicId, cancellationToken);
        if (epic == null)
        {
            return new EpicPlacementResultDto
            {
                Success = false,
                ErrorMessage = $"Epic with ID {command.EpicId} not found"
            };
        }

        if (!epic.Type.Equals("Epic", StringComparison.OrdinalIgnoreCase))
        {
            return new EpicPlacementResultDto
            {
                Success = false,
                ErrorMessage = $"Work item {command.EpicId} is not an Epic (type: {epic.Type})"
            };
        }

        // Verify the Lane exists
        var lane = await _repository.GetLaneByIdAsync(command.LaneId, cancellationToken);
        if (lane == null)
        {
            return new EpicPlacementResultDto
            {
                Success = false,
                ErrorMessage = $"Lane with ID {command.LaneId} not found"
            };
        }

        // Verify the Epic's parent Objective matches the Lane's Objective
        // (Lane membership is immutable via the board UI)
        if (epic.ParentTfsId != lane.ObjectiveId)
        {
            return new EpicPlacementResultDto
            {
                Success = false,
                ErrorMessage = $"Epic {command.EpicId} belongs to Objective {epic.ParentTfsId}, not Objective {lane.ObjectiveId}"
            };
        }

        // Check if Epic is already placed (invariant: Epic exists in at most one Lane)
        var existingPlacement = await _repository.GetPlacementByEpicIdAsync(command.EpicId, cancellationToken);
        if (existingPlacement != null)
        {
            return new EpicPlacementResultDto
            {
                Success = false,
                ErrorMessage = $"Epic {command.EpicId} is already placed on the board"
            };
        }

        // Validate RowIndex is non-negative
        if (command.RowIndex < 0)
        {
            return new EpicPlacementResultDto
            {
                Success = false,
                ErrorMessage = "RowIndex must be non-negative"
            };
        }

        var placementId = await _repository.CreatePlacementAsync(
            command.EpicId,
            command.LaneId,
            command.RowIndex,
            command.OrderInRow,
            cancellationToken);

        return new EpicPlacementResultDto
        {
            Success = true,
            PlacementId = placementId
        };
    }
}
