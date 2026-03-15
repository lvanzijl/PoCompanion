using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Services;
using StateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class BacklogValidationServiceTests
{
    [TestMethod]
    public void Validate_ReportsFindingsInCanonicalExecutionOrder()
    {
        var service = new BacklogValidationService();
        var graph = CreateGraph(
            Snapshot(10, BacklogWorkItemTypes.Epic, null, "Valid epic description", null, StateClassification.Done),
            Snapshot(11, BacklogWorkItemTypes.Task, 10, "Valid task description", null, StateClassification.New),
            Snapshot(20, BacklogWorkItemTypes.Epic, null, "short", null, StateClassification.New),
            Snapshot(21, BacklogWorkItemTypes.Feature, 20, "Valid feature description", null, StateClassification.New),
            Snapshot(30, BacklogWorkItemTypes.Feature, null, "Valid feature description", null, StateClassification.New));

        var result = service.Validate(graph);

        CollectionAssert.AreEqual(
            new[] { "SI-1", "RR-1", "RC-3" },
            result.Findings.Select(finding => finding.Rule.RuleId).ToArray());
    }

    [TestMethod]
    public void Validate_SuppressesImplementationReportingBeneathRefinementBlockers()
    {
        var service = new BacklogValidationService();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, null, null, StateClassification.New),
            Snapshot(2, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(3, BacklogWorkItemTypes.ProductBacklogItem, 2, null, null, StateClassification.New));

        var result = service.Validate(graph);

        CollectionAssert.AreEqual(
            new[] { "RR-1" },
            result.Findings.Select(finding => finding.Rule.RuleId).ToArray());

        Assert.HasCount(3, result.ImplementationStates);
        var epicImplementationState = result.ImplementationStates.Single(state => state.WorkItemId == 1);
        Assert.IsFalse(epicImplementationState.IsReady);
        CollectionAssert.AreEqual(
            new[] { "RR-1" },
            epicImplementationState.BlockingFindings.Select(finding => finding.Rule.RuleId).ToArray());

        var featureState = result.ImplementationStates.Single(state => state.WorkItemId == 2);
        Assert.IsFalse(featureState.IsReady);
        Assert.AreEqual(0, featureState.Score.Value);
        Assert.HasCount(0, featureState.BlockingFindings);

        var pbiState = result.ImplementationStates.Single(state => state.WorkItemId == 3);
        Assert.IsFalse(pbiState.IsReady);
        Assert.IsTrue(pbiState.HasMissingEffort);
        CollectionAssert.AreEqual(
            new[] { "RC-1", "RC-2" },
            pbiState.BlockingFindings.Select(finding => finding.Rule.RuleId).ToArray());

        var epicState = result.RefinementStates.Single(state => state.WorkItemId == 1);
        Assert.IsTrue(epicState.SuppressesImplementationReadiness);
    }

    [TestMethod]
    public void Validate_StructuralFindingsStillReportWhenImplementationIsSuppressed()
    {
        var service = new BacklogValidationService();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Valid epic description", null, StateClassification.Done),
            Snapshot(2, BacklogWorkItemTypes.Task, 1, "Valid task description", null, StateClassification.New),
            Snapshot(10, BacklogWorkItemTypes.Epic, null, "short", null, StateClassification.New),
            Snapshot(11, BacklogWorkItemTypes.Feature, 10, "Valid feature description", null, StateClassification.New),
            Snapshot(12, BacklogWorkItemTypes.ProductBacklogItem, 11, null, null, StateClassification.New));

        var result = service.Validate(graph);

        CollectionAssert.AreEqual(
            new[] { "SI-1", "RR-1" },
            result.Findings.Select(finding => finding.Rule.RuleId).ToArray());
        Assert.HasCount(1, result.IntegrityFindings);
        Assert.AreEqual("SI-1", result.IntegrityFindings[0].Rule.RuleId);
    }

    [TestMethod]
    public void Validate_ReturnsAggregatedOutputShape()
    {
        var service = new BacklogValidationService();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Valid epic description", null, StateClassification.New),
            Snapshot(2, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(3, BacklogWorkItemTypes.ProductBacklogItem, 2, "Valid pbi description", 5, StateClassification.New));

        var result = service.Validate(graph);

        Assert.HasCount(0, result.IntegrityFindings);
        Assert.HasCount(0, result.Findings);
        Assert.AreEqual(result.Findings, result.RuleResults);
        Assert.AreEqual(
            "(1,True,False),(2,True,False)",
            string.Join(",", result.RefinementStates.Select(state => $"({state.WorkItemId},{state.IsReady},{state.SuppressesImplementationReadiness})")));
        Assert.AreEqual(
            "(1,True,100,False),(2,True,100,False),(3,True,100,False)",
            string.Join(",", result.ImplementationStates.Select(state => $"({state.WorkItemId},{state.IsReady},{state.Score.Value},{state.HasMissingEffort})")));
    }

    [TestMethod]
    public void Validate_IsDeterministicAcrossMixedTrees()
    {
        var service = new BacklogValidationService();
        var firstGraph = CreateGraph(
            Snapshot(8, BacklogWorkItemTypes.ProductBacklogItem, 7, null, null, StateClassification.New),
            Snapshot(3, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Valid epic description", null, StateClassification.New),
            Snapshot(7, BacklogWorkItemTypes.Feature, null, "short", null, StateClassification.New),
            Snapshot(2, BacklogWorkItemTypes.Epic, null, "short", null, StateClassification.Done),
            Snapshot(4, BacklogWorkItemTypes.ProductBacklogItem, 3, "Valid pbi description", 8, StateClassification.New),
            Snapshot(5, BacklogWorkItemTypes.Feature, 2, "Valid feature description", null, StateClassification.New),
            Snapshot(6, BacklogWorkItemTypes.ProductBacklogItem, 5, null, null, StateClassification.New));
        var secondGraph = CreateGraph(
            Snapshot(6, BacklogWorkItemTypes.ProductBacklogItem, 5, null, null, StateClassification.New),
            Snapshot(5, BacklogWorkItemTypes.Feature, 2, "Valid feature description", null, StateClassification.New),
            Snapshot(4, BacklogWorkItemTypes.ProductBacklogItem, 3, "Valid pbi description", 8, StateClassification.New),
            Snapshot(2, BacklogWorkItemTypes.Epic, null, "short", null, StateClassification.Done),
            Snapshot(7, BacklogWorkItemTypes.Feature, null, "short", null, StateClassification.New),
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Valid epic description", null, StateClassification.New),
            Snapshot(3, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(8, BacklogWorkItemTypes.ProductBacklogItem, 7, null, null, StateClassification.New));

        var firstResult = service.Validate(firstGraph);
        var secondResult = service.Validate(secondGraph);

        CollectionAssert.AreEqual(
            firstResult.Findings.Select(finding => $"{finding.Rule.RuleId}:{finding.WorkItemId}").ToArray(),
            secondResult.Findings.Select(finding => $"{finding.Rule.RuleId}:{finding.WorkItemId}").ToArray());
        CollectionAssert.AreEqual(
            firstResult.RefinementStates.Select(state => $"{state.WorkItemId}:{state.IsReady}:{state.SuppressesImplementationReadiness}").ToArray(),
            secondResult.RefinementStates.Select(state => $"{state.WorkItemId}:{state.IsReady}:{state.SuppressesImplementationReadiness}").ToArray());
        CollectionAssert.AreEqual(
            firstResult.ImplementationStates.Select(state => $"{state.WorkItemId}:{state.IsReady}:{state.Score.Value}:{state.HasMissingEffort}").ToArray(),
            secondResult.ImplementationStates.Select(state => $"{state.WorkItemId}:{state.IsReady}:{state.Score.Value}:{state.HasMissingEffort}").ToArray());
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
