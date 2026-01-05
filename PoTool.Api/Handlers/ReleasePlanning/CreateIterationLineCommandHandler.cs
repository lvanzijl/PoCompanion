using PoTool.Shared.ReleasePlanning;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.ReleasePlanning.Commands;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for CreateIterationLineCommand.
/// </summary>
public sealed class CreateIterationLineCommandHandler : ICommandHandler<CreateIterationLineCommand, LineOperationResultDto>
{
    private readonly IReleasePlanningRepository _repository;
    private readonly ILogger<CreateIterationLineCommandHandler> _logger;

    public CreateIterationLineCommandHandler(
        IReleasePlanningRepository repository,
        ILogger<CreateIterationLineCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<LineOperationResultDto> Handle(
        CreateIterationLineCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling CreateIterationLineCommand");

        if (string.IsNullOrWhiteSpace(command.Label))
        {
            return new LineOperationResultDto
            {
                Success = false,
                ErrorMessage = "Label is required"
            };
        }

        var lineId = await _repository.CreateIterationLineAsync(
            command.Label, 
            command.VerticalPosition, 
            cancellationToken);

        return new LineOperationResultDto
        {
            Success = true,
            LineId = lineId
        };
    }
}
