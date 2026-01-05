using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for DeleteEpicPlacementCommand.
/// Note: Dragging Epics back to the Unplanned list is forbidden by spec.
/// This handler is for internal/cleanup use only.
/// </summary>
public sealed class DeleteEpicPlacementCommandHandler : ICommandHandler<DeleteEpicPlacementCommand, EpicPlacementResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<DeleteEpicPlacementCommandHandler> _logger;

    public DeleteEpicPlacementCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<DeleteEpicPlacementCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<EpicPlacementResultDto> Handle(
        DeleteEpicPlacementCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling DeleteEpicPlacementCommand for Placement {PlacementId}", command.PlacementId);

        var deleted = await _repository.DeletePlacementAsync(command.PlacementId, cancellationToken);

        return new EpicPlacementResultDto
        {
            Success = deleted,
            PlacementId = command.PlacementId,
            ErrorMessage = deleted ? null : $"Placement with ID {command.PlacementId} not found"
        };
    }
}
