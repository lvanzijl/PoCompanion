using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Services;
using StateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ImplementationReadinessServiceTests
{
    private readonly ImplementationReadinessService _service = new();

    [TestMethod]
    public void Compute_PbiReadyStateIsDerivedFromScore()
    {
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.ProductBacklogItem, 10, "Valid PBI description", 3, StateClassification.New));

        var state = _service.Compute(graph).Single();

        Assert.IsTrue(state.IsReady);
        Assert.AreEqual(100, state.Score.Value);
        Assert.IsFalse(state.HasMissingEffort);
        Assert.HasCount(0, state.BlockingFindings);
    }

    [TestMethod]
    public void Compute_PbiMissingEffort_RemainsNotReadyAndFlagsMissingEffort()
    {
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.ProductBacklogItem, 10, "Valid PBI description", null, StateClassification.New));

        var state = _service.Compute(graph).Single();

        Assert.IsFalse(state.IsReady);
        Assert.AreEqual(75, state.Score.Value);
        Assert.IsTrue(state.HasMissingEffort);
        CollectionAssert.AreEqual(new[] { "RC-2" }, state.BlockingFindings.Select(finding => finding.Rule.RuleId).ToArray());
    }

    [TestMethod]
    public void Compute_FeatureWithPartialChildMaturity_IsNotReadyEvenWithoutDirectFeatureBlockers()
    {
        var graph = CreateGraph(
            Snapshot(10, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(11, BacklogWorkItemTypes.ProductBacklogItem, 10, "Valid PBI description", null, StateClassification.New));

        var state = _service.Compute(graph).Single(item => item.WorkItemId == 10);

        Assert.IsFalse(state.IsReady);
        Assert.AreEqual(75, state.Score.Value);
        Assert.IsFalse(state.HasMissingEffort);
        Assert.HasCount(0, state.BlockingFindings);
    }

    [TestMethod]
    public void Compute_FeatureWithOnlyDoneChild_IsReadyFromCanonicalScoring()
    {
        var graph = CreateGraph(
            Snapshot(10, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(11, BacklogWorkItemTypes.ProductBacklogItem, 10, "Done PBI", null, StateClassification.Done));

        var state = _service.Compute(graph).Single(item => item.WorkItemId == 10);

        Assert.IsTrue(state.IsReady);
        Assert.AreEqual(100, state.Score.Value);
        Assert.HasCount(0, state.BlockingFindings);
    }

    [TestMethod]
    public void Compute_EpicMissingDescription_IsNotReadyWhileChildScopeCanStillBeReady()
    {
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, null, null, StateClassification.New),
            Snapshot(10, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(11, BacklogWorkItemTypes.ProductBacklogItem, 10, "Valid PBI description", 5, StateClassification.New));

        var states = _service.Compute(graph);

        var epicState = states.Single(item => item.WorkItemId == 1);
        var featureState = states.Single(item => item.WorkItemId == 10);

        Assert.IsFalse(epicState.IsReady);
        Assert.AreEqual(0, epicState.Score.Value);
        CollectionAssert.AreEqual(new[] { "RR-1" }, epicState.BlockingFindings.Select(finding => finding.Rule.RuleId).ToArray());
        Assert.IsTrue(featureState.IsReady);
        Assert.AreEqual(100, featureState.Score.Value);
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
