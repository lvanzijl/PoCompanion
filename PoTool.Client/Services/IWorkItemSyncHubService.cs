namespace PoTool.Client.Services;

/// <summary>
/// Service for managing SignalR connection to the work item sync hub.
/// </summary>
public interface IWorkItemSyncHubService : IAsyncDisposable
{
    /// <summary>
    /// Raised when a sync status update is received from the server.
    /// Parameters: status, message
    /// </summary>
    event Action<string, string>? OnSyncStatusChanged;

    /// <summary>
    /// Raised when a detailed sync progress update is received from the server.
    /// </summary>
    event Action<PoTool.Shared.WorkItems.SyncProgressDto>? OnSyncProgressReceived;

    /// <summary>
    /// Gets whether the SignalR connection is currently active.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Starts the SignalR connection to the specified hub URL.
    /// </summary>
    /// <param name="hubUrl">The full URL to the SignalR hub endpoint.</param>
    Task StartAsync(string hubUrl);

    /// <summary>
    /// Stops the SignalR connection.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Requests a work item sync operation via SignalR.
    /// </summary>
    /// <param name="areaPath">The area path to sync (e.g., "DefaultAreaPath").</param>
    Task RequestSyncAsync(string areaPath);
}
