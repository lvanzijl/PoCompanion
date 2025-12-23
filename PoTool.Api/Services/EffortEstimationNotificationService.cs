using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Hubs;
using PoTool.Api.Persistence;
using PoTool.Core.Settings.Queries;
using Mediator;

namespace PoTool.Api.Services;

/// <summary>
/// Background service that monitors work items without effort and sends proactive notifications.
/// </summary>
public class EffortEstimationNotificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<WorkItemHub> _hubContext;
    private readonly ILogger<EffortEstimationNotificationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public EffortEstimationNotificationService(
        IServiceProvider serviceProvider,
        IHubContext<WorkItemHub> hubContext,
        ILogger<EffortEstimationNotificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Effort Estimation Notification Service is starting");

        // Wait a bit before starting to allow the application to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForWorkItemsWithoutEffortAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Effort Estimation Notification Service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Effort Estimation Notification Service is stopping");
    }

    private async Task CheckForWorkItemsWithoutEffortAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Check if notifications are enabled
        var settings = await mediator.Send(new GetEffortEstimationSettingsQuery(), cancellationToken);
        if (!settings.EnableProactiveNotifications)
        {
            _logger.LogDebug("Proactive notifications are disabled, skipping check");
            return;
        }

        // Find work items in "In Progress" or "Active" state without effort
        var workItemsWithoutEffort = await dbContext.WorkItems
            .Where(wi => (wi.State == "In Progress" || wi.State == "Active") &&
                         (!wi.Effort.HasValue || wi.Effort.Value == 0))
            .Select(wi => wi.TfsId)
            .ToListAsync(cancellationToken);

        if (workItemsWithoutEffort.Count > 0)
        {
            _logger.LogInformation("Found {Count} work items in progress without effort, sending notification", 
                workItemsWithoutEffort.Count);

            // Broadcast notification to all connected clients
            await _hubContext.Clients.All.SendAsync("WorkItemsWithoutEffort", workItemsWithoutEffort, cancellationToken);
        }
        else
        {
            _logger.LogDebug("No work items in progress without effort found");
        }
    }
}
