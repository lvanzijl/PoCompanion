using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Services;
using StateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class BacklogQualityAnalyzerTests
{
    [TestMethod]
    public void Analyze_ReturnsCoherentCombinedResult()
    {
        var analyzer = new BacklogQualityAnalyzer();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Valid epic description", null, StateClassification.New),
            Snapshot(2, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(3, BacklogWorkItemTypes.ProductBacklogItem, 2, "Valid pbi description", null, StateClassification.New));

        var result = analyzer.Analyze(graph);

        Assert.AreEqual(result.Findings, result.Validation.Findings);
        Assert.HasCount(0, result.IntegrityFindings);
        Assert.AreEqual(
            "(1,75),(2,75),(3,75)",
            string.Join(",", result.ReadinessScores.Select(score => $"({score.WorkItemId},{score.Score.Value})")));
        Assert.AreEqual(
            "(1,False,75),(2,False,75),(3,False,75)",
            string.Join(",", result.ImplementationStates.Select(state => $"({state.WorkItemId},{state.IsReady},{state.Score.Value})")));
    }

    [TestMethod]
    public void Analyze_ReadinessAndValidationOutputsRemainAligned()
    {
        var analyzer = new BacklogQualityAnalyzer();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Valid epic description", null, StateClassification.New),
            Snapshot(2, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(3, BacklogWorkItemTypes.ProductBacklogItem, 2, null, null, StateClassification.New));

        var result = analyzer.Analyze(graph);

        var pbiReadiness = result.ReadinessScores.Single(score => score.WorkItemId == 3);
        var pbiImplementation = result.ImplementationStates.Single(state => state.WorkItemId == 3);

        Assert.AreEqual(0, pbiReadiness.Score.Value);
        Assert.AreEqual(pbiReadiness.Score, pbiImplementation.Score);
        CollectionAssert.AreEqual(
            new[] { "RC-1", "RC-2" },
            pbiImplementation.BlockingFindings.Select(finding => finding.Rule.RuleId).ToArray());
    }

    private static BacklogGraph CreateGraph(params WorkItemSnapshot[] items)
    {
        return new BacklogGraph(items);
    }

    private static WorkItemSnapshot Snapshot(
        int workItemId,
        string workItemType,
        int? parentWorkItemId,
        string? description,
        decimal? effort,
        StateClassification stateClassification)
    {
        return new WorkItemSnapshot(workItemId, workItemType, parentWorkItemId, description, effort, stateClassification);
    }
}
