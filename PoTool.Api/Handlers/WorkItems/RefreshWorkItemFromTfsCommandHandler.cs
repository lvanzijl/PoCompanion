using Mediator;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for <see cref="RefreshWorkItemFromTfsCommand"/>.
/// Re-fetches the specified work item from TFS and upserts it in the local DB.
/// </summary>
public sealed class RefreshWorkItemFromTfsCommandHandler
    : ICommandHandler<RefreshWorkItemFromTfsCommand, bool>
{
    private readonly ITfsClient _tfsClient;
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<RefreshWorkItemFromTfsCommandHandler> _logger;

    public RefreshWorkItemFromTfsCommandHandler(
        ITfsClient tfsClient,
        IWorkItemRepository repository,
        ILogger<RefreshWorkItemFromTfsCommandHandler> logger)
    {
        _tfsClient = tfsClient;
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<bool> Handle(RefreshWorkItemFromTfsCommand command, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Refreshing work item {TfsId} from TFS", command.TfsId);

        var workItem = await _tfsClient.GetWorkItemByIdAsync(command.TfsId, cancellationToken);
        if (workItem == null)
        {
            _logger.LogWarning("Work item {TfsId} not found in TFS", command.TfsId);
            return false;
        }

        await _repository.UpsertManyAsync(new[] { workItem }, cancellationToken);
        _logger.LogDebug("Work item {TfsId} refreshed and stored", command.TfsId);
        return true;
    }
}
