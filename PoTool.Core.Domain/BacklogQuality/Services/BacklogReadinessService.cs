using PoTool.Core.Domain.BacklogQuality.Models;
using StateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Core.Domain.BacklogQuality.Services;

/// <summary>
/// Computes canonical backlog readiness scores directly from current graph semantics.
/// </summary>
public sealed class BacklogReadinessService
{
    internal const string MissingDescriptionReason = "MissingDescription";
    internal const string MissingEffortReason = "MissingEffort";
    internal const string MissingChildrenReason = "MissingChildren";
    internal const string ChildAverageReason = "ChildAverage";
    internal const string ReadyReason = "Ready";

    /// <summary>
    /// Computes backlog readiness scores for active Epic, Feature, and PBI scope.
    /// </summary>
    public IReadOnlyList<BacklogReadinessScore> Compute(BacklogGraph backlogGraph)
    {
        ArgumentNullException.ThrowIfNull(backlogGraph);

        var cache = new Dictionary<int, BacklogReadinessScore>();

        return backlogGraph.Items
            .Where(IsScoredScope)
            .Where(IsActive)
            .OrderBy(item => item.WorkItemId)
            .Select(item => ComputeScore(item, backlogGraph, cache))
            .ToArray();
    }

    private static BacklogReadinessScore ComputeScore(
        WorkItemSnapshot item,
        BacklogGraph backlogGraph,
        IDictionary<int, BacklogReadinessScore> cache)
    {
        if (cache.TryGetValue(item.WorkItemId, out var cached))
        {
            return cached;
        }

        var score = IsPbi(item)
            ? ComputePbiScore(item)
            : IsFeature(item)
                ? ComputeFeatureScore(item, backlogGraph, cache)
                : ComputeEpicScore(item, backlogGraph, cache);

        cache[item.WorkItemId] = score;
        return score;
    }

    private static BacklogReadinessScore ComputePbiScore(WorkItemSnapshot item)
    {
        if (!HasDescription(item))
        {
            return new BacklogReadinessScore(item.WorkItemId, item.WorkItemType, new ReadinessScore(0), MissingDescriptionReason);
        }

        if (item.Effort is not > 0)
        {
            return new BacklogReadinessScore(item.WorkItemId, item.WorkItemType, new ReadinessScore(75), MissingEffortReason);
        }

        return new BacklogReadinessScore(item.WorkItemId, item.WorkItemType, new ReadinessScore(100), ReadyReason);
    }

    private static BacklogReadinessScore ComputeFeatureScore(
        WorkItemSnapshot item,
        BacklogGraph backlogGraph,
        IDictionary<int, BacklogReadinessScore> cache)
    {
        if (!HasDescription(item))
        {
            return new BacklogReadinessScore(
                item.WorkItemId,
                item.WorkItemType,
                new ReadinessScore(0),
                MissingDescriptionReason,
                ReadinessOwnerState.PO);
        }

        var childPbis = GetScoredChildren(backlogGraph, item.WorkItemId, BacklogWorkItemTypes.PbiTypes);
        if (childPbis.Count == 0)
        {
            return new BacklogReadinessScore(
                item.WorkItemId,
                item.WorkItemType,
                new ReadinessScore(25),
                MissingChildrenReason,
                ReadinessOwnerState.Team);
        }

        var average = ReadinessScore.Average(childPbis.Select(child =>
            child.StateClassification == StateClassification.Done
                ? new ReadinessScore(100)
                : ComputeScore(child, backlogGraph, cache).Score));
        var ownerState = average.IsFullyReady ? ReadinessOwnerState.Ready : ReadinessOwnerState.Team;
        var reason = average.IsFullyReady ? ReadyReason : ChildAverageReason;

        return new BacklogReadinessScore(item.WorkItemId, item.WorkItemType, average, reason, ownerState);
    }

    private static BacklogReadinessScore ComputeEpicScore(
        WorkItemSnapshot item,
        BacklogGraph backlogGraph,
        IDictionary<int, BacklogReadinessScore> cache)
    {
        if (!HasDescription(item))
        {
            return new BacklogReadinessScore(item.WorkItemId, item.WorkItemType, new ReadinessScore(0), MissingDescriptionReason);
        }

        var childFeatures = GetScoredChildren(backlogGraph, item.WorkItemId, [BacklogWorkItemTypes.Feature]);
        if (childFeatures.Count == 0)
        {
            return new BacklogReadinessScore(item.WorkItemId, item.WorkItemType, new ReadinessScore(30), MissingChildrenReason);
        }

        var average = ReadinessScore.Average(childFeatures.Select(child =>
            child.StateClassification == StateClassification.Done
                ? new ReadinessScore(100)
                : ComputeScore(child, backlogGraph, cache).Score));
        var reason = average.IsFullyReady ? ReadyReason : ChildAverageReason;

        return new BacklogReadinessScore(item.WorkItemId, item.WorkItemType, average, reason);
    }

    private static IReadOnlyList<WorkItemSnapshot> GetScoredChildren(
        BacklogGraph backlogGraph,
        int parentWorkItemId,
        IReadOnlyList<string> workItemTypes)
    {
        return backlogGraph.GetChildren(parentWorkItemId)
            .Where(child => workItemTypes.Contains(child.WorkItemType, StringComparer.OrdinalIgnoreCase))
            .Where(child => child.StateClassification != StateClassification.Removed)
            .OrderBy(child => child.WorkItemId)
            .ToArray();
    }

    private static bool IsScoredScope(WorkItemSnapshot item)
    {
        return string.Equals(item.WorkItemType, BacklogWorkItemTypes.Epic, StringComparison.OrdinalIgnoreCase) ||
               IsFeature(item) ||
               IsPbi(item);
    }

    private static bool IsFeature(WorkItemSnapshot item)
    {
        return string.Equals(item.WorkItemType, BacklogWorkItemTypes.Feature, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPbi(WorkItemSnapshot item)
    {
        return BacklogWorkItemTypes.PbiTypes.Contains(item.WorkItemType, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsActive(WorkItemSnapshot item)
    {
        return item.StateClassification is not StateClassification.Done and not StateClassification.Removed;
    }

    private static bool HasDescription(WorkItemSnapshot item)
    {
        return !string.IsNullOrWhiteSpace(item.Description);
    }
}
