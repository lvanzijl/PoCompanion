using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="UpdateWorkItemBacklogPriorityCommand"/>.
/// Updates BacklogPriority in TFS and refreshes the work item in the local cache.
/// </summary>
public sealed class UpdateWorkItemBacklogPriorityCommandHandler
    : ICommandHandler<UpdateWorkItemBacklogPriorityCommand, bool>
{
    private readonly ITfsClient _tfsClient;
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<UpdateWorkItemBacklogPriorityCommandHandler> _logger;

    public UpdateWorkItemBacklogPriorityCommandHandler(
        ITfsClient tfsClient,
        IWorkItemRepository repository,
        ILogger<UpdateWorkItemBacklogPriorityCommandHandler> logger)
    {
        _tfsClient = tfsClient;
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<bool> Handle(UpdateWorkItemBacklogPriorityCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating work item {TfsId} BacklogPriority to {Priority}", command.TfsId, command.NewBacklogPriority);

        // Step 1: Write to TFS
        var success = await _tfsClient.UpdateWorkItemBacklogPriorityAsync(command.TfsId, command.NewBacklogPriority, cancellationToken);
        if (!success)
        {
            _logger.LogWarning("Failed to update BacklogPriority for work item {TfsId} in TFS", command.TfsId);
            return false;
        }

        // Step 2: Refresh from TFS to get the authoritative state back into cache
        var refreshed = await _tfsClient.GetWorkItemByIdAsync(command.TfsId, cancellationToken);
        if (refreshed != null)
        {
            await _repository.UpsertManyAsync(new[] { refreshed }, cancellationToken);
            _logger.LogInformation("Work item {TfsId} BacklogPriority updated and cache refreshed", command.TfsId);
        }
        else
        {
            _logger.LogWarning("Work item {TfsId} was updated in TFS but could not be re-fetched for cache refresh", command.TfsId);
        }

        return true;
    }
}
