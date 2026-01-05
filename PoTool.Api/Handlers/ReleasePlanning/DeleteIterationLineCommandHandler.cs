using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for DeleteIterationLineCommand.
/// </summary>
public sealed class DeleteIterationLineCommandHandler : ICommandHandler<DeleteIterationLineCommand, LineOperationResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<DeleteIterationLineCommandHandler> _logger;

    public DeleteIterationLineCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<DeleteIterationLineCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<LineOperationResultDto> Handle(
        DeleteIterationLineCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling DeleteIterationLineCommand for Line {LineId}", command.LineId);

        var deleted = await _repository.DeleteIterationLineAsync(command.LineId, cancellationToken);

        return new LineOperationResultDto
        {
            Success = deleted,
            LineId = command.LineId,
            ErrorMessage = deleted ? null : $"Iteration line with ID {command.LineId} not found"
        };
    }
}
