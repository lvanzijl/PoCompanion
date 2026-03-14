using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.Metrics.Services;

/// <summary>
/// Computes canonical hierarchy story-point rollups for Features and Epics.
/// </summary>
public interface IHierarchyRollupService
{
    /// <summary>
    /// Rolls a work item's canonical story-point scope up from descendant PBIs, with parent fallback when allowed.
    /// </summary>
    /// <param name="workItem">The root work item whose scope should be calculated.</param>
    /// <param name="allWorkItems">The hierarchy snapshot containing the work item and its descendants.</param>
    /// <param name="doneByWorkItemId">Canonical Done-state lookup keyed by work item id.</param>
    /// <returns>The total and completed canonical scope for the hierarchy rooted at <paramref name="workItem"/>.</returns>
    HierarchyScopeRollup RollupCanonicalScope(
        WorkItemDto workItem,
        IReadOnlyList<WorkItemDto> allWorkItems,
        IReadOnlyDictionary<int, bool> doneByWorkItemId);
}

/// <summary>
/// Represents a canonical hierarchy rollup result.
/// </summary>
/// <param name="Total">The total canonical scope.</param>
/// <param name="Completed">The completed canonical scope.</param>
public readonly record struct HierarchyScopeRollup(double Total, double Completed)
{
    public static HierarchyScopeRollup Empty => new(0d, 0d);
}

/// <summary>
/// Implements the canonical PBI → Feature → Epic story-point rollup rules.
/// </summary>
public sealed class HierarchyRollupService : IHierarchyRollupService
{
    private readonly ICanonicalStoryPointResolutionService _storyPointResolutionService;

    public HierarchyRollupService(ICanonicalStoryPointResolutionService storyPointResolutionService)
    {
        _storyPointResolutionService = storyPointResolutionService;
    }

    public HierarchyScopeRollup RollupCanonicalScope(
        WorkItemDto workItem,
        IReadOnlyList<WorkItemDto> allWorkItems,
        IReadOnlyDictionary<int, bool> doneByWorkItemId)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(allWorkItems);
        ArgumentNullException.ThrowIfNull(doneByWorkItemId);

        var isDone = doneByWorkItemId.GetValueOrDefault(workItem.TfsId);
        var directChildren = allWorkItems
            .Where(candidate => candidate.ParentTfsId == workItem.TfsId)
            .ToList();

        if (IsFeature(workItem.Type))
        {
            return RollupFeatureScope(workItem, isDone, directChildren, doneByWorkItemId);
        }

        var totalScope = 0d;
        var completedScope = 0d;

        foreach (var childFeature in directChildren.Where(child => IsFeature(child.Type)))
        {
            var childScope = RollupCanonicalScope(childFeature, allWorkItems, doneByWorkItemId);
            totalScope += childScope.Total;
            completedScope += childScope.Completed;
        }

        var directPbis = directChildren
            .Where(child => IsAuthoritativePbi(child.Type))
            .ToList();
        if (directPbis.Count > 0)
        {
            var directPbiScope = RollupPbiChildren(directPbis, doneByWorkItemId);
            totalScope += directPbiScope.Total;
            completedScope += directPbiScope.Completed;
        }

        if (totalScope > 0)
        {
            return new HierarchyScopeRollup(totalScope, completedScope);
        }

        return ResolveParentFallback(workItem, isDone);
    }

    private HierarchyScopeRollup RollupFeatureScope(
        WorkItemDto feature,
        bool featureIsDone,
        IReadOnlyList<WorkItemDto> directChildren,
        IReadOnlyDictionary<int, bool> doneByWorkItemId)
    {
        var featurePbis = directChildren
            .Where(child => IsAuthoritativePbi(child.Type))
            .ToList();

        var scope = RollupPbiChildren(featurePbis, doneByWorkItemId);
        if (scope.Total > 0)
        {
            return scope;
        }

        return ResolveParentFallback(feature, featureIsDone);
    }

    private HierarchyScopeRollup RollupPbiChildren(
        IReadOnlyList<WorkItemDto> featurePbis,
        IReadOnlyDictionary<int, bool> doneByWorkItemId)
    {
        if (featurePbis.Count == 0)
        {
            return HierarchyScopeRollup.Empty;
        }

        var candidates = featurePbis
            .Select(pbi => new StoryPointResolutionCandidate(
                pbi,
                doneByWorkItemId.GetValueOrDefault(pbi.TfsId)))
            .ToArray();

        var totalScope = 0d;
        var completedScope = 0d;

        foreach (var pbi in featurePbis)
        {
            var isDone = doneByWorkItemId.GetValueOrDefault(pbi.TfsId);
            var estimate = _storyPointResolutionService.Resolve(new StoryPointResolutionRequest(
                pbi,
                isDone,
                candidates));

            if (!estimate.HasValue)
            {
                continue;
            }

            totalScope += estimate.Value!.Value;
            if (isDone)
            {
                completedScope += estimate.Value.Value;
            }
        }

        return new HierarchyScopeRollup(totalScope, completedScope);
    }

    private HierarchyScopeRollup ResolveParentFallback(WorkItemDto workItem, bool isDone)
    {
        var fallbackEstimate = _storyPointResolutionService.ResolveParentFallback(new StoryPointFallbackRequest(workItem, isDone));
        if (!fallbackEstimate.HasValue)
        {
            return HierarchyScopeRollup.Empty;
        }

        var fallbackValue = fallbackEstimate.Value!.Value;
        return new HierarchyScopeRollup(fallbackValue, isDone ? fallbackValue : 0d);
    }

    private static bool IsFeature(string workItemType)
    {
        return workItemType.Equals(WorkItemType.Feature, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuthoritativePbi(string workItemType)
    {
        return workItemType.Equals(WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.PbiShort, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.UserStory, StringComparison.OrdinalIgnoreCase);
    }
}
