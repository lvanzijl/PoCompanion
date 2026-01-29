using Microsoft.AspNetCore.SignalR;
using PoTool.Shared.Contracts;

namespace PoTool.Api.Hubs;

/// <summary>
/// SignalR hub for TFS configuration save and verify progress updates.
/// </summary>
public class TfsConfigHub : Hub
{
    private readonly ILogger<TfsConfigHub> _logger;

    public TfsConfigHub(ILogger<TfsConfigHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Joins the TFS config progress group to receive updates.
    /// </summary>
    public async Task JoinConfigProgressGroup()
    {
        var groupName = "TfsConfigProgress";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("Client {ConnectionId} joined TfsConfigProgress group", Context.ConnectionId);
    }

    /// <summary>
    /// Leaves the TFS config progress group.
    /// </summary>
    public async Task LeaveConfigProgressGroup()
    {
        var groupName = "TfsConfigProgress";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("Client {ConnectionId} left TfsConfigProgress group", Context.ConnectionId);
    }
}

/// <summary>
/// Service for broadcasting TFS config progress updates via SignalR.
/// </summary>
public interface ITfsConfigProgressBroadcaster
{
    /// <summary>
    /// Broadcasts a TFS config progress update to all clients watching.
    /// </summary>
    Task BroadcastProgressAsync(TfsConfigProgressUpdate update, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of ITfsConfigProgressBroadcaster using SignalR.
/// </summary>
public class TfsConfigProgressBroadcaster : ITfsConfigProgressBroadcaster
{
    private readonly IHubContext<TfsConfigHub> _hubContext;
    private readonly ILogger<TfsConfigProgressBroadcaster> _logger;

    public TfsConfigProgressBroadcaster(IHubContext<TfsConfigHub> hubContext, ILogger<TfsConfigProgressBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastProgressAsync(TfsConfigProgressUpdate update, CancellationToken cancellationToken = default)
    {
        const string groupName = "TfsConfigProgress";

        await _hubContext.Clients.Group(groupName).SendAsync(
            "ConfigProgress",
            update,
            cancellationToken);

        _logger.LogDebug(
            "Broadcasted TFS config progress: Phase={Phase}, State={State}, Progress={Progress}%",
            update.Phase,
            update.State,
            update.PercentComplete);
    }
}
