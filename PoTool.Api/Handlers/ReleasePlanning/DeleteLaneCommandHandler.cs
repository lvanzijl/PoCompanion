using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for DeleteLaneCommand.
/// Deletes a Lane and all its Epic placements from the Release Planning Board.
/// </summary>
public sealed class DeleteLaneCommandHandler : ICommandHandler<DeleteLaneCommand, LaneOperationResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<DeleteLaneCommandHandler> _logger;

    public DeleteLaneCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<DeleteLaneCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<LaneOperationResultDto> Handle(
        DeleteLaneCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling DeleteLaneCommand for Lane {LaneId}", command.LaneId);

        var lane = await _repository.GetLaneByIdAsync(command.LaneId, cancellationToken);
        if (lane == null)
        {
            return new LaneOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Lane with ID {command.LaneId} not found"
            };
        }

        // Delete placements first (cascade should handle this, but be explicit)
        await _repository.DeletePlacementsByLaneIdAsync(command.LaneId, cancellationToken);

        // Delete the lane
        var deleted = await _repository.DeleteLaneAsync(command.LaneId, cancellationToken);

        return new LaneOperationResultDto
        {
            Success = deleted,
            LaneId = command.LaneId
        };
    }
}
