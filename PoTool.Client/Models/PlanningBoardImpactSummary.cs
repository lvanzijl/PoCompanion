using PoTool.Shared.Planning;

namespace PoTool.Client.Models;

public sealed record PlanningBoardActionImpactContext(
    string ActionName,
    int? EpicId,
    bool IsMaintenance = false);

public sealed record PlanningBoardImpactSummary(
    string Title,
    string Detail,
    IReadOnlyList<string> SummaryItems,
    IReadOnlyDictionary<int, IReadOnlyList<string>> EpicMessages,
    bool IsMaintenance);

public static class PlanningBoardImpactSummaryBuilder
{
    private const int MaxEpicImpactMessages = 3;

    public static PlanningBoardImpactSummary? Build(
        ProductPlanningBoardDto? previousBoard,
        ProductPlanningBoardDto currentBoard,
        PlanningBoardActionImpactContext context)
    {
        ArgumentNullException.ThrowIfNull(currentBoard);
        ArgumentNullException.ThrowIfNull(context);

        if (previousBoard is null)
        {
            return BuildFallback(currentBoard, context);
        }

        var previousEpics = ToEpicLookup(previousBoard);
        var changedEpics = currentBoard.EpicItems.Where(static epic => epic.IsChanged).ToArray();
        var affectedEpics = currentBoard.EpicItems.Where(static epic => epic.IsAffected && !epic.IsChanged).ToArray();
        var impactMessages = BuildEpicMessages(previousBoard, currentBoard, previousEpics);

        var summaryItems = new List<string>();
        var impactCountSummary = BuildImpactCountSummary(changedEpics.Length, affectedEpics.Length, context.IsMaintenance);
        if (!string.IsNullOrWhiteSpace(impactCountSummary))
        {
            summaryItems.Add(impactCountSummary);
        }

        if (TryBuildActedEpicSummary(currentBoard, previousEpics, context, out var actedEpicSummary))
        {
            summaryItems.Add(actedEpicSummary);
        }

        if (TryBuildShiftMagnitudeSummary(affectedEpics, previousEpics, out var shiftSummary))
        {
            summaryItems.Add(shiftSummary);
        }

        if (TryBuildParallelSummary(currentBoard, previousEpics, out var parallelSummary))
        {
            summaryItems.Add(parallelSummary);
        }

        foreach (var overlapSummary in BuildOverlapSummaries(previousBoard, currentBoard))
        {
            summaryItems.Add(overlapSummary);
        }

        if (!context.IsMaintenance)
        {
            foreach (var sprintSignalSummary in ProductPlanningSprintSignalFactory.BuildDeltaSummaries(previousBoard, currentBoard))
            {
                summaryItems.Add(sprintSignalSummary);
            }
        }

        if (context.IsMaintenance)
        {
            summaryItems.Add("Planning actions stay separate from reporting maintenance.");
        }

        var distinctSummaries = summaryItems
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var title = context.IsMaintenance ? "Latest reporting update" : "Latest planning impact";
        var detail = BuildDetail(changedEpics.Length, affectedEpics.Length, context.IsMaintenance, distinctSummaries);

        return new PlanningBoardImpactSummary(
            title,
            detail,
            distinctSummaries,
            impactMessages,
            context.IsMaintenance);
    }

    private static PlanningBoardImpactSummary? BuildFallback(ProductPlanningBoardDto currentBoard, PlanningBoardActionImpactContext context)
    {
        var changedCount = currentBoard.ChangedEpicIds.Count;
        var affectedCount = currentBoard.AffectedEpicIds.Count;
        if (changedCount == 0 && affectedCount == 0 && !context.IsMaintenance)
        {
            return null;
        }

        var summaryItems = new List<string>();
        var impactCountSummary = BuildImpactCountSummary(changedCount, affectedCount, context.IsMaintenance);
        if (!string.IsNullOrWhiteSpace(impactCountSummary))
        {
            summaryItems.Add(impactCountSummary);
        }

        if (context.IsMaintenance)
        {
            summaryItems.Add("Reported dates were refreshed from the plan without changing Epic timing.");
        }

        return new PlanningBoardImpactSummary(
            context.IsMaintenance ? "Latest reporting update" : "Latest planning impact",
            BuildDetail(changedCount, affectedCount, context.IsMaintenance, summaryItems),
            summaryItems,
            new Dictionary<int, IReadOnlyList<string>>(),
            context.IsMaintenance);
    }

    private static Dictionary<int, IReadOnlyList<string>> BuildEpicMessages(
        ProductPlanningBoardDto previousBoard,
        ProductPlanningBoardDto currentBoard,
        IReadOnlyDictionary<int, PlanningBoardEpicItemDto> previousEpics)
    {
        var overlapChanges = BuildOverlapChangeLookup(previousBoard, currentBoard);
        var messages = new Dictionary<int, IReadOnlyList<string>>();

        foreach (var epic in currentBoard.EpicItems.Where(static item => item.IsChanged || item.IsAffected))
        {
            var epicMessages = new List<string>();
            if (previousEpics.TryGetValue(epic.EpicId, out var previousEpic))
            {
                if (previousEpic.TrackIndex == 0 && epic.TrackIndex > 0)
                {
                    epicMessages.Add("Moved to parallel work.");
                }
                else if (previousEpic.TrackIndex > 0 && epic.TrackIndex == 0)
                {
                    epicMessages.Add("Returned to the main plan.");
                }

                var startDelta = epic.ComputedStartSprintIndex - previousEpic.ComputedStartSprintIndex;
                if (startDelta != 0)
                {
                    epicMessages.Add(epic.IsAffected && !epic.IsChanged
                        ? $"Shifted {FormatSprintDelta(startDelta)} due to upstream change."
                        : $"Moved {FormatSprintDelta(startDelta)}.");
                }

                if (epic.IsChanged && epic.RoadmapOrder != previousEpic.RoadmapOrder)
                {
                    epicMessages.Add($"Priority order is now #{epic.RoadmapOrder}.");
                }
            }

            if (overlapChanges.TryGetValue(epic.EpicId, out var overlapMessage))
            {
                epicMessages.Add(overlapMessage);
            }

            if (epicMessages.Count == 0)
            {
                epicMessages.Add(epic.IsChanged
                    ? "Changed directly in the latest planning action."
                    : "Affected by an upstream planning change.");
            }

            messages[epic.EpicId] = epicMessages.Distinct(StringComparer.Ordinal).Take(MaxEpicImpactMessages).ToArray();
        }

        return messages;
    }

    private static Dictionary<int, string> BuildOverlapChangeLookup(ProductPlanningBoardDto previousBoard, ProductPlanningBoardDto currentBoard)
    {
        var previousOverlaps = GetOverlapPairs(previousBoard);
        var currentOverlaps = GetOverlapPairs(currentBoard);
        var lookup = new Dictionary<int, string>();

        foreach (var pair in currentOverlaps.Except(previousOverlaps))
        {
            if (!lookup.ContainsKey(pair.FirstEpicId))
            {
                lookup[pair.FirstEpicId] = $"Now overlaps with {pair.SecondEpicTitle}.";
            }

            if (!lookup.ContainsKey(pair.SecondEpicId))
            {
                lookup[pair.SecondEpicId] = $"Now overlaps with {pair.FirstEpicTitle}.";
            }
        }

        foreach (var pair in previousOverlaps.Except(currentOverlaps))
        {
            if (!lookup.ContainsKey(pair.FirstEpicId))
            {
                lookup[pair.FirstEpicId] = $"No longer overlaps with {pair.SecondEpicTitle}.";
            }

            if (!lookup.ContainsKey(pair.SecondEpicId))
            {
                lookup[pair.SecondEpicId] = $"No longer overlaps with {pair.FirstEpicTitle}.";
            }
        }

        return lookup;
    }

    private static string BuildImpactCountSummary(int changedCount, int affectedCount, bool isMaintenance)
    {
        if (isMaintenance && changedCount == 0 && affectedCount == 0)
        {
            return "Reported dates were refreshed from the saved plan. No Epic timing changed.";
        }

        var directText = changedCount switch
        {
            1 => "1 Epic changed directly",
            _ => $"{changedCount} Epics changed directly"
        };

        var shiftedText = affectedCount switch
        {
            0 => "no additional Epics shifted",
            1 => "1 more Epic shifted",
            _ => $"{affectedCount} more Epics shifted"
        };

        return $"{directText}; {shiftedText}.";
    }

    private static string BuildDetail(int changedCount, int affectedCount, bool isMaintenance, IReadOnlyList<string> summaryItems)
    {
        if (isMaintenance && changedCount == 0 && affectedCount == 0)
        {
            return "Reporting data was updated without changing the plan.";
        }

        return summaryItems.Count > 0
            ? summaryItems[0]
            : $"{changedCount} Epic(s) changed directly and {affectedCount} more shifted.";
    }

    private static bool TryBuildActedEpicSummary(
        ProductPlanningBoardDto currentBoard,
        IReadOnlyDictionary<int, PlanningBoardEpicItemDto> previousEpics,
        PlanningBoardActionImpactContext context,
        out string summary)
    {
        summary = string.Empty;
        var currentEpics = ToEpicLookup(currentBoard);
        if (!context.EpicId.HasValue
            || !previousEpics.TryGetValue(context.EpicId.Value, out var previousEpic)
            || !currentEpics.TryGetValue(context.EpicId.Value, out var currentEpic))
        {
            return false;
        }

        if (previousEpic.TrackIndex == 0 && currentEpic.TrackIndex > 0)
        {
            summary = $"{currentEpic.EpicTitle} now runs in parallel.";
            return true;
        }

        if (previousEpic.TrackIndex > 0 && currentEpic.TrackIndex == 0)
        {
            summary = $"{currentEpic.EpicTitle} returned to the main plan.";
            return true;
        }

        var orderedEpics = currentBoard.EpicItems
            .OrderBy(static epic => epic.RoadmapOrder)
            .ThenBy(static epic => epic.ComputedStartSprintIndex)
            .ToArray();

        if (currentEpic.RoadmapOrder != previousEpic.RoadmapOrder)
        {
            var predecessor = orderedEpics
                .Where(epic => epic.RoadmapOrder < currentEpic.RoadmapOrder)
                .OrderByDescending(static epic => epic.RoadmapOrder)
                .FirstOrDefault();

            summary = predecessor is null
                ? $"{currentEpic.EpicTitle} is now first in the plan."
                : $"{currentEpic.EpicTitle} moved after {predecessor.EpicTitle}.";
            return true;
        }

        var startDelta = currentEpic.ComputedStartSprintIndex - previousEpic.ComputedStartSprintIndex;
        if (startDelta != 0)
        {
            summary = $"{currentEpic.EpicTitle} moved {FormatSprintDelta(startDelta)}.";
            return true;
        }

        return false;
    }

    private static bool TryBuildShiftMagnitudeSummary(
        IReadOnlyList<PlanningBoardEpicItemDto> affectedEpics,
        IReadOnlyDictionary<int, PlanningBoardEpicItemDto> previousEpics,
        out string summary)
    {
        summary = string.Empty;
        var deltas = affectedEpics
            .Select(epic => previousEpics.TryGetValue(epic.EpicId, out var previousEpic)
                ? epic.ComputedStartSprintIndex - previousEpic.ComputedStartSprintIndex
                : 0)
            .Where(static delta => delta != 0)
            .ToArray();

        if (deltas.Length == 0)
        {
            return false;
        }

        if (deltas.All(delta => delta == deltas[0]))
        {
            summary = $"{deltas.Length} Epic{(deltas.Length == 1 ? string.Empty : "s")} shifted by {FormatSprintDelta(deltas[0])}.";
            return true;
        }

        summary = $"{deltas.Length} Epics shifted. Range: {FormatSprintDelta(deltas.Min())} to {FormatSprintDelta(deltas.Max())}.";
        return true;
    }

    private static bool TryBuildParallelSummary(
        ProductPlanningBoardDto currentBoard,
        IReadOnlyDictionary<int, PlanningBoardEpicItemDto> previousEpics,
        out string summary)
    {
        summary = string.Empty;
        var movedToParallelCount = currentBoard.EpicItems.Count(epic =>
            previousEpics.TryGetValue(epic.EpicId, out var previousEpic)
            && previousEpic.TrackIndex == 0
            && epic.TrackIndex > 0);

        if (movedToParallelCount > 0)
        {
            summary = movedToParallelCount == 1
                ? "1 Epic now runs in parallel."
                : $"{movedToParallelCount} Epics now run in parallel.";
            return true;
        }

        var returnedToMainCount = currentBoard.EpicItems.Count(epic =>
            previousEpics.TryGetValue(epic.EpicId, out var previousEpic)
            && previousEpic.TrackIndex > 0
            && epic.TrackIndex == 0);

        if (returnedToMainCount > 0)
        {
            summary = returnedToMainCount == 1
                ? "1 Epic returned to the main plan."
                : $"{returnedToMainCount} Epics returned to the main plan.";
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> BuildOverlapSummaries(ProductPlanningBoardDto previousBoard, ProductPlanningBoardDto currentBoard)
    {
        var previousOverlaps = GetOverlapPairs(previousBoard);
        var currentOverlaps = GetOverlapPairs(currentBoard);
        var summaries = new List<string>();

        var introduced = currentOverlaps.Except(previousOverlaps).ToArray();
        if (introduced.Length > 0)
        {
            var pair = introduced[0];
            summaries.Add(introduced.Length == 1
                ? $"Overlap introduced between {pair.FirstEpicTitle} and {pair.SecondEpicTitle}."
                : $"Overlap introduced between {pair.FirstEpicTitle} and {pair.SecondEpicTitle}, plus {introduced.Length - 1} more overlap change(s).");
        }

        var removed = previousOverlaps.Except(currentOverlaps).ToArray();
        if (removed.Length > 0)
        {
            var pair = removed[0];
            summaries.Add(removed.Length == 1
                ? $"Overlap removed between {pair.FirstEpicTitle} and {pair.SecondEpicTitle}."
                : $"Overlap removed between {pair.FirstEpicTitle} and {pair.SecondEpicTitle}, plus {removed.Length - 1} more overlap change(s).");
        }

        return summaries;
    }

    private static HashSet<OverlapPair> GetOverlapPairs(ProductPlanningBoardDto board)
    {
        var overlaps = new HashSet<OverlapPair>();
        var epics = board.EpicItems
            .OrderBy(static epic => epic.ComputedStartSprintIndex)
            .ThenBy(static epic => epic.RoadmapOrder)
            .ToArray();

        for (var index = 0; index < epics.Length; index++)
        {
            for (var comparisonIndex = index + 1; comparisonIndex < epics.Length; comparisonIndex++)
            {
                if (epics[comparisonIndex].ComputedStartSprintIndex >= epics[index].EndSprintIndexExclusive)
                {
                    break;
                }

                if (!Overlaps(epics[index], epics[comparisonIndex]))
                {
                    continue;
                }

                overlaps.Add(OverlapPair.Create(epics[index], epics[comparisonIndex]));
            }
        }

        return overlaps;
    }

    private static Dictionary<int, PlanningBoardEpicItemDto> ToEpicLookup(ProductPlanningBoardDto board)
        => board.EpicItems
            .GroupBy(static epic => epic.EpicId)
            .ToDictionary(static group => group.Key, static group => group.Last());

    private static bool Overlaps(PlanningBoardEpicItemDto left, PlanningBoardEpicItemDto right)
        // EndSprintIndexExclusive is the first sprint outside the Epic range, so overlap uses half-open intervals.
        => left.ComputedStartSprintIndex < right.EndSprintIndexExclusive
           && right.ComputedStartSprintIndex < left.EndSprintIndexExclusive;

    private static string FormatSprintDelta(int delta)
    {
        var sprintLabel = Math.Abs(delta) == 1 ? "sprint" : "sprints";
        return $"{(delta > 0 ? "+" : string.Empty)}{delta} {sprintLabel}";
    }

    private readonly record struct OverlapPair(
        int FirstEpicId,
        int SecondEpicId,
        string FirstEpicTitle,
        string SecondEpicTitle)
    {
        public static OverlapPair Create(PlanningBoardEpicItemDto first, PlanningBoardEpicItemDto second)
            => first.EpicId <= second.EpicId
                ? new OverlapPair(first.EpicId, second.EpicId, first.EpicTitle, second.EpicTitle)
                : new OverlapPair(second.EpicId, first.EpicId, second.EpicTitle, first.EpicTitle);
    }
}
