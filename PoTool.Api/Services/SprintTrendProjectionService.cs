using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.Domain.WorkItems;
using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;
using PoTool.Shared.Metrics;
using SharedEstimationMode = PoTool.Shared.Settings.EstimationMode;
using DomainEstimationMode = PoTool.Core.Domain.Models.EstimationMode;

namespace PoTool.Api.Services;

public class SprintTrendProjectionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SprintTrendProjectionService> _logger;
    private readonly IWorkItemStateClassificationService? _stateClassificationService;
    private readonly ICanonicalStoryPointResolutionService _storyPointResolutionService;
    private readonly IHierarchyRollupService _hierarchyRollupService;
    private readonly ISprintDeliveryProjectionService _deliveryTrendProjectionService;
    private readonly IDeliveryProgressRollupService _deliveryProgressRollupService;
    private readonly ISprintCommitmentService _sprintCommitmentService;
    private readonly ISprintCompletionService _sprintCompletionService;
    private readonly ISprintSpilloverService _sprintSpilloverService;
    private readonly PortfolioFlowProjectionService? _portfolioFlowProjectionService;

    public SprintTrendProjectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<SprintTrendProjectionService> logger,
        IWorkItemStateClassificationService? stateClassificationService,
        ICanonicalStoryPointResolutionService storyPointResolutionService,
        IHierarchyRollupService hierarchyRollupService,
        IDeliveryProgressRollupService deliveryProgressRollupService,
        ISprintCommitmentService sprintCommitmentService,
        ISprintCompletionService sprintCompletionService,
        ISprintSpilloverService sprintSpilloverService,
        ISprintDeliveryProjectionService deliveryTrendProjectionService,
        PortfolioFlowProjectionService? portfolioFlowProjectionService = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(storyPointResolutionService);
        ArgumentNullException.ThrowIfNull(hierarchyRollupService);
        ArgumentNullException.ThrowIfNull(deliveryProgressRollupService);
        ArgumentNullException.ThrowIfNull(sprintCommitmentService);
        ArgumentNullException.ThrowIfNull(sprintCompletionService);
        ArgumentNullException.ThrowIfNull(sprintSpilloverService);
        ArgumentNullException.ThrowIfNull(deliveryTrendProjectionService);

        _scopeFactory = scopeFactory;
        _logger = logger;
        _stateClassificationService = stateClassificationService;
        _storyPointResolutionService = storyPointResolutionService;
        _hierarchyRollupService = hierarchyRollupService;
        _deliveryProgressRollupService = deliveryProgressRollupService;
        _sprintCommitmentService = sprintCommitmentService;
        _sprintCompletionService = sprintCompletionService;
        _sprintSpilloverService = sprintSpilloverService;
        _deliveryTrendProjectionService = deliveryTrendProjectionService;
        _portfolioFlowProjectionService = portfolioFlowProjectionService;
    }

    public virtual async Task<IReadOnlyList<SprintMetricsProjectionEntity>> ComputeProjectionsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        CancellationToken cancellationToken = default)
        => await ComputeProjectionsAsync(productOwnerId, sprintIds, null, cancellationToken);

    public virtual async Task<IReadOnlyList<SprintMetricsProjectionEntity>> ComputeProjectionsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IReadOnlyList<int>? effectiveProductIds,
        CancellationToken cancellationToken)
    {
        var sprintIdList = sprintIds.Distinct().ToList();
        if (sprintIdList.Count == 0)
        {
            return Array.Empty<SprintMetricsProjectionEntity>();
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productIds = effectiveProductIds?.Count > 0
            ? effectiveProductIds.Distinct().ToList()
            : await context.Products
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
        LogInvalidTimeCriticalityOverrides(workItems);

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
        var firstDoneByWorkItem = _sprintCompletionService.BuildFirstDoneByWorkItem(stateFieldChanges, workItemSnapshotsById, stateLookup);
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
            var nextSprintPath = _sprintSpilloverService.GetNextSprintPath(sprintDefinition, teamSprintDefinitions);
            var committedWorkItemIds = _sprintCommitmentService.BuildCommittedWorkItemIds(
                workItemSnapshotsById,
                iterationEventsByWorkItem,
                sprintDefinition.Path,
                _sprintCommitmentService.GetCommitmentTimestamp(sprintStart));

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
                    committedWorkItemIds,
                    stateLookup,
                    firstDoneByWorkItem,
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

        if (_portfolioFlowProjectionService != null)
        {
            await _portfolioFlowProjectionService.ComputeProjectionsAsync(productOwnerId, sprintIdList, cancellationToken);
        }

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
        IReadOnlySet<int> committedWorkItemIds,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null,
        IReadOnlyDictionary<int, DateTimeOffset>? firstDoneByWorkItem = null,
        string? nextSprintPath = null,
        IReadOnlyDictionary<int, WorkItemSnapshot>? workItemSnapshotsById = null,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? stateEventsByWorkItem = null,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? iterationEventsByWorkItem = null)
    {
        ArgumentNullException.ThrowIfNull(committedWorkItemIds);

        var effectiveActivityByWorkItem = activityByWorkItem.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<FieldChangeEvent>)pair.Value.ToFieldChangeEvents());
        var effectiveWorkItemSnapshotsById = workItemSnapshotsById
            ?? workItemsByTfsId.Values.ToSnapshotDictionary();
        var effectiveIterationEventsByWorkItem = iterationEventsByWorkItem
            ?? effectiveActivityByWorkItem.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<FieldChangeEvent>)pair.Value
                    .Where(change => string.Equals(change.FieldRefName, "System.IterationPath", StringComparison.OrdinalIgnoreCase))
                    .ToList());

        var projection = _deliveryTrendProjectionService.Compute(new SprintDeliveryProjectionRequest(
            sprint.ToDefinition(),
            productId,
            resolvedItems.Select(resolvedItem => resolvedItem.ToDeliveryTrendResolvedWorkItem()).ToList(),
            workItemsByTfsId.ToDictionary(pair => pair.Key, pair => pair.Value.ToDeliveryTrendWorkItem()),
            effectiveActivityByWorkItem,
            sprintStart,
            sprintEnd,
            committedWorkItemIds,
            stateLookup,
            firstDoneByWorkItem,
            nextSprintPath,
            effectiveWorkItemSnapshotsById,
            stateEventsByWorkItem,
            effectiveIterationEventsByWorkItem));

        return ToProjectionEntity(projection);
    }

    internal double ComputeProgressionDelta(
        IReadOnlyList<ResolvedWorkItemEntity> productResolved,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyDictionary<int, List<ActivityEventLedgerEntryEntity>> activityByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        var progressionDelta = _deliveryProgressRollupService.ComputeProgressionDelta(new SprintDeliveryProgressionRequest(
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
                StateClassificationLookup.IsDone(stateLookup, CanonicalWorkItemTypes.Pbi, candidate.State)))
            .ToArray();

        return storyPointResolutionService.Resolve(new StoryPointResolutionRequest(
            pbi.ToCanonicalWorkItem(),
            StateClassificationLookup.IsDone(stateLookup, CanonicalWorkItemTypes.Pbi, pbi.State),
            candidates));
    }

    private static IReadOnlyList<CompletedPbiDto> BuildCompletedPbis(
        int featureId,
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyCollection<int>? sprintCompletedPbiIds,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        ICanonicalStoryPointResolutionService storyPointResolutionService)
    {
        if (sprintCompletedPbiIds == null)
        {
            return Array.Empty<CompletedPbiDto>();
        }

        var childPbis = resolvedItems
            .Where(resolvedItem => resolvedItem.WorkItemType.ToCanonicalWorkItemType() == CanonicalWorkItemTypes.Pbi
                && resolvedItem.ResolvedFeatureId == featureId)
            .Select(resolvedItem => workItemsByTfsId.GetValueOrDefault(resolvedItem.WorkItemId))
            .OfType<WorkItemEntity>()
            .ToList();

        return childPbis
            .Where(childPbi => sprintCompletedPbiIds.Contains(childPbi.TfsId))
            .Select(childPbi => new CompletedPbiDto
            {
                TfsId = childPbi.TfsId,
                Title = childPbi.Title,
                Effort = ResolvePbiStoryPointEstimate(childPbi, childPbis, stateLookup, storyPointResolutionService).Value ?? 0d,
                ClosedDate = childPbi.ClosedDate
            })
            .OrderBy(completedPbi => completedPbi.Title)
            .ToArray();
    }

    public virtual async Task<IReadOnlyList<SprintMetricsProjectionEntity>> GetProjectionsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        CancellationToken cancellationToken = default)
        => await GetProjectionsAsync(productOwnerId, sprintIds, null, cancellationToken);

    public virtual async Task<IReadOnlyList<SprintMetricsProjectionEntity>> GetProjectionsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IReadOnlyList<int>? effectiveProductIds,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productIds = effectiveProductIds?.Count > 0
            ? effectiveProductIds.Distinct().ToList()
            : await context.Products
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
        FeatureProgressMode progressMode,
        DateTime? sprintStartUtc = null,
        DateTime? sprintEndUtc = null,
        CancellationToken cancellationToken = default,
        int? sprintId = null)
        => await ComputeFeatureProgressForScopeAsync(
            productOwnerId,
            progressMode,
            sprintStartUtc,
            sprintEndUtc,
            cancellationToken,
            sprintId,
            null);

    public virtual async Task<IReadOnlyList<FeatureProgressDto>> ComputeFeatureProgressForScopeAsync(
        int productOwnerId,
        FeatureProgressMode progressMode,
        DateTime? sprintStartUtc,
        DateTime? sprintEndUtc,
        CancellationToken cancellationToken,
        int? sprintId,
        IReadOnlyList<int>? effectiveProductIds)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productSettings = await context.Products
            .Where(p => p.ProductOwnerId == productOwnerId
                && (effectiveProductIds == null || effectiveProductIds.Contains(p.Id)))
            .Select(p => new ProductEstimationSetting(p.Id, p.EstimationMode))
            .ToListAsync(cancellationToken);
        var productIds = productSettings.Select(product => product.Id).ToList();

        if (productIds.Count == 0)
        {
            return Array.Empty<FeatureProgressDto>();
        }

        LogNonDefaultEstimationModes(productSettings);

        var resolvedItems = await context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId != null && productIds.Contains(r.ResolvedProductId.Value))
            .ToListAsync(cancellationToken);

        var resolvedWorkItemIds = resolvedItems.Select(r => r.WorkItemId).ToHashSet();
        var workItems = await context.WorkItems
            .AsNoTracking()
            .Where(w => resolvedWorkItemIds.Contains(w.TfsId))
            .ToListAsync(cancellationToken);

        var workItemsByTfsId = workItems.ToDictionary(w => w.TfsId, w => w);
        LogInvalidTimeCriticalityOverrides(workItems);

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
                CanonicalWorkItemTypes.Pbi,
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
                    .Where(r => r.WorkItemType.ToCanonicalWorkItemType() == CanonicalWorkItemTypes.Pbi
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
            progressMode,
            _deliveryProgressRollupService,
            _storyPointResolutionService,
            activeWorkItemIds,
            sprintCompletedPbiIds,
            sprintEffortDeltaByWorkItem,
            sprintAssignedPbiIds,
            stateLookup);
    }

    public virtual async Task<IReadOnlyList<FeatureProgressDto>> ComputeFeatureProgressForProductsAsync(
        IReadOnlyList<int> productIds,
        FeatureProgressMode progressMode,
        DateTime? sprintStartUtc = null,
        DateTime? sprintEndUtc = null,
        CancellationToken cancellationToken = default,
        int? sprintId = null)
    {
        var effectiveProductIds = productIds
            .Distinct()
            .ToList();

        if (effectiveProductIds.Count == 0)
        {
            return Array.Empty<FeatureProgressDto>();
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productSettings = await context.Products
            .Where(p => effectiveProductIds.Contains(p.Id))
            .Select(p => new ProductEstimationSetting(p.Id, p.EstimationMode))
            .ToListAsync(cancellationToken);

        if (productSettings.Count == 0)
        {
            return Array.Empty<FeatureProgressDto>();
        }

        LogNonDefaultEstimationModes(productSettings);

        var resolvedItems = await context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId != null && effectiveProductIds.Contains(r.ResolvedProductId.Value))
            .ToListAsync(cancellationToken);

        var resolvedWorkItemIds = resolvedItems.Select(r => r.WorkItemId).ToHashSet();
        var workItems = await context.WorkItems
            .AsNoTracking()
            .Where(w => resolvedWorkItemIds.Contains(w.TfsId))
            .ToListAsync(cancellationToken);

        var workItemsByTfsId = workItems.ToDictionary(w => w.TfsId, w => w);
        LogInvalidTimeCriticalityOverrides(workItems);

        HashSet<int>? activeWorkItemIds = null;
        HashSet<int>? sprintCompletedPbiIds = null;
        IReadOnlyDictionary<int, int>? sprintEffortDeltaByWorkItem = null;
        HashSet<int>? sprintAssignedPbiIds = null;
        var stateLookup = await GetStateLookupAsync(cancellationToken);
        if (sprintStartUtc.HasValue && sprintEndUtc.HasValue)
        {
            var sprintStart = DateTime.SpecifyKind(sprintStartUtc.Value, DateTimeKind.Utc);
            var sprintEnd = DateTime.SpecifyKind(sprintEndUtc.Value, DateTimeKind.Utc);

            var sprintActivity = await context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => resolvedWorkItemIds.Contains(e.WorkItemId)
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
                CanonicalWorkItemTypes.Pbi,
                StateClassification.Done);

            var closedInSprint = await context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => resolvedWorkItemIds.Contains(e.WorkItemId)
                    && e.EventTimestampUtc >= sprintStart
                    && e.EventTimestampUtc <= sprintEnd
                    && e.FieldRefName == "System.State"
                    && donePbiStates.Contains(e.NewValue!))
                .Select(e => e.WorkItemId)
                .Distinct()
                .ToListAsync(cancellationToken);

            sprintCompletedPbiIds = closedInSprint.ToHashSet();

            var effortEvents = await context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => resolvedWorkItemIds.Contains(e.WorkItemId)
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

            if (sprintId.HasValue)
            {
                sprintAssignedPbiIds = resolvedItems
                    .Where(r => r.WorkItemType.ToCanonicalWorkItemType() == CanonicalWorkItemTypes.Pbi
                        && r.ResolvedSprintId == sprintId.Value
                        && r.ResolvedProductId != null
                        && effectiveProductIds.Contains(r.ResolvedProductId.Value))
                    .Select(r => r.WorkItemId)
                    .ToHashSet();
            }
        }

        return ComputeFeatureProgress(
            resolvedItems,
            workItemsByTfsId,
            effectiveProductIds,
            progressMode,
            _deliveryProgressRollupService,
            _storyPointResolutionService,
            activeWorkItemIds,
            sprintCompletedPbiIds,
            sprintEffortDeltaByWorkItem,
            sprintAssignedPbiIds,
            stateLookup);
    }

    private void LogNonDefaultEstimationModes(IEnumerable<ProductEstimationSetting> productSettings)
    {
        foreach (var product in productSettings)
        {
            var sharedMode = Enum.IsDefined(typeof(SharedEstimationMode), product.EstimationMode)
                ? (SharedEstimationMode)product.EstimationMode
                : SharedEstimationMode.StoryPoints;
            var domainMode = MapToDomainEstimationMode(sharedMode);

            if (domainMode == DomainEstimationMode.StoryPoints)
            {
                continue;
            }

            _logger.LogWarning(
                "Product {ProductId} is configured with EstimationMode {EstimationMode}; StoryPoints runtime calculation remains active until Phase B.",
                product.Id,
                domainMode);
        }
    }

    private static DomainEstimationMode MapToDomainEstimationMode(SharedEstimationMode mode)
        => mode switch
        {
            SharedEstimationMode.StoryPoints => DomainEstimationMode.StoryPoints,
            SharedEstimationMode.EffortHours => DomainEstimationMode.EffortHours,
            SharedEstimationMode.Mixed => DomainEstimationMode.Mixed,
            SharedEstimationMode.NoSpMode => DomainEstimationMode.NoSpMode,
            _ => DomainEstimationMode.StoryPoints
        };

    private void LogInvalidTimeCriticalityOverrides(IEnumerable<WorkItemEntity> workItems)
    {
        foreach (var workItem in workItems)
        {
            if (!WorkItemFieldSemantics.IsValidTimeCriticality(workItem.TimeCriticality))
            {
                _logger.LogWarning(
                    "Ignoring invalid TimeCriticality override for feature progress. WorkItemId: {WorkItemId}, Value: {TimeCriticality}",
                    workItem.TfsId,
                    workItem.TimeCriticality);
            }
        }
    }

    private readonly record struct ProductEstimationSetting(int Id, int EstimationMode);

    private const string EffortFieldRef = "Microsoft.VSTS.Scheduling.Effort";

    internal static IReadOnlyList<FeatureProgressDto> ComputeFeatureProgress(
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IReadOnlyList<int> productIds,
        FeatureProgressMode progressMode,
        IDeliveryProgressRollupService deliveryProgressRollupService,
        ICanonicalStoryPointResolutionService storyPointResolutionService,
        IReadOnlyCollection<int>? activeWorkItemIds = null,
        IReadOnlyCollection<int>? sprintCompletedPbiIds = null,
        IReadOnlyDictionary<int, int>? sprintEffortDeltaByWorkItem = null,
        IReadOnlyCollection<int>? sprintAssignedPbiIds = null,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        ArgumentNullException.ThrowIfNull(deliveryProgressRollupService);
        ArgumentNullException.ThrowIfNull(storyPointResolutionService);
        var deliveryTrendWorkItems = workItemsByTfsId.ToDictionary(pair => pair.Key, pair => pair.Value.ToDeliveryTrendWorkItem());
        var featureProgress = deliveryProgressRollupService.ComputeFeatureProgress(new DeliveryFeatureProgressRequest(
            resolvedItems.Select(resolvedItem => resolvedItem.ToDeliveryTrendResolvedWorkItem()).ToList(),
            deliveryTrendWorkItems,
            productIds,
            progressMode,
            activeWorkItemIds,
            sprintCompletedPbiIds,
            sprintEffortDeltaByWorkItem,
            sprintAssignedPbiIds,
            stateLookup));

        return featureProgress
            .Select(progress => progress.ToFeatureProgressDto(BuildCompletedPbis(
                progress.FeatureId,
                resolvedItems,
                workItemsByTfsId,
                sprintCompletedPbiIds,
                stateLookup,
                storyPointResolutionService)))
            .ToList();
    }

    /// <summary>
    /// Computes Epic-level progress from Feature progress data.
    /// Each Epic's progress is the story-point-weighted completion of its child Features.
    /// </summary>
    internal static IReadOnlyList<EpicProgressDto> ComputeEpicProgress(
        IReadOnlyList<FeatureProgressDto> featureProgress,
        IReadOnlyList<ResolvedWorkItemEntity> resolvedItems,
        IReadOnlyDictionary<int, WorkItemEntity> workItemsByTfsId,
        IDeliveryProgressRollupService deliveryProgressRollupService,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        ArgumentNullException.ThrowIfNull(deliveryProgressRollupService);
        var deliveryTrendWorkItems = workItemsByTfsId.ToDictionary(pair => pair.Key, pair => pair.Value.ToDeliveryTrendWorkItem());
        var epicProgress = deliveryProgressRollupService.ComputeEpicProgress(new DeliveryEpicProgressRequest(
            featureProgress.Select(progress => progress.ToFeatureProgress(deliveryTrendWorkItems)).ToList(),
            deliveryTrendWorkItems,
            stateLookup));

        return epicProgress
            .Select(progress => progress.ToEpicProgressDto())
            .ToList();
    }

    /// <summary>
    /// Computes Epic-level progress from the resolved work item hierarchy.
    /// </summary>
    public virtual async Task<IReadOnlyList<EpicProgressDto>> ComputeEpicProgressAsync(
        int productOwnerId,
        IReadOnlyList<FeatureProgressDto> featureProgress,
        CancellationToken cancellationToken = default)
        => await ComputeEpicProgressAsync(productOwnerId, featureProgress, cancellationToken, null);

    public virtual async Task<IReadOnlyList<EpicProgressDto>> ComputeEpicProgressAsync(
        int productOwnerId,
        IReadOnlyList<FeatureProgressDto> featureProgress,
        CancellationToken cancellationToken,
        IReadOnlyList<int>? effectiveProductIds)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productIds = await context.Products
            .Where(p => p.ProductOwnerId == productOwnerId
                && (effectiveProductIds == null || effectiveProductIds.Contains(p.Id)))
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
        return ComputeEpicProgress(featureProgress, resolvedItems, workItemsByTfsId, _deliveryProgressRollupService, stateLookup);
    }

    private async Task<IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>> GetStateLookupAsync(
        CancellationToken cancellationToken)
    {
        if (_stateClassificationService == null)
        {
            return StateClassificationLookup.Default;
        }

        var response = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
        return StateClassificationLookup.Create(response.Classifications.ToCanonicalDomainStateClassifications());
    }
}
