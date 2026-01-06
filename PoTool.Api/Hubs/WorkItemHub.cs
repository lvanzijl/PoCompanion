using Microsoft.AspNetCore.SignalR;
using PoTool.Api.Services;

namespace PoTool.Api.Hubs;

/// <summary>
/// SignalR hub for real-time work item updates.
/// </summary>
public class WorkItemHub : Hub
{
    private readonly WorkItemSyncService _syncService;
    private readonly TfsConfigurationService _configService;
    private readonly ILogger<WorkItemHub> _logger;

    public WorkItemHub(
        WorkItemSyncService syncService,
        TfsConfigurationService configService,
        ILogger<WorkItemHub> logger)
    {
        _syncService = syncService;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Triggers a sync of work items from TFS using the configured Default Area Path.
    /// The areaPath parameter is deprecated and ignored - configuration value is always used.
    /// </summary>
    public async Task RequestSync(string areaPath)
    {
        _logger.LogInformation("Sync requested by client {ConnectionId}", Context.ConnectionId);

        // Read DefaultAreaPath from configuration (ignore the provided parameter)
        var config = await _configService.GetConfigAsync();
        var configuredAreaPath = config?.DefaultAreaPath;
        
        if (string.IsNullOrWhiteSpace(configuredAreaPath))
        {
            _logger.LogError("Sync requested but Default Area Path is not configured");
            await Clients.Caller.SendAsync("SyncStatus", new { Status = "Failed", Message = "Default Area Path is not configured. Configure this in TFS settings." });
            return;
        }

        _logger.LogInformation("Using configured area path: {AreaPath}", configuredAreaPath);
        await _syncService.TriggerSyncAsync(configuredAreaPath);
    }

    /// <summary>
    /// Notifies all clients about work items without effort estimation.
    /// </summary>
    public async Task NotifyWorkItemsWithoutEffort(IReadOnlyList<int> workItemIds)
    {
        _logger.LogInformation("Broadcasting notification for {Count} work items without effort", workItemIds.Count);
        await Clients.All.SendAsync("WorkItemsWithoutEffort", workItemIds);
    }
}
