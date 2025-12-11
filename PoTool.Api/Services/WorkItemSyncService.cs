using Microsoft.AspNetCore.SignalR;
using PoTool.Api.Hubs;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

/// <summary>
/// Background service stub for work item synchronization.
/// </summary>
public class WorkItemSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkItemSyncService> _logger;
    private readonly IHubContext<WorkItemHub> _hubContext;

    public WorkItemSyncService(
        IServiceProvider serviceProvider,
        ILogger<WorkItemSyncService> logger,
        IHubContext<WorkItemHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Work Item Sync Service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Stub: Future implementation will poll for sync requests
            // or respond to triggers from the UI via SignalR
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        _logger.LogInformation("Work Item Sync Service is stopping");
    }

    /// <summary>
    /// Triggers a manual sync of work items from TFS.
    /// </summary>
    public async Task TriggerSyncAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual sync triggered for area path: {AreaPath}", areaPath);

        using var scope = _serviceProvider.CreateScope();
        var tfsClient = scope.ServiceProvider.GetService<ITfsClient>();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();

        if (tfsClient == null)
        {
            _logger.LogWarning("ITfsClient not registered, cannot perform sync");
            await _hubContext.Clients.All.SendAsync(
                "SyncStatus",
                new { Status = "Failed", Message = "TFS client not configured" },
                cancellationToken);
            return;
        }

        try
        {
            await _hubContext.Clients.All.SendAsync(
                "SyncStatus",
                new { Status = "InProgress", Message = "Retrieving work items from TFS..." },
                cancellationToken);

            var workItems = await tfsClient.GetWorkItemsAsync(areaPath, cancellationToken);
            await repository.ReplaceAllAsync(workItems, cancellationToken);

            await _hubContext.Clients.All.SendAsync(
                "SyncStatus",
                new { Status = "Completed", Message = $"Successfully synced {workItems.Count()} work items" },
                cancellationToken);

            _logger.LogInformation("Sync completed successfully for area path: {AreaPath}", areaPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync for area path: {AreaPath}", areaPath);
            
            await _hubContext.Clients.All.SendAsync(
                "SyncStatus",
                new { Status = "Failed", Message = $"Sync failed: {ex.Message}" },
                cancellationToken);
        }
    }
}
