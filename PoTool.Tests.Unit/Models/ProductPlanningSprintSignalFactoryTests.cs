using PoTool.Client.Models;
using PoTool.Shared.Planning;

namespace PoTool.Tests.Unit.Models;

[TestClass]
public sealed class ProductPlanningSprintSignalFactoryTests
{
    [TestMethod]
    public void BuildColumns_ClassifiesHighRiskLowConfidenceSprintAndBuildsHeatStyle()
    {
        var previousBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 2),
            CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 1, computedStart: 2, duration: 2),
            CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 2, computedStart: 3, duration: 2));

        var currentBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 2, isChanged: true),
            CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 1, computedStart: 1, duration: 3, isAffected: true),
            CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 2, computedStart: 1, duration: 2, isAffected: true),
            changedEpicIds: [101],
            affectedEpicIds: [102, 103]);

        var columns = ProductPlanningSprintSignalFactory.BuildColumns(currentBoard, sprintCount: 4, previousBoard);
        var sprintTwo = columns[1];

        Assert.AreEqual(PlanningBoardSprintRiskLevel.High, sprintTwo.RiskLevel);
        Assert.AreEqual(PlanningBoardSprintConfidenceLevel.Low, sprintTwo.ConfidenceLevel);
        CollectionAssert.Contains(sprintTwo.ExplanationChips.ToArray(), "High load");
        CollectionAssert.Contains(sprintTwo.ExplanationChips.ToArray(), "Parallel work high");
        CollectionAssert.Contains(sprintTwo.ExplanationChips.ToArray(), "Plan frequently changed");
        StringAssert.Contains(sprintTwo.HeatStyle, "198, 40, 40");
        StringAssert.Contains(sprintTwo.HeatStyle, "0.10");
    }

    [TestMethod]
    public void BuildColumns_UsesPlanningLanguageForStableNearTermSprint()
    {
        var board = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 1));

        var columns = ProductPlanningSprintSignalFactory.BuildColumns(board, sprintCount: 2);
        var sprintOne = columns[0];

        Assert.AreEqual(PlanningBoardSprintRiskLevel.Low, sprintOne.RiskLevel);
        Assert.AreEqual(PlanningBoardSprintConfidenceLevel.High, sprintOne.ConfidenceLevel);
        CollectionAssert.Contains(sprintOne.ExplanationChips.ToArray(), "Load in range");
        CollectionAssert.Contains(sprintOne.ExplanationChips.ToArray(), "Confidence steady");
        StringAssert.Contains(sprintOne.Tooltip, "manageable");
        StringAssert.Contains(sprintOne.Tooltip, "relatively steady");
        Assert.AreEqual("Risk low", sprintOne.RiskLabel);
        Assert.AreEqual("Confidence high", sprintOne.ConfidenceLabel);
    }

    private static ProductPlanningBoardDto CreateBoard(
        params PlanningBoardEpicItemDto[] epics)
        => CreateBoard(epics, changedEpicIds: [], affectedEpicIds: []);

    private static ProductPlanningBoardDto CreateBoard(
        PlanningBoardEpicItemDto epic1,
        PlanningBoardEpicItemDto epic2,
        PlanningBoardEpicItemDto epic3,
        IReadOnlyList<int> changedEpicIds,
        IReadOnlyList<int> affectedEpicIds)
        => CreateBoard([epic1, epic2, epic3], changedEpicIds, affectedEpicIds);

    private static ProductPlanningBoardDto CreateBoard(
        IReadOnlyList<PlanningBoardEpicItemDto> epics,
        IReadOnlyList<int> changedEpicIds,
        IReadOnlyList<int> affectedEpicIds)
    {
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
            changedEpicIds,
            affectedEpicIds);
    }

    private static PlanningBoardEpicItemDto CreateEpic(
        int epicId,
        string title,
        int roadmapOrder,
        int trackIndex,
        int computedStart,
        int duration,
        bool isChanged = false,
        bool isAffected = false)
        => new(
            epicId,
            title,
            roadmapOrder,
            trackIndex,
            computedStart,
            computedStart,
            duration,
            computedStart + duration,
            [],
            isChanged,
            isAffected);
}
