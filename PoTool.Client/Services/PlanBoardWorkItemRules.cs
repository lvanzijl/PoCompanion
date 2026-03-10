using PoTool.Client.ApiClient;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public static class PlanBoardWorkItemRules
{
    private static readonly string[] PbiAliases = [WorkItemTypeHelper.Pbi, "PBI"];

    public static bool IsPlanBoardItem(string? workItemType)
    {
        var normalized = NormalizeWorkItemType(workItemType);
        return IsPbi(normalized)
            || string.Equals(normalized, WorkItemTypeHelper.Bug, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFeature(string? workItemType) =>
        string.Equals(NormalizeWorkItemType(workItemType), WorkItemTypeHelper.Feature, StringComparison.OrdinalIgnoreCase);

    public static bool IsEpic(string? workItemType) =>
        string.Equals(NormalizeWorkItemType(workItemType), WorkItemTypeHelper.Epic, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true when a work item state is classified as Done or Removed.
    /// The caller must provide a pre-built set of (workItemType.Lower, stateName.Lower) pairs.
    /// </summary>
    public static bool IsDoneOrRemoved(
        string workItemType,
        string state,
        IReadOnlySet<(string type, string state)> doneOrRemovedStates) =>
        doneOrRemovedStates.Contains((workItemType.ToLowerInvariant(), state.ToLowerInvariant()));

    /// <summary>
    /// Builds the hierarchical candidate tree (Epic → Feature → PBI/Bug) for the left-side
    /// planning panel. Epic and Feature nodes appear when they are not Done/Removed. PBI/Bug leaves
    /// appear only when they are not Done/Removed and not yet assigned to any sprint column.
    /// Ordering: BacklogPriority ascending (nulls last), then TfsId ascending as tie-breaker.
    /// Effort: PBI/Bug shows own effort; Feature and Epic show sum of eligible descendant efforts.
    /// </summary>
    /// <param name="allWorkItems">Full work item hierarchy for the product.</param>
    /// <param name="doneOrRemovedStates">
    ///   Set of (workItemType.Lower, stateName.Lower) pairs classified as Done or Removed.
    ///   Build this from <see cref="PoTool.Shared.Settings.GetStateClassificationsResponse"/>.
    /// </param>
    /// <param name="sprintPaths">Normalized (backslash) iteration paths of all visible sprint columns.</param>
    public static List<PlanBoardCandidateNode> BuildCandidateTree(
        IReadOnlyList<WorkItemDto> allWorkItems,
        IReadOnlySet<(string type, string state)> doneOrRemovedStates,
        IReadOnlySet<string> sprintPaths)
    {
        var lookup = allWorkItems.ToDictionary(w => w.TfsId);

        bool IsVisibleParent(WorkItemDto w) =>
            (IsEpic(w.Type) || IsFeature(w.Type)) &&
            !IsDoneOrRemoved(w.Type, w.State, doneOrRemovedStates);

        bool IsEligibleLeaf(WorkItemDto w) =>
            IsPlanBoardItem(w.Type) &&
            !IsDoneOrRemoved(w.Type, w.State, doneOrRemovedStates) &&
            !IsAssignedToSprint(w.IterationPath, sprintPaths);

        // Step 1: collect visible Epics/Features and eligible PBIs/Bugs
        var visibleParents = allWorkItems.Where(IsVisibleParent).ToList();
        var eligibleLeaves = allWorkItems.Where(IsEligibleLeaf).ToList();

        var featureIdSet = visibleParents
            .Where(w => IsFeature(w.Type))
            .Select(w => w.TfsId)
            .ToHashSet();

        var epicIdSet = visibleParents
            .Where(w => IsEpic(w.Type))
            .Select(w => w.TfsId)
            .ToHashSet();

        // Group PBIs/Bugs by Feature parent id (only those with a known Feature parent)
        var leavesByFeature = eligibleLeaves
            .Where(p => p.ParentTfsId.HasValue && featureIdSet.Contains(p.ParentTfsId.Value))
            .GroupBy(p => p.ParentTfsId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Orphan leaves (no Feature parent)
        var orphanLeaves = eligibleLeaves
            .Where(p => !p.ParentTfsId.HasValue || !featureIdSet.Contains(p.ParentTfsId.Value))
            .ToList();

        // Group Features by Epic parent id (only those with a known Epic parent)
        var featuresByEpic = featureIdSet
            .Select(fid => lookup[fid])
            .Where(f => f.ParentTfsId.HasValue && epicIdSet.Contains(f.ParentTfsId.Value))
            .GroupBy(f => f.ParentTfsId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<PlanBoardCandidateNode>();

        // Build Epic nodes
        foreach (var epic in epicIdSet
            .Select(id => lookup[id])
            .OrderByBacklogPriority())
        {
            var epicNode = BuildEpicNode(epic, featuresByEpic, leavesByFeature);
            result.Add(epicNode);
        }

        // Handle orphan Features (no Epic parent) — Features whose Epic is not in epicIdSet
        foreach (var feature in featureIdSet
            .Select(id => lookup[id])
            .Where(f => !f.ParentTfsId.HasValue || !epicIdSet.Contains(f.ParentTfsId.Value))
            .OrderByBacklogPriority())
        {
            var featureNode = BuildFeatureNode(feature, leavesByFeature);
            result.Add(featureNode);
        }

        // Handle orphan PBIs/Bugs (no Feature parent)
        foreach (var leaf in orphanLeaves.OrderByBacklogPriority())
        {
            result.Add(BuildLeafNode(leaf));
        }

        return result;
    }

    private static PlanBoardCandidateNode BuildEpicNode(
        WorkItemDto epic,
        IReadOnlyDictionary<int, List<WorkItemDto>> featuresByEpic,
        IReadOnlyDictionary<int, List<WorkItemDto>> leavesByFeature)
    {
        var epicNode = new PlanBoardCandidateNode
        {
            TfsId = epic.TfsId,
            Title = epic.Title,
            WorkItemType = epic.Type,
            BacklogPriority = epic.BacklogPriority
        };

        foreach (var feature in (featuresByEpic.GetValueOrDefault(epic.TfsId) ?? new List<WorkItemDto>())
            .OrderByBacklogPriority())
        {
            var featureNode = BuildFeatureNode(feature, leavesByFeature);
            epicNode.Children.Add(featureNode);
            epicNode.EligiblePbiIds.AddRange(featureNode.EligiblePbiIds);
        }

        epicNode.AggregatedEffort = epicNode.Children.Sum(f => f.AggregatedEffort);
        return epicNode;
    }

    private static PlanBoardCandidateNode BuildFeatureNode(
        WorkItemDto feature,
        IReadOnlyDictionary<int, List<WorkItemDto>> leavesByFeature)
    {
        var featureNode = new PlanBoardCandidateNode
        {
            TfsId = feature.TfsId,
            Title = feature.Title,
            WorkItemType = feature.Type,
            BacklogPriority = feature.BacklogPriority
        };

        foreach (var leaf in (leavesByFeature.GetValueOrDefault(feature.TfsId) ?? new List<WorkItemDto>())
            .OrderByBacklogPriority())
        {
            var leafNode = BuildLeafNode(leaf);
            featureNode.Children.Add(leafNode);
            featureNode.EligiblePbiIds.Add(leaf.TfsId);
        }

        featureNode.AggregatedEffort = featureNode.Children.Sum(c => c.OwnEffort ?? 0);
        return featureNode;
    }

    private static PlanBoardCandidateNode BuildLeafNode(WorkItemDto leaf) =>
        new()
        {
            TfsId = leaf.TfsId,
            Title = leaf.Title,
            WorkItemType = leaf.Type,
            BacklogPriority = leaf.BacklogPriority,
            OwnEffort = leaf.Effort,
            AggregatedEffort = leaf.Effort ?? 0,
            EligiblePbiIds = [leaf.TfsId]
        };

    private static bool IsAssignedToSprint(string iterationPath, IReadOnlySet<string> sprintPaths)
    {
        if (string.IsNullOrEmpty(iterationPath) || sprintPaths.Count == 0)
            return false;

        var normalized = iterationPath.Replace('/', '\\').Trim();
        return sprintPaths.Contains(normalized);
    }

    /// <summary>
    /// Orders a sequence of work items by BacklogPriority ascending (nulls sorted last),
    /// with TfsId as a stable tie-breaker.
    /// </summary>
    private static IOrderedEnumerable<WorkItemDto> OrderByBacklogPriority(
        this IEnumerable<WorkItemDto> source) =>
        source.OrderBy(w => w.BacklogPriority ?? double.MaxValue).ThenBy(w => w.TfsId);

    public static string? ResolveFeatureTitle(WorkItemDto workItem, IReadOnlyDictionary<int, WorkItemDto> workItemLookup)
    {
        if (!workItem.ParentTfsId.HasValue)
            return null;

        return workItemLookup.TryGetValue(workItem.ParentTfsId.Value, out var parent)
            ? parent.Title
            : null;
    }

    public static PlanBoardWorkItemDescriptor CreateDescriptor(WorkItemDto workItem, IReadOnlyDictionary<int, WorkItemDto> workItemLookup)
    {
        return new PlanBoardWorkItemDescriptor(
            workItem.TfsId,
            workItem.Title,
            NormalizeWorkItemType(workItem.Type) ?? workItem.Type,
            ResolveFeatureTitle(workItem, workItemLookup),
            workItem.Effort,
            workItem.IterationPath);
    }

    public static string GetTypeLabel(string? workItemType)
    {
        var normalized = NormalizeWorkItemType(workItemType);
        if (string.Equals(normalized, WorkItemTypeHelper.Bug, StringComparison.OrdinalIgnoreCase))
            return WorkItemTypeHelper.Bug;

        return "PBI";
    }

    private static bool IsPbi(string? workItemType) =>
        PbiAliases.Any(alias => string.Equals(workItemType, alias, StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeWorkItemType(string? workItemType) =>
        string.IsNullOrWhiteSpace(workItemType) ? null : workItemType.Trim();
}

/// <summary>
/// Represents a node in the hierarchical planning candidate tree on the left side of the Plan Board.
/// </summary>
public sealed class PlanBoardCandidateNode
{
    public int TfsId { get; init; }
    public required string Title { get; init; }
    public required string WorkItemType { get; init; }
    public double? BacklogPriority { get; init; }
    /// <summary>
    /// The raw effort estimate for this work item as stored in TFS.
    /// Populated only for PBI/Bug leaf nodes. Null when no estimate exists.
    /// Use <see cref="AggregatedEffort"/> for display at all levels.
    /// </summary>
    public int? OwnEffort { get; init; }
    /// <summary>
    /// The effort value used for display in the tree UI.
    /// For PBI/Bug: equal to <see cref="OwnEffort"/> (0 when null/unestimated).
    /// For Feature/Epic: sum of <see cref="OwnEffort"/> values of all eligible descendant PBIs/Bugs.
    /// Done and Removed descendants are never included in this sum.
    /// </summary>
    public int AggregatedEffort { get; set; }
    public List<PlanBoardCandidateNode> Children { get; init; } = [];
    /// <summary>Controls expand/collapse in the tree UI. Expanded by default.</summary>
    public bool IsExpanded { get; set; } = true;
    /// <summary>
    /// The TfsIds of PBIs/Bugs that will actually be planned when this node is dragged to a sprint.
    /// For PBI/Bug: [self]. For Feature/Epic: all eligible descendant PBI/Bug ids.
    /// </summary>
    public List<int> EligiblePbiIds { get; set; } = [];
}

public sealed record PlanBoardWorkItemDescriptor(
    int TfsId,
    string Title,
    string WorkItemType,
    string? FeatureTitle,
    int? Effort,
    string IterationPath);
