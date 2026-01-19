using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.Settings.Commands;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for saving work item state classifications.
/// </summary>
public sealed class SaveStateClassificationsCommandHandler : IRequestHandler<SaveStateClassificationsCommand, bool>
{
    private readonly IWorkItemStateClassificationService _classificationService;
    private readonly ILogger<SaveStateClassificationsCommandHandler> _logger;

    public SaveStateClassificationsCommandHandler(
        IWorkItemStateClassificationService classificationService,
        ILogger<SaveStateClassificationsCommandHandler> logger)
    {
        _classificationService = classificationService;
        _logger = logger;
    }

    public async ValueTask<bool> Handle(
        SaveStateClassificationsCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Saving {Count} state classifications for project '{Project}'",
            command.Request.Classifications.Count, command.Request.ProjectName);

        var success = await _classificationService.SaveClassificationsAsync(
            command.Request,
            cancellationToken);

        if (success)
        {
            _logger.LogInformation("Successfully saved state classifications");
        }
        else
        {
            _logger.LogWarning("Failed to save state classifications");
        }

        return success;
    }
}
