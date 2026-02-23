using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

public class SprintTrendProjectionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SprintTrendProjectionService> _logger;

    public SprintTrendProjectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<SprintTrendProjectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public virtual Task<IReadOnlyList<SprintMetricsProjectionEntity>> ComputeProjectionsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        CancellationToken cancellationToken = default)
    {
        _ = productOwnerId;
        _ = sprintIds;
        _ = cancellationToken;
        // REPLACE_WITH_ACTIVITY_SOURCE: compute sprint trend metrics from activity events.
        return Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(Array.Empty<SprintMetricsProjectionEntity>());
    }

    public virtual async Task<IReadOnlyList<SprintMetricsProjectionEntity>> GetProjectionsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productIds = await context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var sprintIdList = sprintIds.Distinct().ToList();
        var projections = await context.SprintMetricsProjections
            .Where(p => sprintIdList.Contains(p.SprintId) && productIds.Contains(p.ProductId))
            .Include(p => p.Sprint)
            .Include(p => p.Product)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {ProjectionCount} cached sprint trend projections for ProductOwner {ProductOwnerId} across {SprintCount} requested sprints.",
            projections.Count, productOwnerId, sprintIdList.Count);

        return projections;
    }

    internal bool IsQualifyingActivity(
        string workItemType,
        StateClassification? oldClass,
        StateClassification? newClass)
    {
        if (oldClass == null || newClass == null || oldClass == newClass)
        {
            return false;
        }

        if (workItemType.Equals("Task", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (workItemType.Equals("Product Backlog Item", StringComparison.OrdinalIgnoreCase) ||
            workItemType.Equals("PBI", StringComparison.OrdinalIgnoreCase))
        {
            if (oldClass == StateClassification.New && newClass == StateClassification.InProgress)
            {
                return false;
            }

            return oldClass == StateClassification.InProgress && newClass == StateClassification.Done;
        }

        if (workItemType.Equals("Bug", StringComparison.OrdinalIgnoreCase))
        {
            return (oldClass == StateClassification.InProgress && newClass == StateClassification.Done) ||
                   (oldClass == StateClassification.Done && newClass == StateClassification.InProgress);
        }

        return false;
    }
}
