using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Services;
using StateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class BacklogReadinessServiceTests
{
    private readonly BacklogReadinessService _service = new();

    [TestMethod]
    public void Compute_PbiMissingDescription_ReturnsZero()
    {
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.ProductBacklogItem, 10, null, null, StateClassification.New));

        var score = _service.Compute(graph).Single();

        Assert.AreEqual(1, score.WorkItemId);
        Assert.AreEqual(0, score.Score.Value);
        Assert.AreEqual("MissingDescription", score.ScoreReason);
        Assert.IsNull(score.OwnerState);
    }

    [TestMethod]
    public void Compute_PbiMissingEffort_ReturnsSeventyFive()
    {
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.ProductBacklogItem, 10, "Valid PBI description", null, StateClassification.New));

        var score = _service.Compute(graph).Single();

        Assert.AreEqual(75, score.Score.Value);
        Assert.AreEqual("MissingEffort", score.ScoreReason);
    }

    [TestMethod]
    public void Compute_PbiWithDescriptionAndEffort_ReturnsHundred()
    {
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.ProductBacklogItem, 10, "Valid PBI description", 3, StateClassification.New));

        var score = _service.Compute(graph).Single();

        Assert.AreEqual(100, score.Score.Value);
        Assert.AreEqual("Ready", score.ScoreReason);
    }

    [TestMethod]
    public void Compute_FeatureMissingDescription_ReturnsZeroOwnedByPo()
    {
        var graph = CreateGraph(
            Snapshot(10, BacklogWorkItemTypes.Feature, 1, null, null, StateClassification.New));

        var score = _service.Compute(graph).Single();

        Assert.AreEqual(0, score.Score.Value);
        Assert.AreEqual(ReadinessOwnerState.PO, score.OwnerState);
    }

    [TestMethod]
    public void Compute_FeatureWithoutActiveOrDonePbis_ReturnsTwentyFiveOwnedByTeam()
    {
        var graph = CreateGraph(
            Snapshot(10, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(11, BacklogWorkItemTypes.ProductBacklogItem, 10, "Removed PBI", 3, StateClassification.Removed));

        var score = _service.Compute(graph).Single();

        Assert.AreEqual(25, score.Score.Value);
        Assert.AreEqual("MissingChildren", score.ScoreReason);
        Assert.AreEqual(ReadinessOwnerState.Team, score.OwnerState);
    }

    [TestMethod]
    public void Compute_FeatureUsesDoneChildrenAndExcludesRemovedChildren()
    {
        var graph = CreateGraph(
            Snapshot(10, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(11, BacklogWorkItemTypes.ProductBacklogItem, 10, "Done PBI", null, StateClassification.Done),
            Snapshot(12, BacklogWorkItemTypes.ProductBacklogItem, 10, "Ready PBI", 5, StateClassification.New),
            Snapshot(13, BacklogWorkItemTypes.ProductBacklogItem, 10, "Removed PBI", null, StateClassification.Removed));

        var score = _service.Compute(graph).Single(item => item.WorkItemId == 10);

        Assert.AreEqual(100, score.Score.Value);
        Assert.AreEqual(ReadinessOwnerState.Ready, score.OwnerState);
    }

    [TestMethod]
    public void Compute_FeatureAverageUsesMidpointToEvenRounding()
    {
        var graph = CreateGraph(
            Snapshot(10, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(11, BacklogWorkItemTypes.ProductBacklogItem, 10, "Ready PBI", 5, StateClassification.New),
            Snapshot(12, BacklogWorkItemTypes.ProductBacklogItem, 10, "Estimated later", null, StateClassification.New),
            Snapshot(13, BacklogWorkItemTypes.ProductBacklogItem, 10, "Estimated later", null, StateClassification.New),
            Snapshot(14, BacklogWorkItemTypes.ProductBacklogItem, 10, null, null, StateClassification.New));

        var score = _service.Compute(graph).Single(item => item.WorkItemId == 10);

        Assert.AreEqual(62, score.Score.Value);
        Assert.AreEqual(ReadinessOwnerState.Team, score.OwnerState);
    }

    [TestMethod]
    public void Compute_EpicMissingDescription_DoesNotEraseMatureFeatureScores()
    {
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, null, null, StateClassification.New),
            Snapshot(10, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(11, BacklogWorkItemTypes.ProductBacklogItem, 10, "Valid PBI description", 5, StateClassification.New));

        var scores = _service.Compute(graph);

        Assert.AreEqual(0, scores.Single(score => score.WorkItemId == 1).Score.Value);
        Assert.AreEqual(100, scores.Single(score => score.WorkItemId == 10).Score.Value);
        Assert.AreEqual(100, scores.Single(score => score.WorkItemId == 11).Score.Value);
    }

    [TestMethod]
    public void Compute_EpicUsesDirectFeatureScoresAndDoneContribution()
    {
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Valid epic description", 50, StateClassification.New),
            Snapshot(10, BacklogWorkItemTypes.Feature, 1, "Valid feature description", null, StateClassification.New),
            Snapshot(11, BacklogWorkItemTypes.ProductBacklogItem, 10, "Valid PBI description", null, StateClassification.New),
            Snapshot(20, BacklogWorkItemTypes.Feature, 1, "Completed feature", null, StateClassification.Done));

        var score = _service.Compute(graph).Single(item => item.WorkItemId == 1);

        Assert.AreEqual(88, score.Score.Value);
        Assert.AreEqual("ChildAverage", score.ScoreReason);
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
