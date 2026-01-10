using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for UpdateIterationLineCommand.
/// </summary>
public sealed class UpdateIterationLineCommandHandler : ICommandHandler<UpdateIterationLineCommand, LineOperationResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<UpdateIterationLineCommandHandler> _logger;

    public UpdateIterationLineCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<UpdateIterationLineCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<LineOperationResultDto> Handle(
        UpdateIterationLineCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling UpdateIterationLineCommand for Line {LineId}", command.LineId);

        var line = await _repository.GetIterationLineByIdAsync(command.LineId, cancellationToken);
        if (line == null)
        {
            return new LineOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Iteration line with ID {command.LineId} not found"
            };
        }

        if (string.IsNullOrWhiteSpace(command.Label))
        {
            return new LineOperationResultDto
            {
                Success = false,
                ErrorMessage = "Label is required"
            };
        }

        var updated = await _repository.UpdateIterationLineAsync(
            command.LineId,
            command.Label,
            command.VerticalPosition,
            cancellationToken);

        return new LineOperationResultDto
        {
            Success = updated,
            LineId = command.LineId
        };
    }
}
