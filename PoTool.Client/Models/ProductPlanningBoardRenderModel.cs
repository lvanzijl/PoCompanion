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
    bool HasRecentChanges,
    bool HasOperationalDiagnostics,
    bool HasBlockingDiagnostics,
    int RecoveredEpicCount,
    int DriftedEpicCount,
    int BlockingDiagnosticCount);

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
                    track.IsMainLane ? "Main plan" : $"Parallel lane {track.TrackIndex}",
                    track.IsMainLane ? "Primary sequence on this board" : "Parallel work derived automatically from the plan",
                    epics);
            })
            .ToArray();

        var maxSprintCount = Math.Max(1, board.EpicItems.Count == 0 ? 0 : board.EpicItems.Max(static epic => epic.EndSprintIndexExclusive));
        var sprintColumns = Enumerable.Range(0, maxSprintCount)
            .Select(index => new ProductPlanningSprintColumn(index, $"Sprint {index + 1}"))
            .ToArray();

        var hasValidationIssues = board.Issues.Count > 0;
        var hasRecentChanges = board.ChangedEpicIds.Count > 0 || board.AffectedEpicIds.Count > 0;
        var recoveredEpicCount = board.EpicItems.Count(static epic => epic.IntentSource == PlanningBoardIntentSource.Recovered);
        var driftedEpicCount = board.EpicItems.Count(static epic => epic.DriftStatus.HasValue && epic.DriftStatus.Value != PlanningBoardDriftStatus.NoDrift);
        var blockingDiagnosticCount = (board.Diagnostics ?? Array.Empty<PlanningBoardDiagnosticDto>()).Count(static diagnostic => diagnostic.IsBlocking) +
                                      board.EpicItems.Sum(static epic => (epic.Diagnostics ?? Array.Empty<PlanningBoardDiagnosticDto>()).Count(diagnostic => diagnostic.IsBlocking));
        var hasOperationalDiagnostics = (board.Diagnostics?.Count ?? 0) > 0 ||
                                        board.EpicItems.Any(static epic => (epic.Diagnostics?.Count ?? 0) > 0);
        var hasBlockingDiagnostics = blockingDiagnosticCount > 0;
        var (statusKind, statusLabel, statusDetail) = ResolveStatus(
            board,
            hasValidationIssues,
            hasRecentChanges,
            hasOperationalDiagnostics,
            hasBlockingDiagnostics,
            recoveredEpicCount,
            driftedEpicCount,
            blockingDiagnosticCount);

        return new ProductPlanningBoardRenderModel(
            board,
            sprintColumns,
            tracks,
            maxSprintCount,
            statusKind,
            statusLabel,
            statusDetail,
            hasValidationIssues,
            hasRecentChanges,
            hasOperationalDiagnostics,
            hasBlockingDiagnostics,
            recoveredEpicCount,
            driftedEpicCount,
            blockingDiagnosticCount);
    }

    private static (ProductPlanningBoardStatusKind Kind, string Label, string Detail) ResolveStatus(
        ProductPlanningBoardDto board,
        bool hasValidationIssues,
        bool hasRecentChanges,
        bool hasOperationalDiagnostics,
        bool hasBlockingDiagnostics,
        int recoveredEpicCount,
        int driftedEpicCount,
        int blockingDiagnosticCount)
    {
        if (hasBlockingDiagnostics)
        {
            return (
                ProductPlanningBoardStatusKind.Warning,
                "Plan needs attention",
                $"{blockingDiagnosticCount} blocking issue(s) affect the plan or the dates reported to TFS.");
        }

        if (hasOperationalDiagnostics)
        {
            return (
                ProductPlanningBoardStatusKind.Warning,
                "Check reporting details",
                $"{recoveredEpicCount} epic(s) were imported from existing data and {driftedEpicCount} epic(s) are out of sync with TFS.");
        }

        if (hasValidationIssues)
        {
            return (
                ProductPlanningBoardStatusKind.Warning,
                "Planning issues present",
                $"{board.Issues.Count} issue(s) need attention before the plan is considered stable.");
        }

        if (hasRecentChanges)
        {
            return (
                ProductPlanningBoardStatusKind.Changed,
                "Plan updated",
                $"{board.ChangedEpicIds.Count} changed epic(s) and {board.AffectedEpicIds.Count} affected epic(s) are highlighted from your latest action.");
        }

        return (
            ProductPlanningBoardStatusKind.Stable,
            "Saved plan loaded",
            "This board defines the plan, and no recent changes are highlighted.");
    }
}
