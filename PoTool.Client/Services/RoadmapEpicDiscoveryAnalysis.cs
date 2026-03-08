using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

public static class RoadmapEpicDiscoveryAnalysis
{
    public static RoadmapEpicDiscoveryReport Analyze(IEnumerable<int> configuredRootIds, IEnumerable<WorkItemDto> workItems)
    {
        var normalizedRootIds = configuredRootIds
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var configuredRootIdSet = normalizedRootIds.ToHashSet();

        var rawItems = workItems.ToList();
        var itemLookup = rawItems
            .GroupBy(item => item.TfsId)
            .ToDictionary(group => group.Key, group => group.First());

        var epicLikeItems = rawItems
            .Where(item => RoadmapWorkItemRules.IsEpic(item.Type))
            .ToList();

        var roadmapTaggedItems = rawItems
            .Where(item => RoadmapWorkItemRules.HasRoadmapTag(item.Tags))
            .ToList();

        var roadmapEpicItems = epicLikeItems
            .Where(item => RoadmapWorkItemRules.HasRoadmapTag(item.Tags))
            .OrderBy(item => item.BacklogPriority ?? double.MaxValue)
            .ThenBy(item => item.TfsId)
            .ToList();

        var availableEpicItems = epicLikeItems
            .Where(item => !RoadmapWorkItemRules.HasRoadmapTag(item.Tags))
            .OrderBy(item => item.Title)
            .ThenBy(item => item.TfsId)
            .ToList();

        var countsByType = rawItems
            .GroupBy(item => RoadmapWorkItemRules.NormalizeWorkItemType(item.Type) ?? "<null>")
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new RoadmapWorkItemTypeCount(group.Key, group.Count()))
            .ToList();

        var distinctTypes = countsByType
            .Select(group => group.Type)
            .ToList();

        var presentRootIds = normalizedRootIds
            .Where(rootId => configuredRootIdSet.Contains(rootId) && itemLookup.ContainsKey(rootId))
            .ToList();

        var missingRootIds = normalizedRootIds
            .Except(presentRootIds)
            .ToList();

        var objectiveRootIds = rawItems
            .Where(item => configuredRootIdSet.Contains(item.TfsId) && RoadmapWorkItemRules.IsObjective(item.Type))
            .Select(item => item.TfsId)
            .OrderBy(id => id)
            .ToList();

        var descendantsUnderConfiguredRootsCount = rawItems.Count(item =>
            item.ParentTfsId.HasValue && IsInConfiguredRootScope(item, configuredRootIdSet, itemLookup));

        var epicLikeItemsUnderConfiguredRootsCount = epicLikeItems.Count(item =>
            IsInConfiguredRootScope(item, configuredRootIdSet, itemLookup));

        return new RoadmapEpicDiscoveryReport(
            normalizedRootIds,
            presentRootIds,
            missingRootIds,
            objectiveRootIds,
            rawItems,
            epicLikeItems,
            roadmapTaggedItems,
            roadmapEpicItems,
            availableEpicItems,
            countsByType,
            distinctTypes,
            descendantsUnderConfiguredRootsCount,
            epicLikeItemsUnderConfiguredRootsCount,
            rawItems.Take(20).Select(ToSample).ToList(),
            epicLikeItems.Take(10).Select(ToSample).ToList(),
            roadmapTaggedItems.Take(10).Select(ToSample).ToList(),
            roadmapEpicItems.Take(10).Select(ToSample).ToList());
    }

    private static RoadmapWorkItemSample ToSample(WorkItemDto workItem) =>
        new(
            workItem.TfsId,
            RoadmapWorkItemRules.NormalizeWorkItemType(workItem.Type) ?? "<null>",
            workItem.Title,
            workItem.Tags,
            workItem.ParentTfsId,
            workItem.BacklogPriority);

    private static bool IsInConfiguredRootScope(
        WorkItemDto workItem,
        IReadOnlySet<int> configuredRootIds,
        IReadOnlyDictionary<int, WorkItemDto> itemLookup)
    {
        if (configuredRootIds.Contains(workItem.TfsId))
            return true;

        var visited = new HashSet<int> { workItem.TfsId };
        var currentParentId = workItem.ParentTfsId;

        while (currentParentId.HasValue)
        {
            if (configuredRootIds.Contains(currentParentId.Value))
                return true;

            if (!visited.Add(currentParentId.Value))
                return false;

            if (!itemLookup.TryGetValue(currentParentId.Value, out var parentItem))
                return false;

            currentParentId = parentItem.ParentTfsId;
        }

        return false;
    }
}

public sealed record RoadmapEpicDiscoveryReport(
    IReadOnlyList<int> ConfiguredRootIds,
    IReadOnlyList<int> PresentRootIds,
    IReadOnlyList<int> MissingRootIds,
    IReadOnlyList<int> ObjectiveRootIds,
    IReadOnlyList<WorkItemDto> RawItems,
    IReadOnlyList<WorkItemDto> EpicLikeItems,
    IReadOnlyList<WorkItemDto> RoadmapTaggedItems,
    IReadOnlyList<WorkItemDto> RoadmapEpicItems,
    IReadOnlyList<WorkItemDto> AvailableEpicItems,
    IReadOnlyList<RoadmapWorkItemTypeCount> CountsByType,
    IReadOnlyList<string> DistinctTypes,
    int DescendantsUnderConfiguredRootsCount,
    int EpicLikeItemsUnderConfiguredRootsCount,
    IReadOnlyList<RoadmapWorkItemSample> RawItemSamples,
    IReadOnlyList<RoadmapWorkItemSample> EpicLikeSamples,
    IReadOnlyList<RoadmapWorkItemSample> RoadmapTaggedSamples,
    IReadOnlyList<RoadmapWorkItemSample> RoadmapEpicSamples);

public sealed record RoadmapWorkItemTypeCount(string Type, int Count);

public sealed record RoadmapWorkItemSample(
    int TfsId,
    string Type,
    string Title,
    string? Tags,
    int? ParentTfsId,
    double? BacklogPriority);
