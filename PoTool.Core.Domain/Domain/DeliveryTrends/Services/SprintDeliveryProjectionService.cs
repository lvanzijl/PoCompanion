using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.WorkItems;
namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Computes canonical sprint delivery projections from prepared delivery-trend inputs.
/// </summary>
public interface ISprintDeliveryProjectionService
{
    /// <summary>
    /// Computes the canonical projection for one sprint/product combination.
    /// </summary>
    SprintDeliveryProjection Compute(SprintDeliveryProjectionRequest request);

    /// <summary>
    /// Computes the average feature progression delta for sprint activity.
    /// </summary>
    ProgressionDelta ComputeProgressionDelta(SprintDeliveryProgressionRequest request);
}

/// <summary>
/// Implements canonical sprint delivery projection formulas independent of API orchestration and persistence.
/// </summary>
public sealed class SprintDeliveryProjectionService : ISprintDeliveryProjectionService
{
    private static readonly HashSet<string> ExcludedActivityFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.ChangedBy",
        "System.ChangedDate"
    };

    private readonly ICanonicalStoryPointResolutionService _storyPointResolutionService;
    private readonly IHierarchyRollupService _hierarchyRollupService;
    private readonly IDeliveryProgressRollupService _deliveryProgressRollupService;
    private readonly ISprintCompletionService _sprintCompletionService;
    private readonly ISprintSpilloverService _sprintSpilloverService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SprintDeliveryProjectionService"/> class.
    /// </summary>
    public SprintDeliveryProjectionService(
        ICanonicalStoryPointResolutionService storyPointResolutionService,
        IHierarchyRollupService hierarchyRollupService,
        IDeliveryProgressRollupService deliveryProgressRollupService,
        ISprintCompletionService sprintCompletionService,
        ISprintSpilloverService sprintSpilloverService)
    {
        _storyPointResolutionService = storyPointResolutionService ?? throw new ArgumentNullException(nameof(storyPointResolutionService));
        _hierarchyRollupService = hierarchyRollupService ?? throw new ArgumentNullException(nameof(hierarchyRollupService));
        _deliveryProgressRollupService = deliveryProgressRollupService ?? throw new ArgumentNullException(nameof(deliveryProgressRollupService));
        _sprintCompletionService = sprintCompletionService ?? throw new ArgumentNullException(nameof(sprintCompletionService));
        _sprintSpilloverService = sprintSpilloverService ?? throw new ArgumentNullException(nameof(sprintSpilloverService));
    }

    /// <inheritdoc />
    public SprintDeliveryProjection Compute(SprintDeliveryProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CommittedWorkItemIds);

        var effectiveWorkItemSnapshotsById = request.WorkItemSnapshotsById
            ?? request.WorkItemsById.Values.ToDictionary(workItem => workItem.WorkItemId, ToSnapshot);
        var effectiveFirstDoneByWorkItem = request.FirstDoneByWorkItem
            ?? _sprintCompletionService.BuildFirstDoneByWorkItem(
                request.ActivityByWorkItem.Values.SelectMany(events => events),
                effectiveWorkItemSnapshotsById,
                request.StateLookup);

        var functionalActivityByWorkItem = request.ActivityByWorkItem
            .Select(pair => new
            {
                pair.Key,
                Events = pair.Value
                    .Where(activityEvent => activityEvent.Timestamp >= request.SprintStart && activityEvent.Timestamp <= request.SprintEnd)
                    .Where(activityEvent => !string.IsNullOrWhiteSpace(activityEvent.FieldRefName)
                        && !ExcludedActivityFields.Contains(activityEvent.FieldRefName))
                    .ToList()
            })
            .Where(entry => entry.Events.Count > 0)
            .ToDictionary(entry => entry.Key, entry => (IReadOnlyList<FieldChangeEvent>)entry.Events);

        var productResolved = request.ResolvedItems
            .Where(resolvedItem => resolvedItem.ResolvedProductId == request.ProductId)
            .ToList();

        var productWorkItemIds = productResolved.Select(resolvedItem => resolvedItem.WorkItemId).ToHashSet();

        var workedItemIds = DeliveryProgressRollupMath.PropagateActivityToAncestors(
            functionalActivityByWorkItem.Keys.Where(productWorkItemIds.Contains).ToHashSet(),
            productResolved,
            request.WorkItemsById);

        var pbiResolved = productResolved.Where(resolvedItem => CanonicalWorkItemTypes.IsAuthoritativePbi(resolvedItem.WorkItemType)).ToList();
        var completedPbiCount = 0;
        var completedPbiEffort = 0;
        var missingEffortCount = 0;
        var isApproximate = false;
        var completedPbiStoryPoints = 0d;
        var unestimatedDeliveryCount = 0;
        var usedDerivedDeliveryEstimate = false;

        foreach (var pbi in pbiResolved)
        {
            if (!request.WorkItemsById.TryGetValue(pbi.WorkItemId, out var workItem))
            {
                continue;
            }

            var deliveredInSprint = effectiveFirstDoneByWorkItem.TryGetValue(pbi.WorkItemId, out var firstDoneTimestamp)
                && firstDoneTimestamp >= request.SprintStart
                && firstDoneTimestamp <= request.SprintEnd;

            if (deliveredInSprint)
            {
                completedPbiCount++;
                completedPbiEffort += workItem.Effort ?? 0;

                var deliveredEstimate = ResolvePbiStoryPointEstimate(
                    workItem,
                    pbiResolved
                        .Where(resolvedItem => resolvedItem.ResolvedFeatureId == pbi.ResolvedFeatureId)
                        .Select(resolvedItem => request.WorkItemsById.GetValueOrDefault(resolvedItem.WorkItemId))
                        .OfType<DeliveryTrendWorkItem>()
                        .ToList(),
                    request.StateLookup);

                if (IsVelocityStoryPointEstimate(deliveredEstimate))
                {
                    completedPbiStoryPoints += deliveredEstimate.Value ?? 0d;
                }
                else
                {
                    unestimatedDeliveryCount++;
                    usedDerivedDeliveryEstimate |= deliveredEstimate.Source == StoryPointEstimateSource.Derived;
                }
            }

            if (workItem.Effort == null)
            {
                missingEffortCount++;
            }
        }

        if (missingEffortCount > 0)
        {
            var siblingEfforts = pbiResolved
                .Select(resolvedItem => request.WorkItemsById.GetValueOrDefault(resolvedItem.WorkItemId))
                .Where(workItem => workItem?.Effort != null)
                .Select(workItem => workItem!.Effort!.Value)
                .ToList();

            if (siblingEfforts.Count > 0)
            {
                isApproximate = true;
            }
        }

        var effectiveStateEventsByWorkItem = request.StateEventsByWorkItem
            ?? request.ActivityByWorkItem
                .SelectMany(pair => pair.Value
                    .Where(activityEvent => string.Equals(activityEvent.FieldRefName, "System.State", StringComparison.OrdinalIgnoreCase)))
                .GroupByWorkItemId();
        var effectiveIterationEventsByWorkItem = request.IterationEventsByWorkItem
            ?? request.ActivityByWorkItem
                .SelectMany(pair => pair.Value
                    .Where(activityEvent => string.Equals(activityEvent.FieldRefName, "System.IterationPath", StringComparison.OrdinalIgnoreCase)))
                .GroupByWorkItemId();
        var spilloverWorkItemIds = _sprintSpilloverService.BuildSpilloverWorkItemIds(
            request.CommittedWorkItemIds,
            effectiveWorkItemSnapshotsById,
            effectiveStateEventsByWorkItem,
            effectiveIterationEventsByWorkItem,
            request.StateLookup,
            request.Sprint,
            request.NextSprintPath,
            request.SprintEnd);
        var spilloverPbis = pbiResolved
            .Where(resolvedItem => spilloverWorkItemIds.Contains(resolvedItem.WorkItemId))
            .Select(resolvedItem => request.WorkItemsById.GetValueOrDefault(resolvedItem.WorkItemId))
            .Where(workItem => workItem != null)
            .ToList();

        var progressionDelta = _deliveryProgressRollupService.ComputeProgressionDelta(new SprintDeliveryProgressionRequest(
            productResolved,
            request.WorkItemsById,
            functionalActivityByWorkItem,
            request.StateLookup));

        var bugResolved = productResolved.Where(resolvedItem => resolvedItem.WorkItemType == CanonicalWorkItemTypes.Bug).ToList();
        var bugsCreated = 0;
        var bugsWorkedOn = 0;
        var bugsClosed = 0;

        foreach (var bug in bugResolved)
        {
            if (!request.WorkItemsById.TryGetValue(bug.WorkItemId, out var bugWorkItem))
            {
                continue;
            }

            if (bugWorkItem.CreatedDate.HasValue
                && bugWorkItem.CreatedDate.Value >= request.SprintStart
                && bugWorkItem.CreatedDate.Value <= request.SprintEnd)
            {
                bugsCreated++;
            }

            var bugClosedInSprint = effectiveFirstDoneByWorkItem.TryGetValue(bug.WorkItemId, out var bugFirstDoneTimestamp)
                && bugFirstDoneTimestamp >= request.SprintStart
                && bugFirstDoneTimestamp <= request.SprintEnd;

            if (bugClosedInSprint)
            {
                bugsClosed++;
            }

            var childTaskHadStateChange = productResolved
                .Where(resolvedItem => resolvedItem.WorkItemType == CanonicalWorkItemTypes.Task)
                .Where(resolvedItem =>
                {
                    if (!request.WorkItemsById.TryGetValue(resolvedItem.WorkItemId, out var taskWorkItem))
                    {
                        return false;
                    }

                    return taskWorkItem.ParentWorkItemId == bug.WorkItemId;
                })
                .Any(childTask => functionalActivityByWorkItem.GetValueOrDefault(childTask.WorkItemId)
                    ?.Any(activityEvent => string.Equals(activityEvent.FieldRefName, "System.State", StringComparison.OrdinalIgnoreCase)) == true);

            if (childTaskHadStateChange)
            {
                bugsWorkedOn++;
            }
        }

        var plannedPbis = productResolved
            .Where(resolvedItem => CanonicalWorkItemTypes.IsAuthoritativePbi(resolvedItem.WorkItemType)
                && request.CommittedWorkItemIds.Contains(resolvedItem.WorkItemId))
            .Select(resolvedItem => request.WorkItemsById.GetValueOrDefault(resolvedItem.WorkItemId))
            .Where(workItem => workItem != null)
            .ToList();
        var plannedStoryPointMetrics = plannedPbis
            .Select(workItem => ResolveProjectionStoryPointMetrics(workItem!, pbiResolved, request.WorkItemsById, request.StateLookup))
            .ToList();
        var spilloverStoryPoints = spilloverPbis
            .Select(workItem => ResolveProjectionStoryPointMetrics(workItem!, pbiResolved, request.WorkItemsById, request.StateLookup))
            .Where(metric => metric.Estimate.HasValue)
            .Sum(metric => metric.Estimate.Value ?? 0d);
        var plannedBugs = productResolved
            .Count(resolvedItem => resolvedItem.WorkItemType == CanonicalWorkItemTypes.Bug
                && request.CommittedWorkItemIds.Contains(resolvedItem.WorkItemId));

        var derivedStoryPointCount = plannedStoryPointMetrics.Count(metric => metric.Estimate.Source == StoryPointEstimateSource.Derived);
        var derivedStoryPoints = plannedStoryPointMetrics
            .Where(metric => metric.Estimate.Source == StoryPointEstimateSource.Derived)
            .Sum(metric => metric.Estimate.Value ?? 0d);
        var missingStoryPointCount = plannedStoryPointMetrics.Count(metric => metric.Estimate.Source == StoryPointEstimateSource.Missing);
        var plannedStoryPoints = plannedStoryPointMetrics
            .Where(metric => metric.Estimate.HasValue)
            .Sum(metric => metric.Estimate.Value ?? 0d);
        isApproximate = isApproximate
            || derivedStoryPointCount > 0
            || usedDerivedDeliveryEstimate;

        return new SprintDeliveryProjection(
            request.Sprint.SprintId,
            request.ProductId,
            plannedPbis.Count,
            plannedPbis.Sum(workItem => workItem!.Effort ?? 0),
            plannedStoryPoints,
            workedItemIds.Count,
            workedItemIds
                .Select(workItemId => request.WorkItemsById.GetValueOrDefault(workItemId))
                .Where(workItem => workItem != null)
                .Sum(workItem => workItem!.Effort ?? 0),
            plannedBugs,
            bugsWorkedOn,
            completedPbiCount,
            completedPbiEffort,
            completedPbiStoryPoints,
            spilloverPbis.Count,
            spilloverPbis.Sum(workItem => workItem!.Effort ?? 0),
            spilloverStoryPoints,
            progressionDelta,
            bugsCreated,
            bugsClosed,
            missingEffortCount,
            missingStoryPointCount,
            derivedStoryPointCount,
            derivedStoryPoints,
            unestimatedDeliveryCount,
            isApproximate);
    }

    /// <inheritdoc />
    public ProgressionDelta ComputeProgressionDelta(SprintDeliveryProgressionRequest request)
    {
        return _deliveryProgressRollupService.ComputeProgressionDelta(request);
    }

    private ResolvedStoryPointEstimate ResolvePbiStoryPointEstimate(
        DeliveryTrendWorkItem pbi,
        IReadOnlyList<DeliveryTrendWorkItem> featurePbis,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup)
    {
        var candidates = featurePbis
            .Select(candidate => new StoryPointResolutionCandidate(
                candidate.ToCanonicalWorkItem(),
                StateClassificationLookup.IsDone(stateLookup, candidate.WorkItemType, candidate.State)))
            .ToArray();

        return DeliveryProgressRollupMath.ResolvePbiStoryPointEstimate(
            pbi,
            featurePbis,
            stateLookup,
            _storyPointResolutionService);
    }

    private ProjectionStoryPointMetrics ResolveProjectionStoryPointMetrics(
        DeliveryTrendWorkItem pbi,
        IReadOnlyList<DeliveryTrendResolvedWorkItem> pbiResolved,
        IReadOnlyDictionary<int, DeliveryTrendWorkItem> workItemsById,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup)
    {
        var resolvedPbi = pbiResolved.FirstOrDefault(resolvedItem => resolvedItem.WorkItemId == pbi.WorkItemId);
        var featureId = resolvedPbi?.ResolvedFeatureId ?? pbi.ParentWorkItemId;
        var featurePbis = pbiResolved
            .Where(resolvedItem => resolvedItem.ResolvedFeatureId == featureId)
            .Select(resolvedItem => workItemsById.GetValueOrDefault(resolvedItem.WorkItemId))
            .OfType<DeliveryTrendWorkItem>()
            .ToList();

        return new ProjectionStoryPointMetrics(
            ResolvePbiStoryPointEstimate(pbi, featurePbis, stateLookup));
    }

    private static bool IsVelocityStoryPointEstimate(ResolvedStoryPointEstimate estimate)
    {
        return estimate.HasValue
            && estimate.Source is not StoryPointEstimateSource.Missing
            && estimate.Source is not StoryPointEstimateSource.Derived;
    }

    private static WorkItemSnapshot ToSnapshot(DeliveryTrendWorkItem workItem)
    {
        return new WorkItemSnapshot(
            workItem.WorkItemId,
            workItem.WorkItemType,
            Normalize(workItem.State),
            Normalize(workItem.IterationPath));
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private readonly record struct ProjectionStoryPointMetrics(
        ResolvedStoryPointEstimate Estimate);
}

internal static class SprintDeliveryProjectionServiceMappings
{
    public static CanonicalWorkItem ToCanonicalWorkItem(this DeliveryTrendWorkItem workItem)
    {
        return new CanonicalWorkItem(
            workItem.WorkItemId,
            workItem.WorkItemType,
            workItem.ParentWorkItemId,
            workItem.BusinessValue,
            workItem.StoryPoints,
            workItem.TimeCriticality,
            workItem.ProjectNumber,
            workItem.ProjectElement,
            workItem.Effort);
    }

    public static IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> GroupByWorkItemId(this IEnumerable<FieldChangeEvent> fieldChanges)
    {
        return fieldChanges
            .GroupBy(fieldChange => fieldChange.WorkItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<FieldChangeEvent>)group.ToList());
    }
}
