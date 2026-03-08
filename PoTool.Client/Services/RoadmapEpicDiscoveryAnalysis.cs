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

        var presentRootIds = normalizedRootIds
            .Where(rootId => configuredRootIdSet.Contains(rootId) && itemLookup.ContainsKey(rootId))
            .ToList();

        var missingRootIds = normalizedRootIds
            .Except(presentRootIds)
            .ToList();

        return new RoadmapEpicDiscoveryReport(
            normalizedRootIds,
            presentRootIds,
            missingRootIds,
            rawItems,
            epicLikeItems,
            roadmapEpicItems,
            availableEpicItems);
    }
}

public sealed record RoadmapEpicDiscoveryReport(
    IReadOnlyList<int> ConfiguredRootIds,
    IReadOnlyList<int> PresentRootIds,
    IReadOnlyList<int> MissingRootIds,
    IReadOnlyList<WorkItemDto> RawItems,
    IReadOnlyList<WorkItemDto> EpicLikeItems,
    IReadOnlyList<WorkItemDto> RoadmapEpicItems,
    IReadOnlyList<WorkItemDto> AvailableEpicItems);
