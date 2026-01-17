using Mediator;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;
using PoTool.Api.Services;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for SyncWorkItemsCommand.
/// NOTE: Sync operations only apply in Cached mode. In Live mode, data is fetched directly from TFS.
/// </summary>
public sealed class SyncWorkItemsCommandHandler : ICommandHandler<SyncWorkItemsCommand>
{
    private readonly WorkItemSyncService _syncService;
    private readonly IDataSourceModeProvider _modeProvider;
    private readonly ILogger<SyncWorkItemsCommandHandler> _logger;

    public SyncWorkItemsCommandHandler(
        WorkItemSyncService syncService,
        IDataSourceModeProvider modeProvider,
        ILogger<SyncWorkItemsCommandHandler> logger)
    {
        _syncService = syncService;
        _modeProvider = modeProvider;
        _logger = logger;
    }

    public async ValueTask<Unit> Handle(
        SyncWorkItemsCommand command,
        CancellationToken cancellationToken)
    {
        // In Live mode, sync operations are not needed as data is fetched directly from TFS
        if (_modeProvider.Mode == DataSourceMode.Live)
        {
            _logger.LogWarning("SyncWorkItemsCommand called in Live mode. Sync operations are only applicable in Cached mode. No action taken.");
            return Unit.Value;
        }

        _logger.LogInformation("Handling SyncWorkItemsCommand for AreaPath={AreaPath} (Cached mode)", command.AreaPath);
        await _syncService.TriggerSyncAsync(command.AreaPath, cancellationToken);
        return Unit.Value;
    }
}
