using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services.MockData;

internal static class BattleshipPlanningBoardSeedCatalog
{
    internal const string PrimaryProductName = "Crew Safety Operations";
    internal const string SecondaryProductName = "Incident Response Control";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<SprintPlacementSeed>> PlacementsByProduct =
        new Dictionary<string, IReadOnlyList<SprintPlacementSeed>>(StringComparer.OrdinalIgnoreCase)
        {
            [PrimaryProductName] =
            [
                new SprintPlacementSeed(3, 1),
                new SprintPlacementSeed(4, 3),
                new SprintPlacementSeed(4, 2),
                new SprintPlacementSeed(5, 2),
                new SprintPlacementSeed(8, 4),
                new SprintPlacementSeed(9, 2)
            ],
            [SecondaryProductName] =
            [
                new SprintPlacementSeed(3, 1),
                new SprintPlacementSeed(3, 1),
                new SprintPlacementSeed(4, 1),
                new SprintPlacementSeed(5, 1),
                new SprintPlacementSeed(6, 1),
                new SprintPlacementSeed(7, 1),
                new SprintPlacementSeed(8, 1),
                new SprintPlacementSeed(9, 1),
                new SprintPlacementSeed(10, 1),
                new SprintPlacementSeed(11, 1),
                new SprintPlacementSeed(12, 1),
                new SprintPlacementSeed(13, 1),
                new SprintPlacementSeed(14, 1),
                new SprintPlacementSeed(14, 1)
            ]
        };

    internal static IReadOnlyList<BattleshipPlanningEpicSeed> CreateProductSeeds(
        string productName,
        IReadOnlyList<int> rootIds,
        IReadOnlyCollection<WorkItemDto> hierarchy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentNullException.ThrowIfNull(rootIds);
        ArgumentNullException.ThrowIfNull(hierarchy);

        if (!PlacementsByProduct.TryGetValue(productName, out var placements))
        {
            throw new InvalidOperationException(
                $"No Battleship planning-board placement seeds are registered for product '{productName}'.");
        }

        var roadmapEpics = WorkItemHierarchyHelper.FilterDescendants(rootIds, hierarchy)
            .Where(static workItem => string.Equals(workItem.Type, WorkItemType.Epic, StringComparison.OrdinalIgnoreCase))
            .Where(static workItem => HasRoadmapTag(workItem.Tags))
            .OrderBy(static workItem => workItem.BacklogPriority ?? double.MaxValue)
            .ThenBy(static workItem => workItem.TfsId)
            .ToArray();

        if (roadmapEpics.Length != placements.Count)
        {
            throw new InvalidOperationException(
                $"Battleship planning-board placement seeds for product '{productName}' expect {placements.Count} roadmap epics, but the generated hierarchy resolved {roadmapEpics.Length}.");
        }

        return roadmapEpics
            .Zip(
                placements,
                static (epic, placement) => new BattleshipPlanningEpicSeed(
                    epic.TfsId,
                    string.IsNullOrWhiteSpace(epic.Title) ? $"Epic {epic.TfsId}" : epic.Title,
                    placement.StartSprintNumber,
                    placement.DurationInSprints))
            .ToArray();
    }

    private static bool HasRoadmapTag(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return false;
        }

        return tags
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(static tag => string.Equals(tag, "roadmap", StringComparison.OrdinalIgnoreCase));
    }

    internal sealed record BattleshipPlanningEpicSeed(
        int EpicId,
        string EpicTitle,
        int StartSprintNumber,
        int DurationInSprints);

    private sealed record SprintPlacementSeed(
        int StartSprintNumber,
        int DurationInSprints);
}
