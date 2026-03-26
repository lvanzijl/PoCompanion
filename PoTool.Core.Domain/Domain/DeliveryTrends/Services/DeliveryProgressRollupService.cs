using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.WorkItems;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.Sprints;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Computes canonical feature and epic delivery progress rollups from prepared delivery-trend inputs.
/// </summary>
public interface IDeliveryProgressRollupService
{
    /// <summary>
    /// Computes canonical feature progress rollups.
    /// </summary>
    IReadOnlyList<FeatureProgress> ComputeFeatureProgress(DeliveryFeatureProgressRequest request);

    /// <summary>
    /// Computes canonical epic progress rollups from feature rollups.
    /// </summary>
    IReadOnlyList<EpicProgress> ComputeEpicProgress(DeliveryEpicProgressRequest request);

    /// <summary>
    /// Computes the average feature progression delta for sprint activity.
    /// </summary>
    ProgressionDelta ComputeProgressionDelta(SprintDeliveryProgressionRequest request);
}

/// <summary>
/// Implements canonical feature, epic, and sprint-progression rollups independent of API orchestration and persistence.
/// </summary>
public sealed class DeliveryProgressRollupService : IDeliveryProgressRollupService
{
    private readonly ICanonicalStoryPointResolutionService _storyPointResolutionService;
    private readonly IHierarchyRollupService _hierarchyRollupService;
    private readonly IFeatureForecastService _featureForecastService;
    private readonly IEpicAggregationService _epicAggregationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryProgressRollupService"/> class.
    /// </summary>
    public DeliveryProgressRollupService(
        ICanonicalStoryPointResolutionService storyPointResolutionService,
        IHierarchyRollupService hierarchyRollupService,
        IFeatureForecastService? featureForecastService = null,
        IEpicAggregationService? epicAggregationService = null)
    {
        _storyPointResolutionService = storyPointResolutionService ?? throw new ArgumentNullException(nameof(storyPointResolutionService));
        _hierarchyRollupService = hierarchyRollupService ?? throw new ArgumentNullException(nameof(hierarchyRollupService));
        _featureForecastService = featureForecastService ?? new FeatureForecastService();
        _epicAggregationService = epicAggregationService ?? new EpicAggregationService();
    }

    /// <inheritdoc />
    public IReadOnlyList<FeatureProgress> ComputeFeatureProgress(DeliveryFeatureProgressRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var results = new List<FeatureProgress>();

        foreach (var productId in request.ProductIds)
        {
            var productResolved = request.ResolvedItems
                .Where(resolvedItem => resolvedItem.ResolvedProductId == productId)
                .ToList();
            var activeHierarchyIds = request.ActiveWorkItemIds == null
                ? null
                : DeliveryProgressRollupMath.PropagateActivityToAncestors(request.ActiveWorkItemIds, productResolved, request.WorkItemsById);

            var features = productResolved
                .Where(resolvedItem => resolvedItem.WorkItemType == CanonicalWorkItemTypes.Feature)
                .ToList();

            foreach (var feature in features)
            {
                if (!request.WorkItemsById.TryGetValue(feature.WorkItemId, out var featureWorkItem))
                {
                    continue;
                }

                var childProgressItems = productResolved
                    .Where(resolvedItem => CanonicalWorkItemTypes.IsFeatureProgressContributor(resolvedItem.WorkItemType)
                        && resolvedItem.ResolvedFeatureId == feature.WorkItemId)
                    .Select(resolvedItem => request.WorkItemsById.GetValueOrDefault(resolvedItem.WorkItemId))
                    .OfType<DeliveryTrendWorkItem>()
                    .ToList();
                var childPbis = childProgressItems
                    .Where(child => CanonicalWorkItemTypes.IsAuthoritativePbi(child.WorkItemType))
                    .ToList();

                if (activeHierarchyIds != null)
                {
                    var hasActivity = activeHierarchyIds.Contains(feature.WorkItemId);
                    var hasSprintPbis = request.SprintAssignedPbiIds != null
                        && childPbis.Any(childPbi => request.SprintAssignedPbiIds.Contains(childPbi.WorkItemId));
                    if (!hasActivity && !hasSprintPbis)
                    {
                        continue;
                    }
                }

                var featureScope = childPbis.Count > 0
                    ? ComputeFeatureScope(featureWorkItem, childPbis, request.StateLookup)
                    : HierarchyScopeRollup.Empty;
                var featureProgressRequest = new FeatureProgressCalculationRequest(
                    featureWorkItem.ToCanonicalWorkItem(),
                    childProgressItems
                        .Select(child => new FeatureProgressChild(
                            child.ToCanonicalWorkItem(),
                            StateClassificationLookup.GetClassification(request.StateLookup, child.WorkItemType, child.State)))
                        .ToList());
                var featureProgressDetails = FeatureProgressComputation.ComputeDetails(featureProgressRequest);
                var donePbiCount = childProgressItems.Count(child =>
                    StateClassificationLookup.GetClassification(request.StateLookup, child.WorkItemType, child.State) == StateClassification.Done);
                var featureForecast = _featureForecastService.Compute(new FeatureForecastCalculationRequest(
                    featureProgressDetails.EffectiveProgress,
                    featureWorkItem.Effort));
                var featureIsDone = StateClassificationLookup.IsDone(request.StateLookup, featureWorkItem.WorkItemType, featureWorkItem.State);
                var calculatedProgressPercent = featureProgressDetails.BaseProgress * 100d;
                var effectiveProgressPercent = featureProgressDetails.EffectiveProgress * 100d;
                var progressPercent = (int)Math.Round(effectiveProgressPercent, MidpointRounding.AwayFromZero);

                var sprintCompletedScopeStoryPoints = request.SprintCompletedPbiIds == null
                    ? 0d
                    : childPbis
                        .Where(childPbi => request.SprintCompletedPbiIds.Contains(childPbi.WorkItemId))
                        .Sum(childPbi => DeliveryProgressRollupMath.ResolvePbiStoryPointEstimate(
                            childPbi,
                            childPbis,
                            request.StateLookup,
                            _storyPointResolutionService).Value ?? 0d);
                var sprintCompletedPbiCount = request.SprintCompletedPbiIds == null
                    ? 0
                    : childPbis.Count(childPbi => request.SprintCompletedPbiIds.Contains(childPbi.WorkItemId));
                var sprintCompletedInSprint = request.SprintCompletedPbiIds?.Contains(feature.WorkItemId) == true;
                var sprintProgressionDelta = featureScope.Total > 0
                    ? new ProgressionDelta(Math.Round(sprintCompletedScopeStoryPoints / featureScope.Total * 100, 2))
                    : new ProgressionDelta(0);
                var sprintEffortDelta = request.SprintEffortDeltaByWorkItem == null
                    ? 0
                    : childPbis.Sum(childPbi => request.SprintEffortDeltaByWorkItem.GetValueOrDefault(childPbi.WorkItemId, 0));
                var weight = featureProgressDetails.TotalEffort;
                var isExcluded = weight <= 0;

                int? epicId = feature.ResolvedEpicId;
                string? epicTitle = null;
                if (epicId.HasValue && request.WorkItemsById.TryGetValue(epicId.Value, out var epicWorkItem))
                {
                    epicTitle = epicWorkItem.Title;
                }

                results.Add(new FeatureProgress(
                    feature.WorkItemId,
                    featureWorkItem.Title,
                    productId,
                    epicId,
                    epicTitle,
                    progressPercent,
                    featureScope.Total,
                    featureScope.Completed,
                    donePbiCount,
                    featureIsDone,
                    sprintCompletedScopeStoryPoints,
                    sprintProgressionDelta,
                    sprintEffortDelta,
                    sprintCompletedPbiCount,
                    sprintCompletedInSprint,
                    calculatedProgressPercent,
                    featureProgressDetails.OverrideRaw,
                    effectiveProgressPercent,
                    Array.Empty<string>(),
                    featureForecast.ForecastConsumedEffort,
                    featureForecast.ForecastRemainingEffort,
                    weight,
                    isExcluded,
                    featureWorkItem.Effort));
            }
        }

        return results
            .OrderByDescending(feature => feature.ProgressPercent)
            .ThenBy(feature => feature.FeatureTitle)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<EpicProgress> ComputeEpicProgress(DeliveryEpicProgressRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var featureProgressByEpic = request.FeatureProgress
            .Where(feature => feature.EpicId.HasValue)
            .GroupBy(feature => feature.EpicId!.Value)
            .ToList();

        var results = new List<EpicProgress>();

        foreach (var group in featureProgressByEpic)
        {
            if (!request.WorkItemsById.TryGetValue(group.Key, out var epicWorkItem))
            {
                continue;
            }

            if (!CanonicalWorkItemTypes.IsEpic(epicWorkItem.WorkItemType))
            {
                continue;
            }

            var features = group.ToList();
            var aggregation = _epicAggregationService.Compute(new EpicAggregationRequest(
                epicWorkItem.ToCanonicalWorkItem(),
                features
                    .Select(feature =>
                    {
                        if (!request.WorkItemsById.TryGetValue(feature.FeatureId, out var featureWorkItem))
                        {
                            return null;
                        }

                        return new EpicFeatureProgress(
                            featureWorkItem.ToCanonicalWorkItem(),
                            (feature.EffectiveProgress ?? 0d) / 100d,
                            feature.Weight,
                            feature.ForecastConsumedEffort,
                            feature.ForecastRemainingEffort);
                    })
                    .OfType<EpicFeatureProgress>()
                    .ToList()));
            var totalScopeStoryPoints = features.Sum(feature => feature.TotalScopeStoryPoints);
            var deliveredStoryPoints = features.Sum(feature => feature.DeliveredStoryPoints);
            var epicIsDone = StateClassificationLookup.IsDone(request.StateLookup, epicWorkItem.WorkItemType, epicWorkItem.State);
            int? progressPercent = (int)Math.Round(aggregation.EpicProgress, MidpointRounding.AwayFromZero);
            var sprintDeliveredStoryPoints = features.Sum(feature => feature.SprintDeliveredStoryPoints);
            var sprintProgressionDelta = totalScopeStoryPoints > 0
                ? new ProgressionDelta(Math.Round(sprintDeliveredStoryPoints / totalScopeStoryPoints * 100, 2))
                : new ProgressionDelta(0);

            results.Add(new EpicProgress(
                group.Key,
                epicWorkItem.Title,
                features[0].ProductId,
                progressPercent,
                totalScopeStoryPoints,
                deliveredStoryPoints,
                features.Count,
                features.Count(feature => feature.IsDone),
                features.Sum(feature => feature.DonePbiCount),
                epicIsDone,
                sprintDeliveredStoryPoints,
                sprintProgressionDelta,
                features.Sum(feature => feature.SprintEffortDelta),
                features.Sum(feature => feature.SprintCompletedPbiCount),
                features.Count(feature => feature.SprintCompletedInSprint),
                aggregation.EpicProgress,
                aggregation.EpicForecastConsumed,
                aggregation.EpicForecastRemaining,
                aggregation.ExcludedFeaturesCount,
                aggregation.IncludedFeaturesCount,
                aggregation.TotalWeight));
        }

        return results
            .OrderByDescending(epic => epic.ProgressPercent)
            .ThenBy(epic => epic.EpicTitle)
            .ToList();
    }

    /// <inheritdoc />
    public ProgressionDelta ComputeProgressionDelta(SprintDeliveryProgressionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var featureResolved = request.ResolvedItems
            .Where(resolvedItem => resolvedItem.WorkItemType == CanonicalWorkItemTypes.Feature)
            .ToList();
        if (featureResolved.Count == 0)
        {
            return new ProgressionDelta(0);
        }

        var totalFeatureProgression = 0.0;
        var featureCount = 0;

        foreach (var feature in featureResolved)
        {
            if (!request.WorkItemsById.TryGetValue(feature.WorkItemId, out var featureWorkItem))
            {
                continue;
            }

            var childPbis = request.ResolvedItems
                .Where(resolvedItem => CanonicalWorkItemTypes.IsAuthoritativePbi(resolvedItem.WorkItemType)
                    && resolvedItem.ResolvedFeatureId == feature.WorkItemId)
                .Select(resolvedItem => request.WorkItemsById.GetValueOrDefault(resolvedItem.WorkItemId))
                .OfType<DeliveryTrendWorkItem>()
                .ToList();

            if (childPbis.Count == 0)
            {
                continue;
            }

            var (canonicalFeatureWorkItem, canonicalFeatureWorkItems, doneByWorkItemId) = DeliveryProgressRollupMath.BuildFeatureRollupContext(
                featureWorkItem,
                childPbis,
                request.StateLookup);
            var scope = _hierarchyRollupService.RollupCanonicalScope(canonicalFeatureWorkItem, canonicalFeatureWorkItems, doneByWorkItemId);
            if (scope.Total <= 0)
            {
                continue;
            }

            var featureHadProgress = childPbis.Any(childPbi =>
                request.ActivityByWorkItem.TryGetValue(childPbi.WorkItemId, out var pbiEvents)
                && pbiEvents.Any(activityEvent =>
                    string.Equals(activityEvent.FieldRefName, "System.State", StringComparison.OrdinalIgnoreCase)
                    && StateClassificationLookup.IsDone(request.StateLookup, childPbi.WorkItemType, activityEvent.NewValue)));

            if (!featureHadProgress)
            {
                continue;
            }

            totalFeatureProgression += scope.Completed / scope.Total * 100;
            featureCount++;
        }

        return new ProgressionDelta(featureCount > 0 ? Math.Round(totalFeatureProgression / featureCount, 2) : 0);
    }

    private HierarchyScopeRollup ComputeFeatureScope(
        DeliveryTrendWorkItem featureWorkItem,
        IReadOnlyList<DeliveryTrendWorkItem> childPbis,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup)
    {
        var (canonicalFeatureWorkItem, canonicalFeatureWorkItems, doneByWorkItemId) = DeliveryProgressRollupMath.BuildFeatureRollupContext(
            featureWorkItem,
            childPbis,
            stateLookup);
        return _hierarchyRollupService.RollupCanonicalScope(canonicalFeatureWorkItem, canonicalFeatureWorkItems, doneByWorkItemId);
    }
}

internal static class DeliveryProgressRollupMath
{
    public static ResolvedStoryPointEstimate ResolvePbiStoryPointEstimate(
        DeliveryTrendWorkItem pbi,
        IReadOnlyList<DeliveryTrendWorkItem> featurePbis,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        ICanonicalStoryPointResolutionService storyPointResolutionService)
    {
        var candidates = featurePbis
            .Select(candidate => new StoryPointResolutionCandidate(
                candidate.ToCanonicalWorkItem(),
                StateClassificationLookup.IsDone(stateLookup, candidate.WorkItemType, candidate.State)))
            .ToArray();

        return storyPointResolutionService.Resolve(new StoryPointResolutionRequest(
            pbi.ToCanonicalWorkItem(),
            StateClassificationLookup.IsDone(stateLookup, pbi.WorkItemType, pbi.State),
            candidates));
    }

    public static (CanonicalWorkItem FeatureWorkItem, List<CanonicalWorkItem> FeatureWorkItems, Dictionary<int, bool> DoneByWorkItemId)
        BuildFeatureRollupContext(
            DeliveryTrendWorkItem featureWorkItem,
            IReadOnlyList<DeliveryTrendWorkItem> childPbis,
            IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup)
    {
        var canonicalFeatureWorkItem = featureWorkItem.ToCanonicalWorkItem();
        var canonicalFeatureWorkItems = new List<CanonicalWorkItem>(childPbis.Count + 1) { canonicalFeatureWorkItem };
        canonicalFeatureWorkItems.AddRange(childPbis.Select(childPbi => childPbi.ToCanonicalWorkItem()));
        var doneByWorkItemId = new Dictionary<int, bool>
        {
            [featureWorkItem.WorkItemId] = StateClassificationLookup.IsDone(stateLookup, featureWorkItem.WorkItemType, featureWorkItem.State)
        };

        foreach (var childPbi in childPbis)
        {
            doneByWorkItemId[childPbi.WorkItemId] = StateClassificationLookup.IsDone(stateLookup, childPbi.WorkItemType, childPbi.State);
        }

        return (canonicalFeatureWorkItem, canonicalFeatureWorkItems, doneByWorkItemId);
    }

    public static HashSet<int> PropagateActivityToAncestors(
        IReadOnlyCollection<int> activeWorkItemIds,
        IReadOnlyCollection<DeliveryTrendResolvedWorkItem> resolvedItems,
        IReadOnlyDictionary<int, DeliveryTrendWorkItem> workItemsById)
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
            if (!workItemsById.TryGetValue(workItemId, out var workItem)
                || !workItem.ParentWorkItemId.HasValue
                || !resolvedWorkItemIds.Contains(workItem.ParentWorkItemId.Value))
            {
                continue;
            }

            if (propagatedWorkItemIds.Add(workItem.ParentWorkItemId.Value))
            {
                queue.Enqueue(workItem.ParentWorkItemId.Value);
            }
        }

        return propagatedWorkItemIds;
    }
}
