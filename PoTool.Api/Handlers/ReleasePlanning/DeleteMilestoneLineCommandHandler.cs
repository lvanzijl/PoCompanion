using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for DeleteMilestoneLineCommand.
/// </summary>
public sealed class DeleteMilestoneLineCommandHandler : ICommandHandler<DeleteMilestoneLineCommand, LineOperationResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<DeleteMilestoneLineCommandHandler> _logger;

    public DeleteMilestoneLineCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<DeleteMilestoneLineCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<LineOperationResultDto> Handle(
        DeleteMilestoneLineCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling DeleteMilestoneLineCommand for Line {LineId}", command.LineId);

        var deleted = await _repository.DeleteMilestoneLineAsync(command.LineId, cancellationToken);

        return new LineOperationResultDto
        {
            Success = deleted,
            LineId = command.LineId,
            ErrorMessage = deleted ? null : $"Milestone line with ID {command.LineId} not found"
        };
    }
}
