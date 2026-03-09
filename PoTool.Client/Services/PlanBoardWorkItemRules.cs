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
    /// planning panel. Only items that are NOT Done/Removed AND NOT yet assigned to any sprint
    /// column appear in the tree.
    /// Ordering: BacklogPriority ascending (nulls last), then TfsId ascending as tie-breaker.
    /// Effort: PBI/Bug shows own effort; Feature and Epic show sum of eligible descendant efforts.
    /// Parent nodes with no eligible descendants are excluded.
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

        bool IsEligibleLeaf(WorkItemDto w) =>
            IsPlanBoardItem(w.Type) &&
            !IsDoneOrRemoved(w.Type, w.State, doneOrRemovedStates) &&
            !IsAssignedToSprint(w.IterationPath, sprintPaths);

        // Step 1: collect eligible PBIs/Bugs
        var eligibleLeaves = allWorkItems.Where(IsEligibleLeaf).ToList();

        // Step 2: collect the Feature parents of those leaves
        var featureIdSet = eligibleLeaves
            .Where(p => p.ParentTfsId.HasValue &&
                        lookup.TryGetValue(p.ParentTfsId.Value, out var parent) &&
                        IsFeature(parent.Type))
            .Select(p => p.ParentTfsId!.Value)
            .ToHashSet();

        // Step 3: collect the Epic parents of those features
        var epicIdSet = featureIdSet
            .Where(fid => lookup.TryGetValue(fid, out var f) && f.ParentTfsId.HasValue &&
                          lookup.TryGetValue(f.ParentTfsId!.Value, out var ep) && IsEpic(ep.Type))
            .Select(fid => lookup[fid].ParentTfsId!.Value)
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
            .OrderBy(e => e.BacklogPriority ?? double.MaxValue).ThenBy(e => e.TfsId))
        {
            var epicNode = BuildEpicNode(epic, featuresByEpic, leavesByFeature, lookup);
            if (epicNode != null)
                result.Add(epicNode);
        }

        // Handle orphan Features (no Epic parent) — Features whose Epic is not in epicIdSet
        foreach (var feature in featureIdSet
            .Select(id => lookup[id])
            .Where(f => !f.ParentTfsId.HasValue || !epicIdSet.Contains(f.ParentTfsId.Value))
            .OrderBy(f => f.BacklogPriority ?? double.MaxValue).ThenBy(f => f.TfsId))
        {
            var featureNode = BuildFeatureNode(feature, leavesByFeature);
            if (featureNode != null)
                result.Add(featureNode);
        }

        // Handle orphan PBIs/Bugs (no Feature parent)
        foreach (var leaf in orphanLeaves
            .OrderBy(p => p.BacklogPriority ?? double.MaxValue).ThenBy(p => p.TfsId))
        {
            result.Add(BuildLeafNode(leaf));
        }

        return result;
    }

    private static PlanBoardCandidateNode? BuildEpicNode(
        WorkItemDto epic,
        IReadOnlyDictionary<int, List<WorkItemDto>> featuresByEpic,
        IReadOnlyDictionary<int, List<WorkItemDto>> leavesByFeature,
        IReadOnlyDictionary<int, WorkItemDto> lookup)
    {
        var epicNode = new PlanBoardCandidateNode
        {
            TfsId = epic.TfsId,
            Title = epic.Title,
            WorkItemType = epic.Type,
            BacklogPriority = epic.BacklogPriority
        };

        foreach (var feature in (featuresByEpic.GetValueOrDefault(epic.TfsId) ?? new List<WorkItemDto>())
            .OrderBy(f => f.BacklogPriority ?? double.MaxValue).ThenBy(f => f.TfsId))
        {
            var featureNode = BuildFeatureNode(feature, leavesByFeature);
            if (featureNode == null) continue;

            epicNode.Children.Add(featureNode);
            epicNode.EligiblePbiIds.AddRange(featureNode.EligiblePbiIds);
        }

        if (epicNode.EligiblePbiIds.Count == 0)
            return null;

        epicNode.AggregatedEffort = epicNode.Children.Sum(f => f.AggregatedEffort);
        return epicNode;
    }

    private static PlanBoardCandidateNode? BuildFeatureNode(
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
            .OrderBy(p => p.BacklogPriority ?? double.MaxValue).ThenBy(p => p.TfsId))
        {
            var leafNode = BuildLeafNode(leaf);
            featureNode.Children.Add(leafNode);
            featureNode.EligiblePbiIds.Add(leaf.TfsId);
        }

        if (featureNode.EligiblePbiIds.Count == 0)
            return null;

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
    /// <summary>For PBI/Bug: the item's own effort. Null if not estimated.</summary>
    public int? OwnEffort { get; init; }
    /// <summary>For PBI/Bug: same as OwnEffort. For Feature/Epic: sum of eligible descendant efforts.</summary>
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
