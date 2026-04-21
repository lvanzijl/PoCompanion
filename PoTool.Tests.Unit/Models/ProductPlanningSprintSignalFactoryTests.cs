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
        CollectionAssert.Contains(sprintTwo.ExplanationChips.ToArray(), "Parallel work high");
        CollectionAssert.Contains(sprintTwo.ExplanationChips.ToArray(), "Plan frequently changed");
        StringAssert.Contains(sprintTwo.Tooltip, "Based on the current plan");
        StringAssert.Contains(sprintTwo.Tooltip, "suggests higher planning strain");
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
        CollectionAssert.Contains(sprintOne.ExplanationChips.ToArray(), "Load within board norm");
        CollectionAssert.Contains(sprintOne.ExplanationChips.ToArray(), "Near-term plan stable");
        StringAssert.Contains(sprintOne.Tooltip, "Based on the current plan");
        StringAssert.Contains(sprintOne.Tooltip, "looks relatively stable");
        Assert.IsFalse(sprintOne.Tooltip.Contains("guarantee", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(sprintOne.Tooltip.Contains("will deliver", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("Within typical range", sprintOne.RiskLabel);
        Assert.AreEqual("Plan stable (near-term)", sprintOne.ConfidenceLabel);
    }

    [TestMethod]
    public void BuildColumns_NormalizesTypicalDenseBoardLoadInsteadOfRecoloringRawCounts()
    {
        var board = CreateBoard(
            [
                CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 4),
                CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 1, computedStart: 0, duration: 4),
                CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 0, computedStart: 0, duration: 4)
            ],
            changedEpicIds: [],
            affectedEpicIds: []);

        var columns = ProductPlanningSprintSignalFactory.BuildColumns(board, sprintCount: 4);
        var sprintTwo = columns[1];
        var visibleText = string.Join(" | ", sprintTwo.ExplanationChips) + " | " + sprintTwo.Tooltip;

        Assert.AreEqual(PlanningBoardSprintRiskLevel.Low, sprintTwo.RiskLevel);
        CollectionAssert.Contains(sprintTwo.ExplanationChips.ToArray(), "Load within board norm");
        Assert.IsFalse(visibleText.Contains("score", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(visibleText.Contains("ratio", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(visibleText.Any(char.IsDigit));
    }

    [TestMethod]
    public void BuildColumns_KeepsMinorFarFutureChangeFromDroppingStraightToLowConfidence()
    {
        var previousBoard = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 7));

        var currentBoard = CreateBoard(
            [
                CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 7, isAffected: true)
            ],
            changedEpicIds: [],
            affectedEpicIds: [101]);

        var columns = ProductPlanningSprintSignalFactory.BuildColumns(currentBoard, sprintCount: 7, previousBoard);

        Assert.AreEqual(PlanningBoardSprintConfidenceLevel.High, columns[0].ConfidenceLevel);
        Assert.AreEqual(PlanningBoardSprintConfidenceLevel.Medium, columns[^1].ConfidenceLevel);
        Assert.AreNotEqual(PlanningBoardSprintConfidenceLevel.Low, columns[^1].ConfidenceLevel);
        Assert.AreEqual("Far-future view provisional", columns[^1].ExplanationChips[0]);
        CollectionAssert.Contains(columns[^1].ExplanationChips.ToArray(), "Load within board norm");
    }

    [TestMethod]
    public void BuildColumns_DecaysConfidenceGraduallyAcrossStablePlanningHorizon()
    {
        var board = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 7));

        var columns = ProductPlanningSprintSignalFactory.BuildColumns(board, sprintCount: 7);
        var confidenceRanks = columns.Select(static column => GetConfidenceRank(column.ConfidenceLevel)).ToArray();

        Assert.AreEqual(PlanningBoardSprintConfidenceLevel.High, columns[0].ConfidenceLevel);
        Assert.AreEqual(PlanningBoardSprintConfidenceLevel.Medium, columns[^1].ConfidenceLevel);
        Assert.AreEqual(PlanningBoardSprintConfidenceLevel.Medium, columns[3].ConfidenceLevel);
        CollectionAssert.Contains(columns[3].ExplanationChips.ToArray(), "Far-future view provisional");

        for (var index = 1; index < confidenceRanks.Length; index++)
        {
            Assert.IsGreaterThanOrEqualTo(confidenceRanks[index - 1], confidenceRanks[index], "Confidence should not improve in later stable sprints.");
            Assert.IsLessThanOrEqualTo(1, confidenceRanks[index] - confidenceRanks[index - 1], "Confidence decay should be gradual.");
        }
    }

    [TestMethod]
    public void BuildColumns_UsesInterpretiveLanguageInsteadOfDeliveryCertainty()
    {
        var board = CreateBoard(
            CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 1));

        var sprintOne = ProductPlanningSprintSignalFactory.BuildColumns(board, sprintCount: 2)[0];
        var visibleText = string.Join(" | ", sprintOne.ExplanationChips) + " | " + sprintOne.Tooltip + " | " + sprintOne.RiskLabel + " | " + sprintOne.ConfidenceLabel;

        StringAssert.Contains(visibleText, "Based on the current plan");
        Assert.IsFalse(visibleText.Contains("guarantee", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(visibleText.Contains("will deliver", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(visibleText.Contains("safe plan", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(visibleText.Contains("Confidence high", StringComparison.Ordinal));
        Assert.IsFalse(visibleText.Contains("Risk low", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildColumns_SeparatesNearTermRiskFromConfidenceWithoutGlobalScore()
    {
        var board = CreateBoard(
            [
                CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 1),
                CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 1, computedStart: 0, duration: 1),
                CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 2, computedStart: 0, duration: 1),
                CreateEpic(104, "Epic D", roadmapOrder: 4, trackIndex: 0, computedStart: 0, duration: 1),
                CreateEpic(105, "Epic E", roadmapOrder: 5, trackIndex: 1, computedStart: 0, duration: 1)
            ],
            changedEpicIds: [],
            affectedEpicIds: []);

        var sprintOne = ProductPlanningSprintSignalFactory.BuildColumns(board, sprintCount: 1)[0];

        Assert.AreEqual(PlanningBoardSprintRiskLevel.High, sprintOne.RiskLevel);
        Assert.AreEqual(PlanningBoardSprintConfidenceLevel.High, sprintOne.ConfidenceLevel);
        Assert.IsFalse(typeof(ProductPlanningSprintColumn).GetProperties().Any(static property => property.Name.Contains("Score", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void BuildColumns_SurfacesSystemicOverloadOnChronicallyHotBoard()
    {
        var board = CreateBoard(
            [
                CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 4),
                CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 1, computedStart: 0, duration: 4),
                CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 0, computedStart: 0, duration: 4),
                CreateEpic(104, "Epic D", roadmapOrder: 4, trackIndex: 1, computedStart: 0, duration: 4)
            ],
            changedEpicIds: [],
            affectedEpicIds: []);

        var sprintTwo = ProductPlanningSprintSignalFactory.BuildColumns(board, sprintCount: 4)[1];

        Assert.AreEqual(PlanningBoardSprintRiskLevel.Medium, sprintTwo.RiskLevel);
        CollectionAssert.Contains(sprintTwo.ExplanationChips.ToArray(), "Board load already high");
        StringAssert.Contains(sprintTwo.Tooltip, "already carrying a heavy load");
    }

    [TestMethod]
    public void BuildColumns_KeepsDominantOverlapVisibleWhenWorkIsAlsoPulledForward()
    {
        var previousBoard = CreateBoard(
            [
                CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 4),
                CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 0, computedStart: 4, duration: 2),
                CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 0, computedStart: 1, duration: 4)
            ],
            changedEpicIds: [],
            affectedEpicIds: []);

        var currentBoard = CreateBoard(
            [
                CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 4),
                CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 0, computedStart: 2, duration: 3, isAffected: true),
                CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 0, computedStart: 1, duration: 4)
            ],
            changedEpicIds: [],
            affectedEpicIds: [102]);

        var sprintThree = ProductPlanningSprintSignalFactory.BuildColumns(currentBoard, sprintCount: 6, previousBoard)[2];

        Assert.AreEqual("Overlap above board norm", sprintThree.ExplanationChips[0]);
        CollectionAssert.Contains(sprintThree.ExplanationChips.ToArray(), "Recent plan changes");
        StringAssert.Contains(sprintThree.Tooltip, "more Epics overlap here than the board typically carries");
    }

    [TestMethod]
    public void BuildColumns_PutsHighRiskAndLowConfidenceSignalsAheadOfNeutralChips()
    {
        var previousBoard = CreateBoard(
            [
                CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 2),
                CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 1, computedStart: 2, duration: 2),
                CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 2, computedStart: 3, duration: 2)
            ],
            changedEpicIds: [],
            affectedEpicIds: []);

        var currentBoard = CreateBoard(
            [
                CreateEpic(101, "Epic A", roadmapOrder: 1, trackIndex: 0, computedStart: 0, duration: 2, isChanged: true),
                CreateEpic(102, "Epic B", roadmapOrder: 2, trackIndex: 1, computedStart: 1, duration: 3, isAffected: true),
                CreateEpic(103, "Epic C", roadmapOrder: 3, trackIndex: 2, computedStart: 1, duration: 2, isAffected: true)
            ],
            changedEpicIds: [101],
            affectedEpicIds: [102, 103]);

        var sprintTwo = ProductPlanningSprintSignalFactory.BuildColumns(currentBoard, sprintCount: 4, previousBoard)[1];

        Assert.AreEqual(PlanningBoardSprintRiskLevel.High, sprintTwo.RiskLevel);
        Assert.AreEqual(PlanningBoardSprintConfidenceLevel.Low, sprintTwo.ConfidenceLevel);
        CollectionAssert.DoesNotContain(sprintTwo.ExplanationChips.ToArray(), "Load within board norm");
        CollectionAssert.AreEquivalent(
            new[] { "Parallel work high", "Plan frequently changed" },
            sprintTwo.ExplanationChips.Take(2).ToArray());
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

    private static int GetConfidenceRank(PlanningBoardSprintConfidenceLevel level)
        => level switch
        {
            PlanningBoardSprintConfidenceLevel.High => 0,
            PlanningBoardSprintConfidenceLevel.Medium => 1,
            _ => 2
        };
}
