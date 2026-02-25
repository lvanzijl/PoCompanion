using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;

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

    public virtual async Task<IReadOnlyList<SprintMetricsProjectionEntity>> ComputeProjectionsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        CancellationToken cancellationToken = default)
    {
        var sprintIdList = sprintIds.Distinct().ToList();
        if (sprintIdList.Count == 0)
        {
            return Array.Empty<SprintMetricsProjectionEntity>();
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productIds = await context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (productIds.Count == 0)
        {
            _logger.LogWarning("No products found for ProductOwner {ProductOwnerId}", productOwnerId);
            return Array.Empty<SprintMetricsProjectionEntity>();
        }

        var sprints = await context.Sprints
            .Where(s => sprintIdList.Contains(s.Id))
            .ToListAsync(cancellationToken);

        if (sprints.Count == 0)
        {
            return Array.Empty<SprintMetricsProjectionEntity>();
        }

        var teamIds = sprints.Select(s => s.TeamId).Distinct().ToList();
        var allTeamSprints = await context.Sprints
            .Where(s => teamIds.Contains(s.TeamId) && s.StartDateUtc != null && s.EndDateUtc != null)
            .OrderBy(s => s.StartDateUtc)
            .ToListAsync(cancellationToken);

        var resolvedItems = await context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId != null && productIds.Contains(r.ResolvedProductId.Value))
            .ToListAsync(cancellationToken);

        var resolvedWorkItemIds = resolvedItems.Select(r => r.WorkItemId).ToHashSet();
        var workItems = await context.WorkItems
            .AsNoTracking()
            .Where(w => resolvedWorkItemIds.Contains(w.TfsId))
            .ToListAsync(cancellationToken);

        var workItemsByTfsId = workItems.ToDictionary(w => w.TfsId, w => w);

        var results = new List<SprintMetricsProjectionEntity>();

        foreach (var sprint in sprints)
        {
            if (sprint.StartDateUtc == null || sprint.EndDateUtc == null)
            {
                _logger.LogWarning("Sprint {SprintId} ({SprintName}) has no start/end dates, skipping", sprint.Id, sprint.Name);
                continue;
            }

            var sprintStart = new DateTimeOffset(sprint.StartDateUtc.Value, TimeSpan.Zero);
            var sprintEnd = new DateTimeOffset(sprint.EndDateUtc.Value, TimeSpan.Zero);

            var activityEvents = await context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => e.ProductOwnerId == productOwnerId
                    && e.EventTimestamp >= sprintStart
                    && e.EventTimestamp <= sprintEnd)
                .ToListAsync(cancellationToken);

            var activityByWorkItem = activityEvents
                .GroupBy(e => e.WorkItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var productId in productIds)
            {
                var projection = ComputeProductSprintProjection(
                    sprint, productId,
                    resolvedItems, workItemsByTfsId,
                    activityByWorkItem, sprintStart, sprintEnd);

                var existing = await context.SprintMetricsProjections
                    .FirstOrDefaultAsync(p => p.SprintId == sprint.Id && p.ProductId == productId, cancellationToken);

                if (existing != null)
                {
                    existing.PlannedCount = projection.PlannedCount;
                    existing.PlannedEffort = projection.PlannedEffort;
                    existing.WorkedCount = projection.WorkedCount;
                    existing.WorkedEffort = projection.WorkedEffort;
                    existing.BugsPlannedCount = projection.BugsPlannedCount;
                    existing.BugsWorkedCount = projection.BugsWorkedCount;
                    existing.CompletedPbiCount = projection.CompletedPbiCount;
                    existing.CompletedPbiEffort = projection.CompletedPbiEffort;
                    existing.ProgressionDelta = projection.ProgressionDelta;
                    existing.BugsCreatedCount = projection.BugsCreatedCount;
                    existing.BugsClosedCount = projection.BugsClosedCount;
                    existing.MissingEffortCount = projection.MissingEffortCount;
                    existing.IsApproximate = projection.IsApproximate;
                    existing.LastComputedAt = DateTimeOffset.UtcNow;
                    results.Add(existing);
                }
                else
                {
                    projection.LastComputedAt = DateTimeOffset.UtcNow;
                    context.SprintMetricsProjections.Add(projection);
                    results.Add(projection);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Computed {ProjectionCount} sprint trend projections for ProductOwner {ProductOwnerId}",
            results.Count, productOwnerId);

        return results;
    }

    internal static SprintMetricsProjectionEntity ComputeProductSprintProjection(
        SprintEntity sprint,
        int productId,
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<int, List<ActivityEventLedgerEntryEntity>> activityByWorkItem,
        DateTimeOffset sprintStart,
        DateTimeOffset sprintEnd)
    {
        var productResolved = resolvedItems
            .Where(r => r.ResolvedProductId == productId)
            .ToList();

        var productWorkItemIds = productResolved.Select(r => r.WorkItemId).ToHashSet();

        var workedItemIds = activityByWorkItem.Keys
            .Where(k => productWorkItemIds.Contains(k))
            .ToHashSet();

        // Bubble up activity from children to parents
        foreach (var resolved in productResolved)
        {
            if (resolved.WorkItemType is WorkItemType.Feature or WorkItemType.Epic or WorkItemType.Pbi or WorkItemType.Bug)
            {
                var hasChildActivity = productResolved
                    .Where(r => IsChildOf(r, resolved, workItemsByTfsId))
                    .Any(r => activityByWorkItem.ContainsKey(r.WorkItemId));

                if (hasChildActivity)
                {
                    workedItemIds.Add(resolved.WorkItemId);
                }
            }
        }

        // PBI metrics
        var pbiResolved = productResolved.Where(r => r.WorkItemType == WorkItemType.Pbi).ToList();
        var completedPbiCount = 0;
        var completedPbiEffort = 0;
        var missingEffortCount = 0;
        var isApproximate = false;

        foreach (var pbi in pbiResolved)
        {
            if (!workItemsByTfsId.TryGetValue(pbi.WorkItemId, out var wi))
                continue;

            var pbiActivity = activityByWorkItem.GetValueOrDefault(pbi.WorkItemId);
            var transitionedToDone = pbiActivity?.Any(e =>
                string.Equals(e.FieldRefName, "System.State", StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.NewValue, "Done", StringComparison.OrdinalIgnoreCase)) == true;

            if (transitionedToDone)
            {
                completedPbiCount++;
                completedPbiEffort += wi.Effort ?? 0;
            }

            if (wi.Effort == null)
            {
                missingEffortCount++;
            }
        }

        // Check if sibling-average approximation would be used
        if (missingEffortCount > 0)
        {
            var siblingEfforts = pbiResolved
                .Select(r => workItemsByTfsId.GetValueOrDefault(r.WorkItemId))
                .Where(w => w?.Effort != null)
                .Select(w => w!.Effort!.Value)
                .ToList();

            if (siblingEfforts.Count > 0)
            {
                isApproximate = true;
            }
        }

        // Feature/Epic progression delta
        var progressionDelta = ComputeProgressionDelta(
            productResolved, workItemsByTfsId, activityByWorkItem);

        // Bug metrics
        var bugResolved = productResolved.Where(r => r.WorkItemType == WorkItemType.Bug).ToList();
        var bugsCreated = 0;
        var bugsWorkedOn = 0;
        var bugsClosed = 0;

        foreach (var bug in bugResolved)
        {
            if (!workItemsByTfsId.TryGetValue(bug.WorkItemId, out var bugWi))
                continue;

            if (bugWi.CreatedDate.HasValue && bugWi.CreatedDate.Value >= sprintStart && bugWi.CreatedDate.Value <= sprintEnd)
            {
                bugsCreated++;
            }

            var bugActivity = activityByWorkItem.GetValueOrDefault(bug.WorkItemId);
            var bugClosedInSprint = bugActivity?.Any(e =>
                string.Equals(e.FieldRefName, "System.State", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(e.NewValue, "Done", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(e.NewValue, "Closed", StringComparison.OrdinalIgnoreCase))) == true;

            if (bugClosedInSprint)
            {
                bugsClosed++;
            }

            // Bug worked on = any child task had a state change during sprint
            var childTaskHadStateChange = productResolved
                .Where(r => r.WorkItemType == WorkItemType.Task)
                .Where(r =>
                {
                    if (!workItemsByTfsId.TryGetValue(r.WorkItemId, out var taskWi))
                        return false;
                    return taskWi.ParentTfsId == bug.WorkItemId;
                })
                .Any(ct => activityByWorkItem.GetValueOrDefault(ct.WorkItemId)
                    ?.Any(e => string.Equals(e.FieldRefName, "System.State", StringComparison.OrdinalIgnoreCase)) == true);

            if (childTaskHadStateChange)
            {
                bugsWorkedOn++;
            }
        }

        // Planned counts
        var plannedPbis = productResolved
            .Where(r => r.ResolvedSprintId == sprint.Id && r.WorkItemType == WorkItemType.Pbi)
            .Select(r => workItemsByTfsId.GetValueOrDefault(r.WorkItemId))
            .Where(w => w != null)
            .ToList();
        var plannedBugs = productResolved
            .Count(r => r.ResolvedSprintId == sprint.Id && r.WorkItemType == WorkItemType.Bug);

        return new SprintMetricsProjectionEntity
        {
            SprintId = sprint.Id,
            ProductId = productId,
            PlannedCount = plannedPbis.Count,
            PlannedEffort = plannedPbis.Sum(w => w!.Effort ?? 0),
            WorkedCount = workedItemIds.Count,
            WorkedEffort = workedItemIds
                .Select(id => workItemsByTfsId.GetValueOrDefault(id))
                .Where(w => w != null)
                .Sum(w => w!.Effort ?? 0),
            BugsPlannedCount = plannedBugs,
            BugsWorkedCount = bugsWorkedOn,
            CompletedPbiCount = completedPbiCount,
            CompletedPbiEffort = completedPbiEffort,
            ProgressionDelta = progressionDelta,
            BugsCreatedCount = bugsCreated,
            BugsClosedCount = bugsClosed,
            MissingEffortCount = missingEffortCount,
            IsApproximate = isApproximate,
            IncludedUpToRevisionId = 0
        };
    }

    internal static double ComputeProgressionDelta(
        IReadOnlyList<ResolvedWorkItemEntity> productResolved,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<int, List<ActivityEventLedgerEntryEntity>> activityByWorkItem)
    {
        var featureResolved = productResolved.Where(r => r.WorkItemType == WorkItemType.Feature).ToList();
        if (featureResolved.Count == 0) return 0;

        var totalFeatureProgression = 0.0;
        var featureCount = 0;

        foreach (var feature in featureResolved)
        {
            var childPbis = productResolved
                .Where(r => r.WorkItemType == WorkItemType.Pbi && r.ResolvedFeatureId == feature.WorkItemId)
                .Select(r => workItemsByTfsId.GetValueOrDefault(r.WorkItemId))
                .Where(w => w != null)
                .ToList();

            if (childPbis.Count == 0) continue;

            var totalEffort = 0;
            var doneEffort = 0;

            foreach (var pbi in childPbis)
            {
                var effort = pbi!.Effort ?? 0;
                if (pbi.Effort == null)
                {
                    var siblingAvg = childPbis
                        .Where(p => p!.Effort != null)
                        .Select(p => p!.Effort!.Value)
                        .DefaultIfEmpty(0)
                        .Average();
                    effort = (int)Math.Round(siblingAvg);
                }

                totalEffort += effort;
                if (string.Equals(pbi!.State, "Done", StringComparison.OrdinalIgnoreCase))
                {
                    doneEffort += effort;
                }
            }

            if (totalEffort > 0)
            {
                var featureHadActivity = childPbis.Any(p =>
                    activityByWorkItem.ContainsKey(p!.TfsId));

                if (featureHadActivity)
                {
                    var featurePercent = (double)doneEffort / totalEffort * 100;
                    totalFeatureProgression += featurePercent;
                    featureCount++;
                }
            }
        }

        return featureCount > 0 ? Math.Round(totalFeatureProgression / featureCount, 2) : 0;
    }

    private static bool IsChildOf(
        ResolvedWorkItemEntity candidate,
        ResolvedWorkItemEntity parent,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId)
    {
        if (!workItemsByTfsId.TryGetValue(candidate.WorkItemId, out var childWi))
            return false;

        return childWi.ParentTfsId == parent.WorkItemId;
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

    /// <summary>
    /// Computes feature-level progress from the resolved work item hierarchy.
    /// Returns one FeatureProgressDto per Feature that has child PBIs resolved to the PO's products.
    /// </summary>
    public virtual async Task<IReadOnlyList<FeatureProgressDto>> ComputeFeatureProgressAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productIds = await context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (productIds.Count == 0)
        {
            return Array.Empty<FeatureProgressDto>();
        }

        var resolvedItems = await context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId != null && productIds.Contains(r.ResolvedProductId.Value))
            .ToListAsync(cancellationToken);

        var resolvedWorkItemIds = resolvedItems.Select(r => r.WorkItemId).ToHashSet();
        var workItems = await context.WorkItems
            .AsNoTracking()
            .Where(w => resolvedWorkItemIds.Contains(w.TfsId))
            .ToListAsync(cancellationToken);

        var workItemsByTfsId = workItems.ToDictionary(w => w.TfsId, w => w);

        return ComputeFeatureProgress(resolvedItems, workItemsByTfsId, productIds);
    }

    internal static IReadOnlyList<FeatureProgressDto> ComputeFeatureProgress(
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyList<int> productIds)
    {
        var results = new List<FeatureProgressDto>();

        foreach (var productId in productIds)
        {
            var productResolved = resolvedItems
                .Where(r => r.ResolvedProductId == productId)
                .ToList();

            var features = productResolved
                .Where(r => r.WorkItemType == WorkItemType.Feature)
                .ToList();

            foreach (var feature in features)
            {
                if (!workItemsByTfsId.TryGetValue(feature.WorkItemId, out var featureWi))
                    continue;

                var childPbis = productResolved
                    .Where(r => r.WorkItemType == WorkItemType.Pbi && r.ResolvedFeatureId == feature.WorkItemId)
                    .Select(r => workItemsByTfsId.GetValueOrDefault(r.WorkItemId))
                    .Where(w => w != null)
                    .ToList();

                if (childPbis.Count == 0)
                    continue;

                var totalEffort = 0;
                var doneEffort = 0;

                foreach (var pbi in childPbis)
                {
                    var effort = pbi!.Effort ?? 0;
                    if (pbi.Effort == null)
                    {
                        var siblingAvg = childPbis
                            .Where(p => p!.Effort != null)
                            .Select(p => p!.Effort!.Value)
                            .DefaultIfEmpty(0)
                            .Average();
                        effort = (int)Math.Round(siblingAvg);
                    }

                    totalEffort += effort;
                    if (string.Equals(pbi!.State, "Done", StringComparison.OrdinalIgnoreCase))
                    {
                        doneEffort += effort;
                    }
                }

                var featureIsDone = string.Equals(featureWi.State, "Done", StringComparison.OrdinalIgnoreCase);
                var rawPercent = totalEffort > 0 ? (int)Math.Round((double)doneEffort / totalEffort * 100) : 0;
                var progressPercent = featureIsDone ? 100 : Math.Min(rawPercent, 90);

                // Resolve epic info
                int? epicId = feature.ResolvedEpicId;
                string? epicTitle = null;
                if (epicId.HasValue && workItemsByTfsId.TryGetValue(epicId.Value, out var epicWi))
                {
                    epicTitle = epicWi.Title;
                }

                results.Add(new FeatureProgressDto
                {
                    FeatureId = feature.WorkItemId,
                    FeatureTitle = featureWi.Title,
                    EpicId = epicId,
                    EpicTitle = epicTitle,
                    ProductId = productId,
                    ProgressPercent = progressPercent,
                    TotalEffort = totalEffort,
                    DoneEffort = doneEffort,
                    IsDone = featureIsDone
                });
            }
        }

        return results
            .OrderByDescending(f => f.ProgressPercent)
            .ThenBy(f => f.FeatureTitle)
            .ToList();
    }
}
