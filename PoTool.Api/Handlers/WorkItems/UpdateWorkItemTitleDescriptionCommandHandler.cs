using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="UpdateWorkItemTitleDescriptionCommand"/>.
/// Updates title and/or description in TFS and refreshes the work item in the local cache.
/// </summary>
public sealed class UpdateWorkItemTitleDescriptionCommandHandler
    : ICommandHandler<UpdateWorkItemTitleDescriptionCommand, WorkItemDto?>
{
    private readonly ITfsClient _tfsClient;
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<UpdateWorkItemTitleDescriptionCommandHandler> _logger;

    public UpdateWorkItemTitleDescriptionCommandHandler(
        ITfsClient tfsClient,
        IWorkItemRepository repository,
        ILogger<UpdateWorkItemTitleDescriptionCommandHandler> logger)
    {
        _tfsClient = tfsClient;
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<WorkItemDto?> Handle(UpdateWorkItemTitleDescriptionCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating work item {TfsId} title/description", command.TfsId);

        // Step 1: Write to TFS and get updated work item
        var updatedWorkItem = await _tfsClient.UpdateWorkItemTitleDescriptionAsync(
            command.TfsId, command.Title, command.Description, cancellationToken);
        if (updatedWorkItem == null)
        {
            _logger.LogWarning("Failed to update title/description for work item {TfsId} in TFS", command.TfsId);
            return null;
        }

        // Step 2: Refresh cache with the updated work item from TFS
        await _repository.UpsertManyAsync(new[] { updatedWorkItem }, cancellationToken);
        _logger.LogInformation("Work item {TfsId} title/description updated and cache refreshed", command.TfsId);

        return updatedWorkItem;
    }
}
