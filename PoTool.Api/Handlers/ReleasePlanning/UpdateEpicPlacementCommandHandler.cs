using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for UpdateEpicPlacementCommand.
/// Updates an Epic's placement on the Release Planning Board.
/// </summary>
public sealed class UpdateEpicPlacementCommandHandler : ICommandHandler<UpdateEpicPlacementCommand, EpicPlacementResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<UpdateEpicPlacementCommandHandler> _logger;

    public UpdateEpicPlacementCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<UpdateEpicPlacementCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<EpicPlacementResultDto> Handle(
        UpdateEpicPlacementCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling UpdateEpicPlacementCommand for Placement {PlacementId}", command.PlacementId);

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
        if (command.RowIndex < 0)
        {
            return new EpicPlacementResultDto
            {
                Success = false,
                ErrorMessage = "RowIndex must be non-negative"
            };
        }

        var updated = await _repository.UpdatePlacementAsync(
            command.PlacementId, 
            command.RowIndex, 
            command.OrderInRow, 
            cancellationToken);

        return new EpicPlacementResultDto
        {
            Success = updated,
            PlacementId = command.PlacementId
        };
    }
}
