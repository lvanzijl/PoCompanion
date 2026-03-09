using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="RefreshWorkItemsByRootIdsFromTfsCommand"/>.
/// Re-fetches the specified hierarchy from TFS and upserts it in the local DB cache.
/// </summary>
public sealed class RefreshWorkItemsByRootIdsFromTfsCommandHandler
    : ICommandHandler<RefreshWorkItemsByRootIdsFromTfsCommand, int>
{
    private readonly ITfsClient _tfsClient;
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<RefreshWorkItemsByRootIdsFromTfsCommandHandler> _logger;

    public RefreshWorkItemsByRootIdsFromTfsCommandHandler(
        ITfsClient tfsClient,
        IWorkItemRepository repository,
        ILogger<RefreshWorkItemsByRootIdsFromTfsCommandHandler> logger)
    {
        _tfsClient = tfsClient;
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<int> Handle(RefreshWorkItemsByRootIdsFromTfsCommand command, CancellationToken cancellationToken)
    {
        if (command.RootIds.Length == 0)
        {
            _logger.LogWarning("No root IDs provided for hierarchy refresh");
            return 0;
        }

        _logger.LogDebug("Refreshing work item hierarchy from TFS for root IDs {RootIds}", string.Join(", ", command.RootIds));

        var workItems = (await _tfsClient.GetWorkItemsByRootIdsAsync(command.RootIds, cancellationToken: cancellationToken)).ToList();
        if (workItems.Count == 0)
        {
            _logger.LogWarning("No work items returned from TFS for root IDs {RootIds}", string.Join(", ", command.RootIds));
            return 0;
        }

        await _repository.UpsertManyAsync(workItems, cancellationToken);
        _logger.LogDebug("Refreshed and stored {Count} work items for root IDs {RootIds}", workItems.Count, string.Join(", ", command.RootIds));
        return workItems.Count;
    }
}
