using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="UpdateWorkItemTagsCommand"/>.
/// Updates tags in TFS and refreshes the work item in the local cache.
/// </summary>
public sealed class UpdateWorkItemTagsCommandHandler
    : ICommandHandler<UpdateWorkItemTagsCommand, WorkItemDto?>
{
    private readonly ITfsClient _tfsClient;
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<UpdateWorkItemTagsCommandHandler> _logger;

    public UpdateWorkItemTagsCommandHandler(
        ITfsClient tfsClient,
        IWorkItemRepository repository,
        ILogger<UpdateWorkItemTagsCommandHandler> logger)
    {
        _tfsClient = tfsClient;
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<WorkItemDto?> Handle(UpdateWorkItemTagsCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating work item {TfsId} tags to [{Tags}]", command.TfsId, string.Join("; ", command.Tags));

        // Step 1: Write to TFS and get updated work item
        var updatedWorkItem = await _tfsClient.UpdateWorkItemTagsAndReturnAsync(command.TfsId, command.Tags, cancellationToken);
        if (updatedWorkItem == null)
        {
            _logger.LogWarning("Failed to update tags for work item {TfsId} in TFS", command.TfsId);
            return null;
        }

        // Step 2: Refresh cache with the updated work item from TFS
        await _repository.UpsertManyAsync(new[] { updatedWorkItem }, cancellationToken);
        _logger.LogInformation("Work item {TfsId} tags updated and cache refreshed", command.TfsId);

        return updatedWorkItem;
    }
}
