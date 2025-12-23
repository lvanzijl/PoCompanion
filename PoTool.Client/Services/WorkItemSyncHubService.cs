using Microsoft.AspNetCore.SignalR.Client;

namespace PoTool.Client.Services;

/// <summary>
/// Implementation of SignalR connection management for work item synchronization.
/// </summary>
public class WorkItemSyncHubService : IWorkItemSyncHubService, IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _isDisposed;

    /// <inheritdoc/>
    public event Action<string, string>? OnSyncStatusChanged;

    /// <inheritdoc/>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <inheritdoc/>
    public async Task StartAsync(string hubUrl)
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(WorkItemSyncHubService));

            if (_hubConnection != null)
            {
                if (_hubConnection.State == HubConnectionState.Connected)
                {
                    Console.WriteLine("[WorkItemSyncHubService] Already connected");
                    return;
                }
                // Only stop if connection exists and is not already disconnected
                if (_hubConnection.State != HubConnectionState.Disconnected)
                {
                    await StopAsync();
                }
            }

            Console.WriteLine($"[WorkItemSyncHubService] Connecting to hub: {hubUrl}");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<object>("SyncStatus", HandleSyncStatus);

            await _hubConnection.StartAsync();
            Console.WriteLine("[WorkItemSyncHubService] SignalR connected successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WorkItemSyncHubService] Error connecting to SignalR hub: {ex.Message}");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
                Console.WriteLine("[WorkItemSyncHubService] SignalR disconnected");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorkItemSyncHubService] Error stopping connection: {ex.Message}");
            }
        }
    }

    /// <inheritdoc/>
    public async Task RequestSyncAsync(string areaPath)
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("SignalR connection is not active. Call StartAsync first.");
        }

        try
        {
            await _hubConnection.SendAsync("RequestSync", areaPath);
            Console.WriteLine($"[WorkItemSyncHubService] Sync requested for area path: {areaPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WorkItemSyncHubService] Error requesting sync: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _isDisposed = true;
        
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
        
        _connectionLock.Dispose();
    }

    private void HandleSyncStatus(object status)
    {
        try
        {
            var statusDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                System.Text.Json.JsonSerializer.Serialize(status));

            if (statusDict != null)
            {
                var statusValue = statusDict.GetValueOrDefault("Status", "Unknown");
                var message = statusDict.GetValueOrDefault("Message", "");

                Console.WriteLine($"[WorkItemSyncHubService] Sync status: {statusValue} - {message}");
                OnSyncStatusChanged?.Invoke(statusValue, message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WorkItemSyncHubService] Error handling sync status: {ex.Message}");
        }
    }
}
