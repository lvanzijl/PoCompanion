using PoTool.Shared.Planning;

namespace PoTool.Client.Models;

public enum ProductPlanningBoardStatusKind
{
    Stable,
    Changed,
    Warning
}

public sealed record ProductPlanningBoardRenderModel(
    ProductPlanningBoardDto Board,
    IReadOnlyList<ProductPlanningSprintColumn> SprintColumns,
    IReadOnlyList<ProductPlanningTrackRow> Tracks,
    int MaxSprintCount,
    ProductPlanningBoardStatusKind StatusKind,
    string StatusLabel,
    string StatusDetail,
    bool HasValidationIssues,
    bool HasRecentChanges);

public sealed record ProductPlanningSprintColumn(int SprintIndex, string Label);

public sealed record ProductPlanningTrackRow(
    int TrackIndex,
    bool IsMainLane,
    string Title,
    string Subtitle,
    IReadOnlyList<PlanningBoardEpicItemDto> Epics);

public static class ProductPlanningBoardRenderModelFactory
{
    public static ProductPlanningBoardRenderModel Create(ProductPlanningBoardDto board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var epicById = board.EpicItems.ToDictionary(static epic => epic.EpicId);
        var tracks = board.Tracks
            .OrderBy(static track => track.TrackIndex)
            .Select(track =>
            {
                var epics = track.EpicIds
                    .Select(epicId => epicById.GetValueOrDefault(epicId))
                    .Where(static epic => epic is not null)
                    .Cast<PlanningBoardEpicItemDto>()
                    .OrderBy(static epic => epic.ComputedStartSprintIndex)
                    .ThenBy(static epic => epic.RoadmapOrder)
                    .ToArray();

                return new ProductPlanningTrackRow(
                    track.TrackIndex,
                    track.IsMainLane,
                    track.IsMainLane ? "Main lane" : $"Parallel track {track.TrackIndex}",
                    track.IsMainLane ? "Primary roadmap sequence" : "Derived secondary execution track",
                    epics);
            })
            .ToArray();

        var maxSprintCount = Math.Max(1, board.EpicItems.Count == 0 ? 0 : board.EpicItems.Max(static epic => epic.EndSprintIndexExclusive));
        var sprintColumns = Enumerable.Range(0, maxSprintCount)
            .Select(index => new ProductPlanningSprintColumn(index, $"Sprint {index + 1}"))
            .ToArray();

        var hasValidationIssues = board.Issues.Count > 0;
        var hasRecentChanges = board.ChangedEpicIds.Count > 0 || board.AffectedEpicIds.Count > 0;
        var (statusKind, statusLabel, statusDetail) = ResolveStatus(board, hasValidationIssues, hasRecentChanges);

        return new ProductPlanningBoardRenderModel(
            board,
            sprintColumns,
            tracks,
            maxSprintCount,
            statusKind,
            statusLabel,
            statusDetail,
            hasValidationIssues,
            hasRecentChanges);
    }

    private static (ProductPlanningBoardStatusKind Kind, string Label, string Detail) ResolveStatus(
        ProductPlanningBoardDto board,
        bool hasValidationIssues,
        bool hasRecentChanges)
    {
        if (hasValidationIssues)
        {
            return (
                ProductPlanningBoardStatusKind.Warning,
                "Validation issues present",
                $"{board.Issues.Count} issue(s) need attention before the plan is considered stable.");
        }

        if (hasRecentChanges)
        {
            return (
                ProductPlanningBoardStatusKind.Changed,
                "Working session updated",
                $"{board.ChangedEpicIds.Count} changed epic(s) and {board.AffectedEpicIds.Count} affected epic(s) are highlighted from the latest operation.");
        }

        return (
            ProductPlanningBoardStatusKind.Stable,
            "Board matches current durable base",
            "No recent operation deltas are highlighted, and the board is ready for the next explicit planning decision.");
    }
}
