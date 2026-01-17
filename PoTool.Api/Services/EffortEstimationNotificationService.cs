using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Settings.Queries;
using Mediator;

namespace PoTool.Api.Services;

/// <summary>
/// Background service that monitors work items without effort and sends proactive notifications.
/// NOTE: SignalR hub removed in Live-only mode. This service is currently inactive.
/// </summary>
public class EffortEstimationNotificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EffortEstimationNotificationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public EffortEstimationNotificationService(
        IServiceProvider serviceProvider,
        ILogger<EffortEstimationNotificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Effort Estimation Notification Service is starting (inactive in Live-only mode)");

        // Service inactive in Live-only mode - no SignalR hub available
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
