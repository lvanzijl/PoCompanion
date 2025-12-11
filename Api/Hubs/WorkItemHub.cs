using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// SignalR hub for real-time work item updates.
/// </summary>
public sealed class WorkItemHub : Hub
{
    private readonly ILogger<WorkItemHub> _logger;

    public WorkItemHub(ILogger<WorkItemHub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Notifies all clients that work items have been updated.
    /// This is typically called after a sync operation completes.
    /// </summary>
    public async Task NotifyWorkItemsUpdated()
    {
        _logger.LogInformation("Broadcasting work items updated notification");
        await Clients.All.SendAsync("WorkItemsUpdated");
    }

    /// <summary>
    /// Notifies all clients about sync progress.
    /// </summary>
    /// <param name="message">Progress message.</param>
    /// <param name="percentage">Progress percentage (0-100).</param>
    public async Task NotifySyncProgress(string message, int percentage)
    {
        _logger.LogDebug("Broadcasting sync progress: {Message} ({Percentage}%)", message, percentage);
        await Clients.All.SendAsync("SyncProgress", message, percentage);
    }
}
