using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public class SprintTrendProjectionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SprintTrendProjectionService> _logger;
    private readonly IWorkItemStateClassificationService? _stateClassificationService;
    private readonly ICanonicalStoryPointResolutionService _storyPointResolutionService;
    private readonly IHierarchyRollupService _hierarchyRollupService;
    private readonly ISprintDeliveryProjectionService _deliveryTrendProjectionService;

    public SprintTrendProjectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<SprintTrendProjectionService> logger,
        ICanonicalStoryPointResolutionService storyPointResolutionService,
        IHierarchyRollupService hierarchyRollupService)
        : this(
            scopeFactory,
            logger,
            null,
            storyPointResolutionService,
            hierarchyRollupService,
            new SprintDeliveryProjectionService(storyPointResolutionService, hierarchyRollupService))
    {
    }

    public SprintTrendProjectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<SprintTrendProjectionService> logger,
        IWorkItemStateClassificationService? stateClassificationService,
        ICanonicalStoryPointResolutionService storyPointResolutionService,
        IHierarchyRollupService hierarchyRollupService)
        : this(
            scopeFactory,
            logger,
            stateClassificationService,
            storyPointResolutionService,
            hierarchyRollupService,
            new SprintDeliveryProjectionService(storyPointResolutionService, hierarchyRollupService))
    {
    }

    public SprintTrendProjectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<SprintTrendProjectionService> logger,
        IWorkItemStateClassificationService? stateClassificationService,
        ICanonicalStoryPointResolutionService storyPointResolutionService,
        IHierarchyRollupService hierarchyRollupService,
        ISprintDeliveryProjectionService deliveryTrendProjectionService)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(storyPointResolutionService);
        ArgumentNullException.ThrowIfNull(hierarchyRollupService);
        ArgumentNullException.ThrowIfNull(deliveryTrendProjectionService);

        _scopeFactory = scopeFactory;
        _logger = logger;
        _stateClassificationService = stateClassificationService;
        _storyPointResolutionService = storyPointResolutionService;
        _hierarchyRollupService = hierarchyRollupService;
        _deliveryTrendProjectionService = deliveryTrendProjectionService;
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

        var stateLookup = await GetStateLookupAsync(cancellationToken);

        // Batch-load all activity events for the full date range across all sprints
        var rangeStartUtc = validSprints.Min(s => DateTime.SpecifyKind(s.StartDateUtc!.Value, DateTimeKind.Utc));
        var rangeEndUtc = validSprints.Max(s => DateTime.SpecifyKind(s.EndDateUtc!.Value, DateTimeKind.Utc));
        var stateHistoryCutoffUtc = DateTime.UtcNow;

        var allActivityEvents = await context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(e => e.ProductOwnerId == productOwnerId
                && e.EventTimestampUtc >= rangeStartUtc
                && e.EventTimestampUtc <= rangeEndUtc)
            .ToListAsync(cancellationToken);

        var allStateEvents = await context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(e => e.ProductOwnerId == productOwnerId
                && e.FieldRefName == "System.State"
                && e.EventTimestampUtc <= stateHistoryCutoffUtc
                && resolvedWorkItemIds.Contains(e.WorkItemId))
            .ToListAsync(cancellationToken);

        var allIterationEvents = await context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(e => e.ProductOwnerId == productOwnerId
                && e.FieldRefName == "System.IterationPath"
                && e.EventTimestampUtc >= rangeStartUtc
                && resolvedWorkItemIds.Contains(e.WorkItemId))
            .ToListAsync(cancellationToken);

        // Batch-load existing projections for all sprint+product combinations
        var validSprintIds = validSprints.Select(s => s.Id).ToList();
        var existingProjections = await context.SprintMetricsProjections
            .Where(p => validSprintIds.Contains(p.SprintId) && productIds.Contains(p.ProductId))
            .ToListAsync(cancellationToken);

        var teamIds = validSprints.Select(s => s.TeamId).Distinct().ToList();
        var teamSprints = await context.Sprints
            .AsNoTracking()
            .Where(s => teamIds.Contains(s.TeamId))
            .ToListAsync(cancellationToken);

        var existingByKey = existingProjections
            .ToDictionary(p => (p.SprintId, p.ProductId), p => p);

        var workItemSnapshotsById = workItems.ToSnapshotDictionary();
        var stateFieldChanges = allStateEvents.ToFieldChangeEvents();
        var iterationFieldChanges = allIterationEvents.ToFieldChangeEvents();
        var stateEventsByWorkItem = stateFieldChanges.GroupByWorkItemId();
        var iterationEventsByWorkItem = iterationFieldChanges.GroupByWorkItemId();
        var firstDoneByWorkItem = FirstDoneDeliveryLookup.Build(stateFieldChanges, workItemSnapshotsById, stateLookup);
        var sprintDefinitionsById = validSprints.ToDictionary(sprint => sprint.Id, sprint => sprint.ToDefinition());
        var teamSprintDefinitions = teamSprints.Select(teamSprint => teamSprint.ToDefinition()).ToList();
        var deliveryTrendResolvedItems = resolvedItems
            .Select(resolvedItem => resolvedItem.ToDeliveryTrendResolvedWorkItem())
            .ToList();
        var deliveryTrendWorkItemsById = workItemsByTfsId
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToDeliveryTrendWorkItem());
        var results = new List<SprintMetricsProjectionEntity>();

        foreach (var sprint in validSprints)
        {
            var sprintStartUtc = DateTime.SpecifyKind(sprint.StartDateUtc!.Value, DateTimeKind.Utc);
            var sprintEndUtc = DateTime.SpecifyKind(sprint.EndDateUtc!.Value, DateTimeKind.Utc);
            var sprintStart = new DateTimeOffset(sprintStartUtc, TimeSpan.Zero);
            var sprintEnd = new DateTimeOffset(sprintEndUtc, TimeSpan.Zero);
            var sprintDefinition = sprintDefinitionsById[sprint.Id];
            var nextSprintPath = SprintSpilloverLookup.GetNextSprintPath(sprintDefinition, teamSprintDefinitions);
            var committedWorkItemIds = SprintCommitmentLookup.BuildCommittedWorkItemIds(
                workItemSnapshotsById,
                iterationEventsByWorkItem,
                sprintDefinition.Path,
                SprintCommitmentLookup.GetCommitmentTimestamp(sprintStart));

            // Filter the pre-loaded activity events to this sprint's date range
            var sprintActivity = allActivityEvents
                .Where(e => e.EventTimestampUtc >= sprintStartUtc && e.EventTimestampUtc <= sprintEndUtc)
                .ToList();

            var activityByWorkItem = sprintActivity
                .GroupBy(e => e.WorkItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var productId in productIds)
            {
                var projection = _deliveryTrendProjectionService.Compute(new SprintDeliveryProjectionRequest(
                    sprintDefinition,
                    productId,
                    deliveryTrendResolvedItems,
                    deliveryTrendWorkItemsById,
                    activityByWorkItem.ToDictionary(
                        pair => pair.Key,
                        pair => (IReadOnlyList<FieldChangeEvent>)pair.Value.ToFieldChangeEvents()),
                    sprintStart,
                    sprintEnd,
                    stateLookup,
                    firstDoneByWorkItem,
                    committedWorkItemIds,
                    nextSprintPath,
                    workItemSnapshotsById,
                    stateEventsByWorkItem,
                    iterationEventsByWorkItem));
                var projectionEntity = ToProjectionEntity(projection);

                if (existingByKey.TryGetValue((sprint.Id, productId), out var existing))
                {
                    ApplyProjection(existing, projectionEntity);
                    existing.LastComputedAt = DateTimeOffset.UtcNow;
                    results.Add(existing);
                }
                else
                {
                    projectionEntity.LastComputedAt = DateTimeOffset.UtcNow;
                    context.SprintMetricsProjections.Add(projectionEntity);
                    results.Add(projectionEntity);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Computed {ProjectionCount} sprint trend projections for ProductOwner {ProductOwnerId}",
            results.Count, productOwnerId);

        return results;
    }

    internal SprintMetricsProjectionEntity ComputeProductSprintProjection(
        SprintEntity sprint,
        int productId,
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<int, List<ActivityEventLedgerEntryEntity>> activityByWorkItem,
        DateTimeOffset sprintStart,
        DateTimeOffset sprintEnd,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null,
        IReadOnlyDictionary<int, DateTimeOffset>? firstDoneByWorkItem = null,
        IReadOnlySet<int>? committedWorkItemIds = null,
        string? nextSprintPath = null,
        IReadOnlyDictionary<int, WorkItemSnapshot>? workItemSnapshotsById = null,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? stateEventsByWorkItem = null,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? iterationEventsByWorkItem = null)
    {
        var projection = _deliveryTrendProjectionService.Compute(new SprintDeliveryProjectionRequest(
            sprint.ToDefinition(),
            productId,
            resolvedItems.Select(resolvedItem => resolvedItem.ToDeliveryTrendResolvedWorkItem()).ToList(),
            workItemsByTfsId.ToDictionary(pair => pair.Key, pair => pair.Value.ToDeliveryTrendWorkItem()),
            activityByWorkItem.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<FieldChangeEvent>)pair.Value.ToFieldChangeEvents()),
            sprintStart,
            sprintEnd,
            stateLookup,
            firstDoneByWorkItem,
            committedWorkItemIds,
            nextSprintPath,
            workItemSnapshotsById,
            stateEventsByWorkItem,
            iterationEventsByWorkItem));

        return ToProjectionEntity(projection);
    }

    internal double ComputeProgressionDelta(
        IReadOnlyList<ResolvedWorkItemEntity> productResolved,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<int, List<ActivityEventLedgerEntryEntity>> activityByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        var progressionDelta = _deliveryTrendProjectionService.ComputeProgressionDelta(new SprintDeliveryProgressionRequest(
            productResolved.Select(resolvedItem => resolvedItem.ToDeliveryTrendResolvedWorkItem()).ToList(),
            workItemsByTfsId.ToDictionary(pair => pair.Key, pair => pair.Value.ToDeliveryTrendWorkItem()),
            activityByWorkItem.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<FieldChangeEvent>)pair.Value.ToFieldChangeEvents()),
            stateLookup));

        return progressionDelta.Percentage;
    }

    private static SprintMetricsProjectionEntity ToProjectionEntity(SprintDeliveryProjection projection)
    {
        return new SprintMetricsProjectionEntity
        {
            SprintId = projection.SprintId,
            ProductId = projection.ProductId,
            PlannedCount = projection.PlannedCount,
            PlannedEffort = projection.PlannedEffort,
            PlannedStoryPoints = projection.PlannedStoryPoints,
            WorkedCount = projection.WorkedCount,
            WorkedEffort = projection.WorkedEffort,
            BugsPlannedCount = projection.BugsPlannedCount,
            BugsWorkedCount = projection.BugsWorkedCount,
            CompletedPbiCount = projection.CompletedPbiCount,
            CompletedPbiEffort = projection.CompletedPbiEffort,
            CompletedPbiStoryPoints = projection.CompletedPbiStoryPoints,
            SpilloverCount = projection.SpilloverCount,
            SpilloverEffort = projection.SpilloverEffort,
            SpilloverStoryPoints = projection.SpilloverStoryPoints,
            ProgressionDelta = projection.ProgressionDelta.Percentage,
            BugsCreatedCount = projection.BugsCreatedCount,
            BugsClosedCount = projection.BugsClosedCount,
            MissingEffortCount = projection.MissingEffortCount,
            MissingStoryPointCount = projection.MissingStoryPointCount,
            DerivedStoryPointCount = projection.DerivedStoryPointCount,
            DerivedStoryPoints = projection.DerivedStoryPoints,
            UnestimatedDeliveryCount = projection.UnestimatedDeliveryCount,
            IsApproximate = projection.IsApproximate,
            IncludedUpToRevisionId = 0
        };
    }

    private static void ApplyProjection(
        SprintMetricsProjectionEntity target,
        SprintMetricsProjectionEntity source)
    {
        target.PlannedCount = source.PlannedCount;
        target.PlannedEffort = source.PlannedEffort;
        target.PlannedStoryPoints = source.PlannedStoryPoints;
        target.WorkedCount = source.WorkedCount;
        target.WorkedEffort = source.WorkedEffort;
        target.BugsPlannedCount = source.BugsPlannedCount;
        target.BugsWorkedCount = source.BugsWorkedCount;
        target.CompletedPbiCount = source.CompletedPbiCount;
        target.CompletedPbiEffort = source.CompletedPbiEffort;
        target.CompletedPbiStoryPoints = source.CompletedPbiStoryPoints;
        target.SpilloverCount = source.SpilloverCount;
        target.SpilloverEffort = source.SpilloverEffort;
        target.SpilloverStoryPoints = source.SpilloverStoryPoints;
        target.ProgressionDelta = source.ProgressionDelta;
        target.BugsCreatedCount = source.BugsCreatedCount;
        target.BugsClosedCount = source.BugsClosedCount;
        target.MissingEffortCount = source.MissingEffortCount;
        target.MissingStoryPointCount = source.MissingStoryPointCount;
        target.DerivedStoryPointCount = source.DerivedStoryPointCount;
        target.DerivedStoryPoints = source.DerivedStoryPoints;
        target.UnestimatedDeliveryCount = source.UnestimatedDeliveryCount;
        target.IsApproximate = source.IsApproximate;
    }

    private static ResolvedStoryPointEstimate ResolvePbiStoryPointEstimate(
        WorkItemEntity pbi,
        IReadOnlyList<WorkItemEntity> featurePbis,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        ICanonicalStoryPointResolutionService storyPointResolutionService)
    {
        var candidates = featurePbis
            .Select(candidate => new StoryPointResolutionCandidate(
                candidate.ToCanonicalWorkItem(),
                StateClassificationLookup.IsDone(stateLookup, WorkItemType.Pbi, candidate.State)))
            .ToArray();

        return storyPointResolutionService.Resolve(new StoryPointResolutionRequest(
            pbi.ToCanonicalWorkItem(),
            StateClassificationLookup.IsDone(stateLookup, WorkItemType.Pbi, pbi.State),
            candidates));
    }

    private static (CanonicalWorkItem FeatureWorkItem, List<CanonicalWorkItem> FeatureWorkItems, Dictionary<int, bool> DoneByWorkItemId)
        BuildFeatureRollupContext(
            WorkItemEntity featureWi,
            IReadOnlyList<WorkItemEntity> childPbis,
            IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup)
    {
        var featureWorkItem = featureWi.ToCanonicalWorkItem();
        var featureWorkItems = new List<CanonicalWorkItem>(childPbis.Count + 1) { featureWorkItem };
        featureWorkItems.AddRange(childPbis.Select(childPbi => childPbi.ToCanonicalWorkItem()));
        var doneByWorkItemId = new Dictionary<int, bool>
        {
            [featureWi.TfsId] = StateClassificationLookup.IsDone(stateLookup, featureWi.Type, featureWi.State)
        };

        foreach (var childPbi in childPbis)
        {
            doneByWorkItemId[childPbi.TfsId] = StateClassificationLookup.IsDone(stateLookup, childPbi.Type, childPbi.State);
        }

        return (featureWorkItem, featureWorkItems, doneByWorkItemId);
    }

    private static HashSet<int> PropagateActivityToAncestors(
        IReadOnlyCollection<int> activeWorkItemIds,
        IReadOnlyCollection<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId)
    {
        var resolvedWorkItemIds = resolvedItems
            .Select(item => item.WorkItemId)
            .ToHashSet();
        var propagatedWorkItemIds = activeWorkItemIds
            .Where(resolvedWorkItemIds.Contains)
            .ToHashSet();
        var queue = new Queue<int>(propagatedWorkItemIds);

        while (queue.Count > 0)
        {
            var workItemId = queue.Dequeue();
            if (!workItemsByTfsId.TryGetValue(workItemId, out var workItem)
                || !workItem.ParentTfsId.HasValue
                || !resolvedWorkItemIds.Contains(workItem.ParentTfsId.Value))
            {
                continue;
            }

            if (propagatedWorkItemIds.Add(workItem.ParentTfsId.Value))
            {
                queue.Enqueue(workItem.ParentTfsId.Value);
            }
        }

        return propagatedWorkItemIds;
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
            _storyPointResolutionService,
            _hierarchyRollupService,
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
        ICanonicalStoryPointResolutionService storyPointResolutionService,
        IHierarchyRollupService hierarchyRollupService,
        IReadOnlyCollection<int>? activeWorkItemIds = null,
        IReadOnlyCollection<int>? sprintCompletedPbiIds = null,
        IReadOnlyDictionary<int, int>? sprintEffortDeltaByWorkItem = null,
        IReadOnlyCollection<int>? sprintAssignedPbiIds = null,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        var results = new List<FeatureProgressDto>();
        ArgumentNullException.ThrowIfNull(storyPointResolutionService);
        ArgumentNullException.ThrowIfNull(hierarchyRollupService);

        foreach (var productId in productIds)
        {
            var productResolved = resolvedItems
                .Where(r => r.ResolvedProductId == productId)
                .ToList();
            var activeHierarchyIds = activeWorkItemIds == null
                ? null
                : PropagateActivityToAncestors(activeWorkItemIds, productResolved, workItemsByTfsId);

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
                    .OfType<WorkItemEntity>()
                    .ToList();

                if (childPbis.Count == 0)
                    continue;

                // When an activity filter is provided, skip features that have neither sprint
                // activity events nor PBIs assigned to the sprint.
                if (activeHierarchyIds != null)
                {
                    var hasActivity = activeHierarchyIds.Contains(feature.WorkItemId);
                    var hasSprintPbis = sprintAssignedPbiIds != null
                        && childPbis.Any(pbi => sprintAssignedPbiIds.Contains(pbi.TfsId));
                    if (!hasActivity && !hasSprintPbis)
                        continue;
                }

                var (featureWorkItem, featureWorkItems, doneByWorkItemId) = BuildFeatureRollupContext(featureWi, childPbis, stateLookup);
                var featureScope = hierarchyRollupService.RollupCanonicalScope(featureWorkItem, featureWorkItems, doneByWorkItemId);
                var totalScopeStoryPoints = featureScope.Total;
                var doneScopeStoryPoints = featureScope.Completed;
                var donePbiCount = 0;

                foreach (var pbi in childPbis)
                {
                    if (StateClassificationLookup.IsDone(stateLookup, WorkItemType.Pbi, pbi.State))
                    {
                        donePbiCount++;
                    }
                }

                var featureIsDone = StateClassificationLookup.IsDone(stateLookup, WorkItemType.Feature, featureWi.State);
                var rawPercent = totalScopeStoryPoints > 0 ? (int)Math.Round((double)doneScopeStoryPoints / totalScopeStoryPoints * 100) : 0;
                var progressPercent = featureIsDone ? 100 : Math.Min(rawPercent, 90);

                // Sprint-scoped metrics
                var sprintCompletedScopeStoryPoints = sprintCompletedPbiIds == null
                    ? 0d
                    : childPbis
                        .Where(pbi => sprintCompletedPbiIds.Contains(pbi.TfsId))
                        .Sum(pbi => ResolvePbiStoryPointEstimate(pbi, childPbis, stateLookup, storyPointResolutionService).Value ?? 0d);

                var sprintCompletedPbiCount = sprintCompletedPbiIds == null
                    ? 0
                    : childPbis.Count(pbi => sprintCompletedPbiIds.Contains(pbi.TfsId));

                var featureCompletedInSprint = sprintCompletedPbiIds != null
                    && sprintCompletedPbiIds.Contains(feature.WorkItemId);

                var sprintProgressionDelta = totalScopeStoryPoints > 0
                    ? Math.Round((double)sprintCompletedScopeStoryPoints / totalScopeStoryPoints * 100, 2)
                    : 0.0;

                // Δ Effort = effort_end_of_sprint − effort_start_of_sprint for child PBIs
                var sprintEffortDelta = sprintEffortDeltaByWorkItem == null
                    ? 0
                    : childPbis.Sum(pbi => sprintEffortDeltaByWorkItem.GetValueOrDefault(pbi.TfsId, 0));

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
                        .Where(pbi => sprintCompletedPbiIds.Contains(pbi.TfsId))
                        .Select(pbi => new CompletedPbiDto
                        {
                            TfsId = pbi.TfsId,
                            Title = pbi.Title,
                            Effort = ResolvePbiStoryPointEstimate(pbi, childPbis, stateLookup, storyPointResolutionService).Value ?? 0d,
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
                    TotalEffort = totalScopeStoryPoints,
                    DoneEffort = doneScopeStoryPoints,
                    DonePbiCount = donePbiCount,
                    IsDone = featureIsDone,
                    SprintCompletedEffort = sprintCompletedScopeStoryPoints,
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
    /// Each Epic's progress is the story-point-weighted completion of its child Features.
    /// The DTO keeps legacy <c>*Effort</c> property names for API compatibility.
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
            var totalScopeStoryPoints = features.Sum(f => f.TotalEffort);
            var doneScopeStoryPoints = features.Sum(f => f.DoneEffort);
            var doneFeatureCount = features.Count(f => f.IsDone);
            var donePbiCount = features.Sum(f => f.DonePbiCount);
            var epicIsDone = StateClassificationLookup.IsDone(stateLookup, WorkItemType.Epic, epicWi.State);
            var rawPercent = totalScopeStoryPoints > 0 ? (int)Math.Round((double)doneScopeStoryPoints / totalScopeStoryPoints * 100) : 0;
            var progressPercent = epicIsDone ? 100 : Math.Min(rawPercent, 90);

            // Aggregate sprint-scoped metrics from child features
            var epicSprintCompletedScopeStoryPoints = features.Sum(f => f.SprintCompletedEffort);
            var epicSprintProgressionDelta = totalScopeStoryPoints > 0
                ? Math.Round((double)epicSprintCompletedScopeStoryPoints / totalScopeStoryPoints * 100, 2)
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
                TotalEffort = totalScopeStoryPoints,
                DoneEffort = doneScopeStoryPoints,
                FeatureCount = features.Count,
                DoneFeatureCount = doneFeatureCount,
                DonePbiCount = donePbiCount,
                IsDone = epicIsDone,
                SprintCompletedEffort = epicSprintCompletedScopeStoryPoints,
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
        return StateClassificationLookup.Create(response.Classifications.ToDomainStateClassifications());
    }
}
