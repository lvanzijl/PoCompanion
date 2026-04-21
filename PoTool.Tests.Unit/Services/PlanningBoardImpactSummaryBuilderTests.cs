using PoTool.Client.Models;
using PoTool.Shared.Planning;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PlanningBoardImpactSummaryBuilderTests
{
    [TestMethod]
    public void Build_MoveAction_QuantifiesShiftMagnitudeAndEpicMessages()
    {
        var previousBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, plannedStart: 0, computedStart: 0, duration: 2, isChanged: false, isAffected: false),
            CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 0, plannedStart: 2, computedStart: 2, duration: 2, isChanged: false, isAffected: false),
            CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 0, plannedStart: 4, computedStart: 4, duration: 2, isChanged: false, isAffected: false));

        var currentBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, plannedStart: 1, computedStart: 1, duration: 2, isChanged: true, isAffected: false),
            CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 0, plannedStart: 3, computedStart: 3, duration: 2, isChanged: false, isAffected: true),
            CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 0, plannedStart: 5, computedStart: 5, duration: 2, isChanged: false, isAffected: true),
            changedEpicIds: [101],
            affectedEpicIds: [102, 103]);

        var summary = PlanningBoardImpactSummaryBuilder.Build(
            previousBoard,
            currentBoard,
            new PlanningBoardActionImpactContext("Move Epic", 101));

        Assert.IsNotNull(summary);
        CollectionAssert.Contains(summary.SummaryItems.ToArray(), "1 Epic changed directly; 2 more Epics shifted.");
        CollectionAssert.Contains(summary.SummaryItems.ToArray(), "Epic A moved +1 sprint.");
        CollectionAssert.Contains(summary.SummaryItems.ToArray(), "2 Epics shifted by +1 sprint.");
        CollectionAssert.Contains(summary.EpicMessages[102].ToArray(), "Shifted +1 sprint due to upstream change.");
    }

    [TestMethod]
    public void Build_ParallelAction_ReportsStructuralAndOverlapChanges()
    {
        var previousBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, plannedStart: 0, computedStart: 0, duration: 2, isChanged: false, isAffected: false),
            CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 0, plannedStart: 2, computedStart: 2, duration: 2, isChanged: false, isAffected: false));

        var currentBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, plannedStart: 0, computedStart: 0, duration: 2, isChanged: false, isAffected: false),
            CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 1, plannedStart: 1, computedStart: 1, duration: 2, isChanged: true, isAffected: false),
            changedEpicIds: [102],
            affectedEpicIds: []);

        var summary = PlanningBoardImpactSummaryBuilder.Build(
            previousBoard,
            currentBoard,
            new PlanningBoardActionImpactContext("Create parallel work", 102));

        Assert.IsNotNull(summary);
        CollectionAssert.Contains(summary.SummaryItems.ToArray(), "Epic B now runs in parallel.");
        CollectionAssert.Contains(summary.SummaryItems.ToArray(), "1 Epic now runs in parallel.");
        CollectionAssert.Contains(summary.SummaryItems.ToArray(), "Overlap introduced between Epic A and Epic B.");
        CollectionAssert.Contains(summary.EpicMessages[102].ToArray(), "Moved to parallel work.");
        CollectionAssert.Contains(summary.EpicMessages[102].ToArray(), "Now overlaps with Epic A.");
    }

    [TestMethod]
    public void Build_PlanningAction_ReportsRiskAndConfidenceShiftBySprint()
    {
        var previousBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, plannedStart: 0, computedStart: 0, duration: 2, isChanged: false, isAffected: false),
            CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 1, plannedStart: 2, computedStart: 2, duration: 2, isChanged: false, isAffected: false),
            CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 2, plannedStart: 3, computedStart: 3, duration: 2, isChanged: false, isAffected: false));

        var currentBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, plannedStart: 0, computedStart: 0, duration: 2, isChanged: true, isAffected: false),
            CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 1, plannedStart: 1, computedStart: 1, duration: 3, isChanged: false, isAffected: true),
            CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 2, plannedStart: 1, computedStart: 1, duration: 2, isChanged: false, isAffected: true),
            changedEpicIds: [101],
            affectedEpicIds: [102, 103]);

        var summary = PlanningBoardImpactSummaryBuilder.Build(
            previousBoard,
            currentBoard,
            new PlanningBoardActionImpactContext("Move Epic", 101));

        Assert.IsNotNull(summary);
        CollectionAssert.Contains(summary.SummaryItems.ToArray(), "Sprint 2 now suggests higher planning strain than usual.");
        CollectionAssert.Contains(summary.SummaryItems.ToArray(), "Sprint 2 now looks more provisional after recent changes.");
        Assert.IsFalse(summary.SummaryItems.Any(static item => item.Contains("will deliver", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(summary.SummaryItems.Any(static item => item.Contains("Confidence", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(
            new[]
            {
                "1 Epic changed directly; 2 more Epics shifted.",
                "Sprint 2 now suggests higher planning strain than usual.",
                "Sprint 2 now looks more provisional after recent changes."
            },
            summary.SummaryItems.Take(3).ToArray());
    }

    [TestMethod]
    public void Build_MaintenanceAction_KeepsPlanningAndReportingSeparate()
    {
        var previousBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, plannedStart: 0, computedStart: 0, duration: 2, isChanged: false, isAffected: false));

        var currentBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, plannedStart: 0, computedStart: 0, duration: 2, isChanged: false, isAffected: false));

        var summary = PlanningBoardImpactSummaryBuilder.Build(
            previousBoard,
            currentBoard,
            new PlanningBoardActionImpactContext("Update reporting data", 101, IsMaintenance: true));

        Assert.IsNotNull(summary);
        Assert.IsTrue(summary.IsMaintenance);
        Assert.AreEqual("Latest reporting update", summary.Title);
        CollectionAssert.Contains(summary.SummaryItems.ToArray(), "Reported dates were refreshed from the saved plan. No Epic timing changed.");
        CollectionAssert.Contains(summary.SummaryItems.ToArray(), "Planning actions stay separate from reporting maintenance.");
        Assert.IsFalse(summary.SummaryItems.Any(static item => item.Contains("Confidence", StringComparison.Ordinal)));
    }

    private static ProductPlanningBoardDto CreateBoard(
        PlanningBoardEpicItemDto epic1,
        PlanningBoardEpicItemDto? epic2 = null,
        PlanningBoardEpicItemDto? epic3 = null,
        IReadOnlyList<int>? changedEpicIds = null,
        IReadOnlyList<int>? affectedEpicIds = null)
    {
        var epics = new[] { epic1, epic2, epic3 }
            .Where(static epic => epic is not null)
            .Cast<PlanningBoardEpicItemDto>()
            .ToArray();

        var tracks = epics
            .GroupBy(static epic => epic.TrackIndex)
            .OrderBy(static group => group.Key)
            .Select(group => new PlanningBoardTrackDto(group.Key, group.Key == 0, group.Select(static epic => epic.EpicId).ToArray()))
            .ToArray();

        return new ProductPlanningBoardDto(
            7,
            "Roadmap Product",
            tracks,
            epics,
            [],
            changedEpicIds ?? [],
            affectedEpicIds ?? []);
    }

    private static PlanningBoardEpicItemDto CreateEpic(
        int epicId,
        string title,
        int roadmapOrder,
        int trackIndex,
        int plannedStart,
        int computedStart,
        int duration,
        bool isChanged,
        bool isAffected)
        => new(
            epicId,
            title,
            roadmapOrder,
            trackIndex,
            plannedStart,
            computedStart,
            duration,
            computedStart + duration,
            [],
            isChanged,
            isAffected);
}
