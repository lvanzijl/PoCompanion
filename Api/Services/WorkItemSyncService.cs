using Core.Contracts;

namespace Api.Services;

/// <summary>
/// Background service for synchronizing work items from TFS to local cache.
/// </summary>
public sealed class WorkItemSyncService : BackgroundService
{
    private readonly ILogger<WorkItemSyncService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public WorkItemSyncService(
        ILogger<WorkItemSyncService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Work Item Sync Service is starting");

        // This is a stub - actual implementation will be added in future PRs
        // For now, just log that the service is running
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Work Item Sync Service is running");

            // Wait for 1 hour before next check
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        _logger.LogInformation("Work Item Sync Service is stopping");
    }

    /// <summary>
    /// Triggers an immediate sync of work items from TFS.
    /// This method is a stub and will be implemented in future PRs.
    /// </summary>
    /// <param name="areaPath">The area path to sync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task TriggerSyncAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();

        _logger.LogInformation("Sync triggered for area path: {AreaPath}", areaPath);

        // TODO: Implement actual sync logic in future PR
        // 1. Get ITfsClient from DI
        // 2. Retrieve work items from TFS
        // 3. Store in repository using ReplaceAllAsync
        // 4. Send SignalR notification when complete

        await Task.CompletedTask;
    }
}
