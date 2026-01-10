using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for UpdateMilestoneLineCommand.
/// </summary>
public sealed class UpdateMilestoneLineCommandHandler : ICommandHandler<UpdateMilestoneLineCommand, LineOperationResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<UpdateMilestoneLineCommandHandler> _logger;

    public UpdateMilestoneLineCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<UpdateMilestoneLineCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<LineOperationResultDto> Handle(
        UpdateMilestoneLineCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling UpdateMilestoneLineCommand for Line {LineId}", command.LineId);

        var line = await _repository.GetMilestoneLineByIdAsync(command.LineId, cancellationToken);
        if (line == null)
        {
            return new LineOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Milestone line with ID {command.LineId} not found"
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

        var updated = await _repository.UpdateMilestoneLineAsync(
            command.LineId,
            command.Label,
            command.VerticalPosition,
            command.Type,
            cancellationToken);

        return new LineOperationResultDto
        {
            Success = updated,
            LineId = command.LineId
        };
    }
}
