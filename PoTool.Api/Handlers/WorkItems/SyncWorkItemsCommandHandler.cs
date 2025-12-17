using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;
using PoTool.Api.Services;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for SyncWorkItemsCommand.
/// </summary>
public sealed class SyncWorkItemsCommandHandler : ICommandHandler<SyncWorkItemsCommand>
{
    private readonly WorkItemSyncService _syncService;
    private readonly ILogger<SyncWorkItemsCommandHandler> _logger;

    public SyncWorkItemsCommandHandler(
        WorkItemSyncService syncService,
        ILogger<SyncWorkItemsCommandHandler> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    public async ValueTask<Unit> Handle(
        SyncWorkItemsCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling SyncWorkItemsCommand for AreaPath={AreaPath}", command.AreaPath);
        await _syncService.TriggerSyncAsync(command.AreaPath, cancellationToken);
        return Unit.Value;
    }
}
