using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

public class SprintTrendProjectionService
{
    private static readonly HashSet<string> ExcludedActivityFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.ChangedBy",
        "System.ChangedDate"
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SprintTrendProjectionService> _logger;
    private readonly IWorkItemStateClassificationService? _stateClassificationService;

    public SprintTrendProjectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<SprintTrendProjectionService> logger,
        IWorkItemStateClassificationService? stateClassificationService = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _stateClassificationService = stateClassificationService;
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

        var resolvedItems = await context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId != null && productIds.Contains(r.ResolvedProductId.Value))
            .ToListAsync(cancellationToken);

        var resolvedWorkItemIds = resolvedItems.Select(r => r.WorkItemId).ToHashSet();
        var workItems = await context.WorkItems
            .AsNoTracking()
            .Where(w => resolvedWorkItemIds.Contains(w.TfsId))
            .ToListAsync(cancellationToken);

        var workItemsByTfsId = workItems.ToDictionary(w => w.TfsId, w => w);

        // Filter to sprints with valid dates
        var validSprints = sprints
            .Where(s => s.StartDateUtc != null && s.EndDateUtc != null)
            .ToList();

        foreach (var s in sprints.Except(validSprints))
        {
            _logger.LogWarning("Sprint {SprintId} ({SprintName}) has no start/end dates, skipping", s.Id, s.Name);
        }

        if (validSprints.Count == 0)
        {
            return Array.Empty<SprintMetricsProjectionEntity>();
        }

        // Batch-load all activity events for the full date range across all sprints
        var rangeStartUtc = validSprints.Min(s => DateTime.SpecifyKind(s.StartDateUtc!.Value, DateTimeKind.Utc));
        var rangeEndUtc = validSprints.Max(s => DateTime.SpecifyKind(s.EndDateUtc!.Value, DateTimeKind.Utc));

        var allActivityEvents = await context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(e => e.ProductOwnerId == productOwnerId
                && e.EventTimestampUtc >= rangeStartUtc
                && e.EventTimestampUtc <= rangeEndUtc)
            .ToListAsync(cancellationToken);

        // Batch-load existing projections for all sprint+product combinations
        var validSprintIds = validSprints.Select(s => s.Id).ToList();
        var existingProjections = await context.SprintMetricsProjections
            .Where(p => validSprintIds.Contains(p.SprintId) && productIds.Contains(p.ProductId))
            .ToListAsync(cancellationToken);

        var existingByKey = existingProjections
            .ToDictionary(p => (p.SprintId, p.ProductId), p => p);

        var stateLookup = await GetStateLookupAsync(cancellationToken);
        var results = new List<SprintMetricsProjectionEntity>();

        foreach (var sprint in validSprints)
        {
            var sprintStartUtc = DateTime.SpecifyKind(sprint.StartDateUtc!.Value, DateTimeKind.Utc);
            var sprintEndUtc = DateTime.SpecifyKind(sprint.EndDateUtc!.Value, DateTimeKind.Utc);
            var sprintStart = new DateTimeOffset(sprintStartUtc, TimeSpan.Zero);
            var sprintEnd = new DateTimeOffset(sprintEndUtc, TimeSpan.Zero);

            // Filter the pre-loaded activity events to this sprint's date range
            var sprintActivity = allActivityEvents
                .Where(e => e.EventTimestampUtc >= sprintStartUtc && e.EventTimestampUtc <= sprintEndUtc)
                .ToList();

            var activityByWorkItem = sprintActivity
                .GroupBy(e => e.WorkItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var productId in productIds)
            {
                var projection = ComputeProductSprintProjection(
                    sprint, productId,
                    resolvedItems, workItemsByTfsId,
                    activityByWorkItem, sprintStart, sprintEnd, stateLookup);

                if (existingByKey.TryGetValue((sprint.Id, productId), out var existing))
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
        DateTimeOffset sprintEnd,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        var functionalActivityByWorkItem = activityByWorkItem
            .Select(pair => new
            {
                pair.Key,
                Events = pair.Value
                    .Where(e => !string.IsNullOrWhiteSpace(e.FieldRefName)
                        && !ExcludedActivityFields.Contains(e.FieldRefName))
                    .ToList()
            })
            .Where(x => x.Events.Count > 0)
            .ToDictionary(x => x.Key, x => x.Events);

        var productResolved = resolvedItems
            .Where(r => r.ResolvedProductId == productId)
            .ToList();

        var productWorkItemIds = productResolved.Select(r => r.WorkItemId).ToHashSet();

        var workedItemIds = functionalActivityByWorkItem.Keys
            .Where(k => productWorkItemIds.Contains(k))
            .ToHashSet();

        // Bubble up activity from children to parents
        foreach (var resolved in productResolved)
        {
            if (resolved.WorkItemType is WorkItemType.Feature or WorkItemType.Epic or WorkItemType.Pbi or WorkItemType.Bug)
            {
                var hasChildActivity = productResolved
                    .Where(r => IsChildOf(r, resolved, workItemsByTfsId))
                    .Any(r => functionalActivityByWorkItem.ContainsKey(r.WorkItemId));

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

            var pbiActivity = functionalActivityByWorkItem.GetValueOrDefault(pbi.WorkItemId);
            var transitionedToDone = pbiActivity?.Any(e =>
                string.Equals(e.FieldRefName, "System.State", StringComparison.OrdinalIgnoreCase)
                && StateClassificationLookup.IsDone(stateLookup, WorkItemType.Pbi, e.NewValue)) == true;

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
            productResolved, workItemsByTfsId, functionalActivityByWorkItem, stateLookup);

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

            var bugActivity = functionalActivityByWorkItem.GetValueOrDefault(bug.WorkItemId);
            var bugClosedInSprint = bugActivity?.Any(e =>
                string.Equals(e.FieldRefName, "System.State", StringComparison.OrdinalIgnoreCase)
                && StateClassificationLookup.IsDone(stateLookup, WorkItemType.Bug, e.NewValue)) == true;

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
                .Any(ct => functionalActivityByWorkItem.GetValueOrDefault(ct.WorkItemId)
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
        IReadOnlyDictionary<int, List<ActivityEventLedgerEntryEntity>> activityByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
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
                if (StateClassificationLookup.IsDone(stateLookup, WorkItemType.Pbi, pbi!.State))
                {
                    doneEffort += effort;
                }
            }

            if (totalEffort > 0)
            {
                var featureHadProgress = childPbis.Any(p =>
                    activityByWorkItem.TryGetValue(p!.TfsId, out var pbiEvents)
                    && pbiEvents.Any(e =>
                        string.Equals(e.FieldRefName, "System.State", StringComparison.OrdinalIgnoreCase)
                        && StateClassificationLookup.IsDone(stateLookup, WorkItemType.Pbi, e.NewValue)));

                if (featureHadProgress)
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
    /// When sprint dates are supplied, only features with functional activity during that sprint are returned.
    /// When <paramref name="sprintId"/> is supplied, features whose PBIs are assigned to the sprint
    /// are always included, even when no activity events exist for the sprint period.
    /// </summary>
    public virtual async Task<IReadOnlyList<FeatureProgressDto>> ComputeFeatureProgressAsync(
        int productOwnerId,
        DateTime? sprintStartUtc = null,
        DateTime? sprintEndUtc = null,
        CancellationToken cancellationToken = default,
        int? sprintId = null)
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

        HashSet<int>? activeWorkItemIds = null;
        HashSet<int>? sprintCompletedPbiIds = null;
        IReadOnlyDictionary<int, int>? sprintEffortDeltaByWorkItem = null;
        HashSet<int>? sprintAssignedPbiIds = null;
        var stateLookup = await GetStateLookupAsync(cancellationToken);
        if (sprintStartUtc.HasValue && sprintEndUtc.HasValue)
        {
            // Callers must supply UTC DateTime values; SpecifyKind guards against unspecified Kind
            var sprintStart = DateTime.SpecifyKind(sprintStartUtc.Value, DateTimeKind.Utc);
            var sprintEnd = DateTime.SpecifyKind(sprintEndUtc.Value, DateTimeKind.Utc);

            var sprintActivity = await context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => e.ProductOwnerId == productOwnerId
                    && e.EventTimestampUtc >= sprintStart
                    && e.EventTimestampUtc <= sprintEnd
                    && !string.IsNullOrEmpty(e.FieldRefName)
                    && e.FieldRefName != "System.ChangedBy"
                    && e.FieldRefName != "System.ChangedDate")
                .Select(e => e.WorkItemId)
                .Distinct()
                .ToListAsync(cancellationToken);

            activeWorkItemIds = sprintActivity.ToHashSet();

            var donePbiStates = StateClassificationLookup.GetStatesForClassification(
                stateLookup,
                WorkItemType.Pbi,
                StateClassification.Done);

            // Load PBI IDs that transitioned to Done during this sprint
            var closedInSprint = await context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => e.ProductOwnerId == productOwnerId
                    && e.EventTimestampUtc >= sprintStart
                    && e.EventTimestampUtc <= sprintEnd
                    && e.FieldRefName == "System.State"
                    && donePbiStates.Contains(e.NewValue!))
                .Select(e => e.WorkItemId)
                .Distinct()
                .ToListAsync(cancellationToken);

            sprintCompletedPbiIds = closedInSprint.ToHashSet();

            // Load effort change events to compute Δ Effort per work item
            var effortEvents = await context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => e.ProductOwnerId == productOwnerId
                    && e.EventTimestampUtc >= sprintStart
                    && e.EventTimestampUtc <= sprintEnd
                    && e.FieldRefName == EffortFieldRef)
                .Select(e => new { e.WorkItemId, e.OldValue, e.NewValue })
                .ToListAsync(cancellationToken);

            sprintEffortDeltaByWorkItem = effortEvents
                .GroupBy(e => e.WorkItemId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(e =>
                    {
                        var newVal = int.TryParse(e.NewValue, out var n) ? n : 0;
                        var oldVal = int.TryParse(e.OldValue, out var o) ? o : 0;
                        return newVal - oldVal;
                    }));

            // Load PBI IDs assigned to the sprint (by iteration path), regardless of activity.
            // These ensure epics/features remain visible when PBIs are in-sprint but not yet active.
            if (sprintId.HasValue)
            {
                sprintAssignedPbiIds = resolvedItems
                    .Where(r => r.WorkItemType == WorkItemType.Pbi
                        && r.ResolvedSprintId == sprintId.Value
                        && r.ResolvedProductId != null
                        && productIds.Contains(r.ResolvedProductId.Value))
                    .Select(r => r.WorkItemId)
                    .ToHashSet();
            }
        }

        return ComputeFeatureProgress(
            resolvedItems,
            workItemsByTfsId,
            productIds,
            activeWorkItemIds,
            sprintCompletedPbiIds,
            sprintEffortDeltaByWorkItem,
            sprintAssignedPbiIds,
            stateLookup);
    }

    private const string EffortFieldRef = "Microsoft.VSTS.Scheduling.Effort";

    internal static IReadOnlyList<FeatureProgressDto> ComputeFeatureProgress(
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyList<int> productIds,
        IReadOnlyCollection<int>? activeWorkItemIds = null,
        IReadOnlyCollection<int>? sprintCompletedPbiIds = null,
        IReadOnlyDictionary<int, int>? sprintEffortDeltaByWorkItem = null,
        IReadOnlyCollection<int>? sprintAssignedPbiIds = null,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
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

                // When an activity filter is provided, skip features that have neither sprint
                // activity events nor PBIs assigned to the sprint.
                if (activeWorkItemIds != null)
                {
                    var hasActivity = activeWorkItemIds.Contains(feature.WorkItemId)
                        || childPbis.Any(pbi => activeWorkItemIds.Contains(pbi!.TfsId));
                    var hasSprintPbis = sprintAssignedPbiIds != null
                        && childPbis.Any(pbi => sprintAssignedPbiIds.Contains(pbi!.TfsId));
                    if (!hasActivity && !hasSprintPbis)
                        continue;
                }

                var totalEffort = 0;
                var doneEffort = 0;
                var donePbiCount = 0;

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
                    if (StateClassificationLookup.IsDone(stateLookup, WorkItemType.Pbi, pbi!.State))
                    {
                        doneEffort += effort;
                        donePbiCount++;
                    }
                }

                var featureIsDone = StateClassificationLookup.IsDone(stateLookup, WorkItemType.Feature, featureWi.State);
                var rawPercent = totalEffort > 0 ? (int)Math.Round((double)doneEffort / totalEffort * 100) : 0;
                var progressPercent = featureIsDone ? 100 : Math.Min(rawPercent, 90);

                // Sprint-scoped metrics
                var sprintCompletedEffort = sprintCompletedPbiIds == null
                    ? 0
                    : childPbis
                        .Where(pbi => sprintCompletedPbiIds.Contains(pbi!.TfsId))
                        .Sum(pbi => pbi!.Effort ?? 0);

                var sprintCompletedPbiCount = sprintCompletedPbiIds == null
                    ? 0
                    : childPbis.Count(pbi => sprintCompletedPbiIds.Contains(pbi!.TfsId));

                var featureCompletedInSprint = sprintCompletedPbiIds != null
                    && sprintCompletedPbiIds.Contains(feature.WorkItemId);

                var sprintProgressionDelta = totalEffort > 0
                    ? Math.Round((double)sprintCompletedEffort / totalEffort * 100, 2)
                    : 0.0;

                // Δ Effort = effort_end_of_sprint − effort_start_of_sprint for child PBIs
                var sprintEffortDelta = sprintEffortDeltaByWorkItem == null
                    ? 0
                    : childPbis.Sum(pbi => sprintEffortDeltaByWorkItem.GetValueOrDefault(pbi!.TfsId, 0));

                // Resolve epic info
                int? epicId = feature.ResolvedEpicId;
                string? epicTitle = null;
                if (epicId.HasValue && workItemsByTfsId.TryGetValue(epicId.Value, out var epicWi))
                {
                    epicTitle = epicWi.Title;
                }

                // Build individual completed PBI details for drill-down
                var completedPbis = sprintCompletedPbiIds == null
                    ? Array.Empty<CompletedPbiDto>()
                    : childPbis
                        .Where(pbi => sprintCompletedPbiIds.Contains(pbi!.TfsId))
                        .Select(pbi => new CompletedPbiDto
                        {
                            TfsId = pbi!.TfsId,
                            Title = pbi.Title,
                            Effort = pbi.Effort ?? 0,
                            ClosedDate = pbi.ClosedDate
                        })
                        .OrderBy(p => p.Title)
                        .ToArray();

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
                    DonePbiCount = donePbiCount,
                    IsDone = featureIsDone,
                    SprintCompletedEffort = sprintCompletedEffort,
                    SprintProgressionDelta = sprintProgressionDelta,
                    SprintEffortDelta = sprintEffortDelta,
                    SprintCompletedPbiCount = sprintCompletedPbiCount,
                    SprintCompletedInSprint = featureCompletedInSprint,
                    CompletedPbis = completedPbis
                });
            }
        }

        return results
            .OrderByDescending(f => f.ProgressPercent)
            .ThenBy(f => f.FeatureTitle)
            .ToList();
    }

    /// <summary>
    /// Computes Epic-level progress from Feature progress data.
    /// Each Epic's progress is the effort-weighted completion of its child Features.
    /// </summary>
    internal static IReadOnlyList<EpicProgressDto> ComputeEpicProgress(
        IReadOnlyList<FeatureProgressDto> featureProgress,
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        // Group features by their parent epic
        var featuresByEpic = featureProgress
            .Where(f => f.EpicId.HasValue)
            .GroupBy(f => f.EpicId!.Value)
            .ToList();

        var results = new List<EpicProgressDto>();

        foreach (var group in featuresByEpic)
        {
            var epicTfsId = group.Key;
            if (!workItemsByTfsId.TryGetValue(epicTfsId, out var epicWi))
                continue;

            var features = group.ToList();
            var totalEffort = features.Sum(f => f.TotalEffort);
            var doneEffort = features.Sum(f => f.DoneEffort);
            var doneFeatureCount = features.Count(f => f.IsDone);
            var donePbiCount = features.Sum(f => f.DonePbiCount);
            var epicIsDone = StateClassificationLookup.IsDone(stateLookup, WorkItemType.Epic, epicWi.State);
            var rawPercent = totalEffort > 0 ? (int)Math.Round((double)doneEffort / totalEffort * 100) : 0;
            var progressPercent = epicIsDone ? 100 : Math.Min(rawPercent, 90);

            // Aggregate sprint-scoped metrics from child features
            var epicSprintCompletedEffort = features.Sum(f => f.SprintCompletedEffort);
            var epicSprintProgressionDelta = totalEffort > 0
                ? Math.Round((double)epicSprintCompletedEffort / totalEffort * 100, 2)
                : 0.0;
            var epicSprintEffortDelta = features.Sum(f => f.SprintEffortDelta);
            var epicSprintCompletedPbiCount = features.Sum(f => f.SprintCompletedPbiCount);
            var epicSprintCompletedFeatureCount = features.Count(f => f.SprintCompletedInSprint);

            // Get the product ID from the first feature (all should share the same product)
            var productId = features.First().ProductId;

            results.Add(new EpicProgressDto
            {
                EpicId = epicTfsId,
                EpicTitle = epicWi.Title,
                ProductId = productId,
                ProgressPercent = progressPercent,
                TotalEffort = totalEffort,
                DoneEffort = doneEffort,
                FeatureCount = features.Count,
                DoneFeatureCount = doneFeatureCount,
                DonePbiCount = donePbiCount,
                IsDone = epicIsDone,
                SprintCompletedEffort = epicSprintCompletedEffort,
                SprintProgressionDelta = epicSprintProgressionDelta,
                SprintEffortDelta = epicSprintEffortDelta,
                SprintCompletedPbiCount = epicSprintCompletedPbiCount,
                SprintCompletedFeatureCount = epicSprintCompletedFeatureCount
            });
        }

        return results
            .OrderByDescending(e => e.ProgressPercent)
            .ThenBy(e => e.EpicTitle)
            .ToList();
    }

    /// <summary>
    /// Computes Epic-level progress from the resolved work item hierarchy.
    /// </summary>
    public virtual async Task<IReadOnlyList<EpicProgressDto>> ComputeEpicProgressAsync(
        int productOwnerId,
        IReadOnlyList<FeatureProgressDto> featureProgress,
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
            return Array.Empty<EpicProgressDto>();
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

        var stateLookup = await GetStateLookupAsync(cancellationToken);
        return ComputeEpicProgress(featureProgress, resolvedItems, workItemsByTfsId, stateLookup);
    }

    private async Task<IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>> GetStateLookupAsync(
        CancellationToken cancellationToken)
    {
        if (_stateClassificationService == null)
        {
            return StateClassificationLookup.Default;
        }

        var response = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
        return StateClassificationLookup.Create(response.Classifications);
    }
}
