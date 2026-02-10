using Microsoft.AspNetCore.SignalR;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Hubs;

/// <summary>
/// SignalR hub for cache sync progress updates.
/// </summary>
public class CacheSyncHub : Hub
{
    private readonly ILogger<CacheSyncHub> _logger;

    public CacheSyncHub(ILogger<CacheSyncHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Joins a group to receive updates for a specific Product Owner.
    /// </summary>
    public async Task JoinProductOwnerGroup(int productOwnerId)
    {
        var groupName = GetGroupName(productOwnerId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("Client {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leaves a Product Owner group.
    /// </summary>
    public async Task LeaveProductOwnerGroup(int productOwnerId)
    {
        var groupName = GetGroupName(productOwnerId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("Client {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
    }

    private static string GetGroupName(int productOwnerId) => $"ProductOwner_{productOwnerId}";
}

/// <summary>
/// Service for broadcasting sync progress updates via SignalR.
/// </summary>
public interface ISyncProgressBroadcaster
{
    /// <summary>
    /// Broadcasts a sync progress update to all clients watching the specified Product Owner.
    /// </summary>
    Task BroadcastProgressAsync(int productOwnerId, SyncProgressUpdate update, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of ISyncProgressBroadcaster using SignalR.
/// </summary>
public class SyncProgressBroadcaster : ISyncProgressBroadcaster
{
    private readonly IHubContext<CacheSyncHub> _hubContext;
    private readonly ILogger<SyncProgressBroadcaster> _logger;

    public SyncProgressBroadcaster(IHubContext<CacheSyncHub> hubContext, ILogger<SyncProgressBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastProgressAsync(int productOwnerId, SyncProgressUpdate update, CancellationToken cancellationToken = default)
    {
        var groupName = $"ProductOwner_{productOwnerId}";

        // Convert Core type to Shared DTO for client consumption
        var dto = new SyncProgressUpdateDto
        {
            CurrentStage = update.CurrentStage,
            StageProgressPercent = update.StageProgressPercent,
            IsComplete = update.IsComplete,
            HasFailed = update.HasFailed,
            ErrorMessage = update.ErrorMessage,
            HasWarnings = update.HasWarnings,
            WarningMessage = update.WarningMessage,
            StageNumber = update.StageNumber,
            TotalStages = update.TotalStages
        };
        
        await _hubContext.Clients.Group(groupName).SendAsync(
            "SyncProgress",
            dto,
            cancellationToken);

        _logger.LogDebug(
            "Broadcasted sync progress for ProductOwner {ProductOwnerId}: Stage={Stage}, Progress={Progress}%",
            productOwnerId,
            update.CurrentStage,
            update.StageProgressPercent);
    }
}
