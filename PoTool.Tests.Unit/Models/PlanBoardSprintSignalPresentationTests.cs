using MudBlazor;
using PoTool.Client.Models;

namespace PoTool.Tests.Unit.Models;

[TestClass]
public sealed class PlanBoardSprintSignalPresentationTests
{
    [TestMethod]
    public void GetRiskChipColor_UsesNeutralPresentationForLowRisk()
    {
        Assert.AreEqual(Variant.Outlined, PlanBoardSprintSignalPresentation.GetRiskChipVariant(PlanningBoardSprintRiskLevel.Low));
        Assert.AreEqual(Color.Default, PlanBoardSprintSignalPresentation.GetRiskChipColor(PlanningBoardSprintRiskLevel.Low));
    }

    [TestMethod]
    public void GetConfidenceChipColor_UsesNeutralPresentationForHighConfidence()
    {
        Assert.AreEqual(Variant.Text, PlanBoardSprintSignalPresentation.GetConfidenceChipVariant(PlanningBoardSprintConfidenceLevel.High));
        Assert.AreEqual(Color.Default, PlanBoardSprintSignalPresentation.GetConfidenceChipColor(PlanningBoardSprintConfidenceLevel.High));
    }

    [TestMethod]
    public void GetDeltaSummaryForSprint_FindsMatchingSprintSummaryAndSeparatesOtherItems()
    {
        var impactSummary = new PlanningBoardImpactSummary(
            "Latest planning impact",
            "Detail",
            [
                "1 Epic changed directly; 2 more Epics shifted.",
                "Sprint 2 now suggests higher planning strain than usual.",
                "Sprint 2 now looks more provisional after recent changes."
            ],
            new Dictionary<int, IReadOnlyList<string>>(),
            IsMaintenance: false);

        var sprint = new ProductPlanningSprintColumn(
            1,
            "Sprint 2",
            PlanningBoardSprintRiskLevel.High,
            PlanningBoardSprintConfidenceLevel.Low,
            "Strain elevated",
            "Plan provisional",
            "style",
            ["Parallel work high", "Plan frequently changed"],
            "Tooltip");

        Assert.AreEqual(
            "Sprint 2 now suggests higher planning strain than usual.",
            PlanBoardSprintSignalPresentation.GetDeltaSummaryForSprint(impactSummary, sprint));
        CollectionAssert.AreEqual(
            new[]
            {
                "Sprint 2 now suggests higher planning strain than usual.",
                "Sprint 2 now looks more provisional after recent changes."
            },
            PlanBoardSprintSignalPresentation.GetSignalDeltaSummaries(impactSummary).ToArray());
        CollectionAssert.AreEqual(
            new[] { "1 Epic changed directly; 2 more Epics shifted." },
            PlanBoardSprintSignalPresentation.GetNonSignalSummaries(impactSummary).ToArray());
    }

    [TestMethod]
    public void GetExplanationChipPresentation_EmphasizesOnlyTheDominantChipForAttentionStates()
    {
        var sprint = new ProductPlanningSprintColumn(
            1,
            "Sprint 2",
            PlanningBoardSprintRiskLevel.Medium,
            PlanningBoardSprintConfidenceLevel.Medium,
            "Needs attention",
            "Plan less settled",
            "style",
            ["Board load already high", "Far-future view provisional"],
            "Tooltip");

        Assert.IsTrue(PlanBoardSprintSignalPresentation.IsPrimaryExplanationChip(sprint, 0));
        Assert.AreEqual(Variant.Outlined, PlanBoardSprintSignalPresentation.GetExplanationChipVariant(sprint, 0));
        Assert.AreEqual(Color.Warning, PlanBoardSprintSignalPresentation.GetExplanationChipColor(sprint, 0));
        Assert.IsFalse(PlanBoardSprintSignalPresentation.IsPrimaryExplanationChip(sprint, 1));
        Assert.AreEqual(Variant.Text, PlanBoardSprintSignalPresentation.GetExplanationChipVariant(sprint, 1));
        Assert.AreEqual(Color.Default, PlanBoardSprintSignalPresentation.GetExplanationChipColor(sprint, 1));
    }
}
