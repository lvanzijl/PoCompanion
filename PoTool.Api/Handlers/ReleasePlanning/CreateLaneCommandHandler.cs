using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for CreateLaneCommand.
/// Creates a new Lane (Objective) on the Release Planning Board.
/// </summary>
public sealed class CreateLaneCommandHandler : ICommandHandler<CreateLaneCommand, LaneOperationResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly ILogger<CreateLaneCommandHandler> _logger;

    public CreateLaneCommandHandler(
        IReleasePlanningRepository repository,
        IWorkItemRepository workItemRepository,
        ILogger<CreateLaneCommandHandler> logger)
    {
        _repository = repository;
        _workItemRepository = workItemRepository;
        _logger = logger;
    }

    public async ValueTask<LaneOperationResultDto> Handle(
        CreateLaneCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling CreateLaneCommand for Objective {ObjectiveId}", command.ObjectiveId);

        // Verify the Objective exists
        var objective = await _workItemRepository.GetByTfsIdAsync(command.ObjectiveId, cancellationToken);
        if (objective == null)
        {
            return new LaneOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Objective with ID {command.ObjectiveId} not found"
            };
        }

        if (!objective.Type.Equals("Objective", StringComparison.OrdinalIgnoreCase))
        {
            return new LaneOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Work item {command.ObjectiveId} is not an Objective (type: {objective.Type})"
            };
        }

        // Check if a Lane for this Objective already exists
        var existingLane = await _repository.GetLaneByObjectiveIdAsync(command.ObjectiveId, cancellationToken);
        if (existingLane != null)
        {
            return new LaneOperationResultDto
            {
                Success = false,
                ErrorMessage = $"A Lane for Objective {command.ObjectiveId} already exists"
            };
        }

        var laneId = await _repository.CreateLaneAsync(command.ObjectiveId, command.DisplayOrder, cancellationToken);

        return new LaneOperationResultDto
        {
            Success = true,
            LaneId = laneId
        };
    }
}
