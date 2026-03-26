using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class EpicProgressServiceTests
{
    private static readonly IEpicProgressService Service = new EpicProgressService();

    [TestMethod]
    public void Compute_ReturnsFullProgress_WhenAllFeaturesAreComplete()
    {
        var result = Service.Compute(CreateRequest(
        [
            CreateFeature(200, effectiveProgress: 1d, totalEffort: 3d),
            CreateFeature(201, effectiveProgress: 1d, totalEffort: 5d)
        ]));

        Assert.AreEqual(1d, result.EpicProgress, 0.001d);
        Assert.AreEqual(2, result.IncludedFeaturesCount);
        Assert.AreEqual(0, result.ExcludedFeaturesCount);
        Assert.AreEqual(8d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsZero_WhenAllFeaturesHaveNoProgress()
    {
        var result = Service.Compute(CreateRequest(
        [
            CreateFeature(200, effectiveProgress: 0d, totalEffort: 3d),
            CreateFeature(201, effectiveProgress: 0d, totalEffort: 5d)
        ]));

        Assert.AreEqual(0d, result.EpicProgress, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsWeightedAverage_ForMixedFeatureProgress()
    {
        var result = Service.Compute(CreateRequest(
        [
            CreateFeature(200, effectiveProgress: 0.5d, totalEffort: 2d),
            CreateFeature(201, effectiveProgress: 1d, totalEffort: 1d)
        ]));

        Assert.AreEqual(2d / 3d, result.EpicProgress, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsZero_WhenEpicHasNoFeatures()
    {
        var result = Service.Compute(CreateRequest([]));

        Assert.AreEqual(0d, result.EpicProgress, 0.001d);
        Assert.AreEqual(0, result.IncludedFeaturesCount);
        Assert.AreEqual(0, result.ExcludedFeaturesCount);
        Assert.AreEqual(0d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsZero_WhenAllFeatureWeightsAreZero()
    {
        var result = Service.Compute(CreateRequest(
        [
            CreateFeature(200, effectiveProgress: 0.5d, totalEffort: 0d),
            CreateFeature(201, effectiveProgress: 1d, totalEffort: 0d)
        ]));

        Assert.AreEqual(0d, result.EpicProgress, 0.001d);
        Assert.AreEqual(0, result.IncludedFeaturesCount);
        Assert.AreEqual(2, result.ExcludedFeaturesCount);
        Assert.AreEqual(0d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsSingleFeatureProgress_WhenOnlyOneFeatureExists()
    {
        var result = Service.Compute(CreateRequest(
        [
            CreateFeature(200, effectiveProgress: 0.25d, totalEffort: 8d)
        ]));

        Assert.AreEqual(0.25d, result.EpicProgress, 0.001d);
    }

    [TestMethod]
    public void Compute_IsDeterministicRegardlessOfFeatureOrdering()
    {
        var original = CreateRequest(
        [
            CreateFeature(200, effectiveProgress: 0.25d, totalEffort: 8d),
            CreateFeature(201, effectiveProgress: 0.75d, totalEffort: 4d)
        ]);
        var reversed = CreateRequest(
        [
            CreateFeature(201, effectiveProgress: 0.75d, totalEffort: 4d),
            CreateFeature(200, effectiveProgress: 0.25d, totalEffort: 8d)
        ]);

        var originalResult = Service.Compute(original);
        var reversedResult = Service.Compute(reversed);

        Assert.AreEqual(originalResult.EpicProgress, reversedResult.EpicProgress, 0.001d);
        Assert.AreEqual(originalResult.TotalWeight, reversedResult.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_RejectsNonEpicWorkItem()
    {
        var request = new EpicProgressCalculationRequest(
            new CanonicalWorkItem(
                100,
                CanonicalWorkItemTypes.Feature,
                parentWorkItemId: null,
                businessValue: null,
                storyPoints: null),
            []);

        Assert.ThrowsExactly<ArgumentException>(() => Service.Compute(request));
    }

    [TestMethod]
    public void Compute_IgnoresNonFeatureChildren()
    {
        var result = Service.Compute(CreateRequest(
        [
            CreateFeature(200, effectiveProgress: 0.5d, totalEffort: 2d),
            CreateFeature(201, CanonicalWorkItemTypes.Task, effectiveProgress: 1d, totalEffort: 100d)
        ]));

        Assert.AreEqual(0.5d, result.EpicProgress, 0.001d);
        Assert.AreEqual(1, result.IncludedFeaturesCount);
        Assert.AreEqual(0, result.ExcludedFeaturesCount);
        Assert.AreEqual(2d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_IgnoresEpicLevelTimeCriticality()
    {
        var result = Service.Compute(CreateRequest(
            [CreateFeature(200, effectiveProgress: 0.25d, totalEffort: 8d)],
            epicTimeCriticality: 100d));

        Assert.AreEqual(0.25d, result.EpicProgress, 0.001d);
    }

    private static EpicProgressCalculationRequest CreateRequest(
        IReadOnlyList<EpicFeatureProgress> features,
        double? epicTimeCriticality = null)
    {
        return new EpicProgressCalculationRequest(
            new CanonicalWorkItem(
                100,
                CanonicalWorkItemTypes.Epic,
                parentWorkItemId: null,
                businessValue: null,
                storyPoints: null,
                timeCriticality: epicTimeCriticality),
            features);
    }

    private static EpicFeatureProgress CreateFeature(
        int workItemId,
        double effectiveProgress,
        double totalEffort)
    {
        return CreateFeature(workItemId, CanonicalWorkItemTypes.Feature, effectiveProgress, totalEffort);
    }

    private static EpicFeatureProgress CreateFeature(
        int workItemId,
        string workItemType,
        double effectiveProgress,
        double totalEffort)
    {
        return new EpicFeatureProgress(
            new CanonicalWorkItem(
                workItemId,
                workItemType,
                parentWorkItemId: 100,
                businessValue: null,
                storyPoints: null,
                effort: totalEffort),
            effectiveProgress,
            totalEffort);
    }
}
