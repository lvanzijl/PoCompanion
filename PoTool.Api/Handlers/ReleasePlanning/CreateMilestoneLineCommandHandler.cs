using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for CreateMilestoneLineCommand.
/// </summary>
public sealed class CreateMilestoneLineCommandHandler : ICommandHandler<CreateMilestoneLineCommand, LineOperationResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<CreateMilestoneLineCommandHandler> _logger;

    public CreateMilestoneLineCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<CreateMilestoneLineCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<LineOperationResultDto> Handle(
        CreateMilestoneLineCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling CreateMilestoneLineCommand");

        if (string.IsNullOrWhiteSpace(command.Label))
        {
            return new LineOperationResultDto
            {
                Success = false,
                ErrorMessage = "Label is required"
            };
        }

        var lineId = await _repository.CreateMilestoneLineAsync(
            command.Label,
            command.VerticalPosition,
            command.Type,
            cancellationToken);

        return new LineOperationResultDto
        {
            Success = true,
            LineId = lineId
        };
    }
}
