using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for MoveEpicCommand.
/// Moves an Epic to a different row within the same Lane.
/// </summary>
public sealed class MoveEpicCommandHandler : ICommandHandler<MoveEpicCommand, EpicPlacementResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<MoveEpicCommandHandler> _logger;

    public MoveEpicCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<MoveEpicCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<EpicPlacementResultDto> Handle(
        MoveEpicCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling MoveEpicCommand for Placement {PlacementId}", command.PlacementId);

        var placement = await _repository.GetPlacementByIdAsync(command.PlacementId, cancellationToken);
        if (placement == null)
        {
            return new EpicPlacementResultDto
            {
                Success = false,
                ErrorMessage = $"Placement with ID {command.PlacementId} not found"
            };
        }

        // Validate RowIndex is non-negative
        if (command.NewRowIndex < 0)
        {
            return new EpicPlacementResultDto
            {
                Success = false,
                ErrorMessage = "NewRowIndex must be non-negative"
            };
        }

        // Note: Cross-lane moves are forbidden by spec. 
        // The Lane is not changed in this command.
        var updated = await _repository.UpdatePlacementAsync(
            command.PlacementId,
            command.NewRowIndex,
            command.NewOrderInRow,
            cancellationToken);

        return new EpicPlacementResultDto
        {
            Success = updated,
            PlacementId = command.PlacementId
        };
    }
}
