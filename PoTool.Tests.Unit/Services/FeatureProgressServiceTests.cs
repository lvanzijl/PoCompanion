using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class FeatureProgressServiceTests
{
    private static readonly IFeatureProgressService Service = new FeatureProgressService();

    [TestMethod]
    public void Compute_ReturnsFullProgress_WhenAllContributorsAreDone()
    {
        var result = Service.Compute(CreateRequest(
            [
                CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.Done, effort: 3),
                CreateChild(202, CanonicalWorkItemTypes.Bug, StateClassification.Done, effort: 5)
            ]));

        Assert.AreEqual(1d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(1d, result.EffectiveProgress, 0.001d);
        Assert.AreEqual(8d, result.CompletedEffort, 0.001d);
        Assert.AreEqual(8d, result.TotalEffort, 0.001d);
        Assert.IsNull(result.Override);
        Assert.IsEmpty(result.ValidationSignals);
    }

    [TestMethod]
    public void Compute_ReturnsZeroProgress_WhenNoContributorIsDone()
    {
        var result = Service.Compute(CreateRequest(
            [
                CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.New, effort: 3),
                CreateChild(202, CanonicalWorkItemTypes.Bug, StateClassification.InProgress, effort: 5)
            ]));

        Assert.AreEqual(0d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(0d, result.EffectiveProgress, 0.001d);
        Assert.AreEqual(0d, result.CompletedEffort, 0.001d);
        Assert.AreEqual(8d, result.TotalEffort, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsExpectedRatio_ForMixedContributorStates()
    {
        var result = Service.Compute(CreateRequest(
            [
                CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.Done, effort: 3),
                CreateChild(202, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.InProgress, effort: 5),
                CreateChild(203, CanonicalWorkItemTypes.Bug, StateClassification.New, effort: 2)
            ]));

        Assert.AreEqual(0.3d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(0.3d, result.EffectiveProgress, 0.001d);
        Assert.AreEqual(3d, result.CompletedEffort, 0.001d);
        Assert.AreEqual(10d, result.TotalEffort, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsZero_WhenFeatureHasNoChildren()
    {
        var result = Service.Compute(CreateRequest([]));

        Assert.AreEqual(0d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(0d, result.EffectiveProgress, 0.001d);
        Assert.AreEqual(0d, result.CompletedEffort, 0.001d);
        Assert.AreEqual(0d, result.TotalEffort, 0.001d);
    }

    [TestMethod]
    public void Compute_TreatsNullEffortAsZeroWithoutExcludingItems()
    {
        var result = Service.Compute(CreateRequest(
            [
                CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.Done, effort: null),
                CreateChild(202, CanonicalWorkItemTypes.Bug, StateClassification.InProgress, effort: null)
            ]));

        Assert.AreEqual(0d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(0d, result.EffectiveProgress, 0.001d);
        Assert.AreEqual(0d, result.CompletedEffort, 0.001d);
        Assert.AreEqual(0d, result.TotalEffort, 0.001d);
    }

    [TestMethod]
    public void Compute_ExcludesRemovedItemsFromTotalEffort()
    {
        var result = Service.Compute(CreateRequest(
            [
                CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.Done, effort: 3),
                CreateChild(202, CanonicalWorkItemTypes.Bug, StateClassification.Removed, effort: 7)
            ]));

        Assert.AreEqual(1d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(3d, result.CompletedEffort, 0.001d);
        Assert.AreEqual(3d, result.TotalEffort, 0.001d);
    }

    [TestMethod]
    public void Compute_IgnoresTasks()
    {
        var result = Service.Compute(CreateRequest(
            [
                CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.Done, effort: 3),
                CreateChild(202, CanonicalWorkItemTypes.Task, StateClassification.Done, effort: 7)
            ]));

        Assert.AreEqual(1d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(3d, result.CompletedEffort, 0.001d);
        Assert.AreEqual(3d, result.TotalEffort, 0.001d);
    }

    [TestMethod]
    public void Compute_AppliesZeroOverrideStrictly()
    {
        var result = Service.Compute(CreateRequest(
            [CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.Done, effort: 5)],
            timeCriticality: 0));

        Assert.AreEqual(1d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(0d, result.EffectiveProgress, 0.001d);
        Assert.AreEqual(0d, result.Override!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_AppliesMidpointOverrideStrictly()
    {
        var result = Service.Compute(CreateRequest(
            [CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.Done, effort: 5)],
            timeCriticality: 50));

        Assert.AreEqual(1d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(0.5d, result.EffectiveProgress, 0.001d);
        Assert.AreEqual(50d, result.Override!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_AppliesFullOverrideStrictly()
    {
        var result = Service.Compute(CreateRequest(
            [CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.New, effort: 5)],
            timeCriticality: 100));

        Assert.AreEqual(0d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(1d, result.EffectiveProgress, 0.001d);
        Assert.AreEqual(100d, result.Override!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_OverrideIgnoresBaseCalculation()
    {
        var result = Service.Compute(CreateRequest(
            [
                CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.Done, effort: 2),
                CreateChild(202, CanonicalWorkItemTypes.Bug, StateClassification.InProgress, effort: 8)
            ],
            timeCriticality: 50));

        Assert.AreEqual(0.2d, result.CalculatedProgress, 0.001d);
        Assert.AreEqual(0.5d, result.EffectiveProgress, 0.001d);
    }

    [TestMethod]
    public void Compute_RejectsNonFeatureWorkItem()
    {
        var request = new FeatureProgressCalculationRequest(
            new CanonicalWorkItem(
                100,
                CanonicalWorkItemTypes.Epic,
                parentWorkItemId: null,
                businessValue: null,
                storyPoints: null,
                effort: null),
            []);

        Assert.ThrowsExactly<ArgumentException>(() => Service.Compute(request));
    }

    [TestMethod]
    public void Compute_IsDeterministicRegardlessOfChildOrdering()
    {
        var original = CreateRequest(
            [
                CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.Done, effort: 2),
                CreateChild(202, CanonicalWorkItemTypes.Bug, StateClassification.InProgress, effort: 8)
            ],
            timeCriticality: 50);
        var reversed = CreateRequest(
            [
                CreateChild(202, CanonicalWorkItemTypes.Bug, StateClassification.InProgress, effort: 8),
                CreateChild(201, CanonicalWorkItemTypes.ProductBacklogItem, StateClassification.Done, effort: 2)
            ],
            timeCriticality: 50);

        var originalResult = Service.Compute(original);
        var reversedResult = Service.Compute(reversed);

        Assert.AreEqual(originalResult.CalculatedProgress, reversedResult.CalculatedProgress, 0.001d);
        Assert.AreEqual(originalResult.EffectiveProgress, reversedResult.EffectiveProgress, 0.001d);
        Assert.AreEqual(originalResult.CompletedEffort, reversedResult.CompletedEffort, 0.001d);
        Assert.AreEqual(originalResult.TotalEffort, reversedResult.TotalEffort, 0.001d);
    }

    private static FeatureProgressCalculationRequest CreateRequest(
        IReadOnlyList<FeatureProgressChild> children,
        double? timeCriticality = null)
    {
        return new FeatureProgressCalculationRequest(
            new CanonicalWorkItem(
                100,
                CanonicalWorkItemTypes.Feature,
                parentWorkItemId: null,
                businessValue: null,
                storyPoints: null,
                timeCriticality: timeCriticality,
                effort: null),
            children);
    }

    private static FeatureProgressChild CreateChild(
        int workItemId,
        string workItemType,
        StateClassification stateClassification,
        double? effort)
    {
        return new FeatureProgressChild(
            new CanonicalWorkItem(
                workItemId,
                workItemType,
                parentWorkItemId: 100,
                businessValue: null,
                storyPoints: null,
                effort: effort),
            stateClassification);
    }
}
