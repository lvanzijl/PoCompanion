using Microsoft.AspNetCore.SignalR;
using PoTool.Api.Services;

namespace PoTool.Api.Hubs;

/// <summary>
/// SignalR hub for real-time work item updates.
/// </summary>
public class WorkItemHub : Hub
{
    private readonly WorkItemSyncService _syncService;
    private readonly ILogger<WorkItemHub> _logger;

    public WorkItemHub(
        WorkItemSyncService syncService,
        ILogger<WorkItemHub> logger)
    {
        _syncService = syncService;
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
    /// Triggers a sync of work items from TFS.
    /// </summary>
    public async Task RequestSync(string areaPath)
    {
        _logger.LogInformation("Sync requested by client {ConnectionId} for area path: {AreaPath}",
            Context.ConnectionId, areaPath);

        await _syncService.TriggerSyncAsync(areaPath);
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
