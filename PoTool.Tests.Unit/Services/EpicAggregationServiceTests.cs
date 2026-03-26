using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class EpicAggregationServiceTests
{
    private static readonly IEpicAggregationService Service = new EpicAggregationService();

    [TestMethod]
    public void Compute_UsesWeightedAverageAcrossIncludedFeatures()
    {
        var result = Service.Compute(new EpicAggregationRequest(
            CreateEpic(),
        [
            CreateFeatureProgress(100, effectiveProgress: 0.5d, totalEffort: 2d),
            CreateFeatureProgress(101, effectiveProgress: 1d, totalEffort: 1d)
        ]));

        Assert.AreEqual(66.67d, result.EpicProgress, 0.01d);
        Assert.AreEqual(0, result.ExcludedFeaturesCount);
        Assert.AreEqual(2, result.IncludedFeaturesCount);
        Assert.AreEqual(3d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsZeroProgress_WhenAllFeaturesAreExcluded()
    {
        var result = Service.Compute(new EpicAggregationRequest(
            CreateEpic(),
        [
            CreateFeatureProgress(100, effectiveProgress: 0.5d, totalEffort: 0d),
            CreateFeatureProgress(101, effectiveProgress: 1d, totalEffort: 0d)
        ]));

        Assert.AreEqual(0d, result.EpicProgress, 0.001d);
        Assert.AreEqual(2, result.ExcludedFeaturesCount);
        Assert.AreEqual(0, result.IncludedFeaturesCount);
        Assert.AreEqual(0d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_IgnoresNonFeatureChildrenForProgressAndForecast()
    {
        var result = Service.Compute(new EpicAggregationRequest(
            CreateEpic(),
        [
            CreateFeatureProgress(100, effectiveProgress: 0.5d, totalEffort: 2d, forecastConsumedEffort: 20d, forecastRemainingEffort: 80d),
            CreateFeatureProgress(101, CanonicalWorkItemTypes.Task, effectiveProgress: 1d, totalEffort: 100d, forecastConsumedEffort: 30d, forecastRemainingEffort: 70d)
        ]));

        Assert.AreEqual(50d, result.EpicProgress, 0.001d);
        Assert.AreEqual(20d, result.EpicForecastConsumed!.Value, 0.001d);
        Assert.AreEqual(80d, result.EpicForecastRemaining!.Value, 0.001d);
        Assert.AreEqual(1, result.IncludedFeaturesCount);
        Assert.AreEqual(0, result.ExcludedFeaturesCount);
        Assert.AreEqual(2d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_SumsForecastsAcrossFeatures()
    {
        var result = Service.Compute(new EpicAggregationRequest(
            CreateEpic(),
        [
            CreateFeatureProgress(100, effectiveProgress: 0.5d, totalEffort: 2d, forecastConsumedEffort: 20d, forecastRemainingEffort: 80d),
            CreateFeatureProgress(101, effectiveProgress: 1d, totalEffort: 1d, forecastConsumedEffort: 30d, forecastRemainingEffort: 70d)
        ]));

        Assert.AreEqual(50d, result.EpicForecastConsumed!.Value, 0.001d);
        Assert.AreEqual(150d, result.EpicForecastRemaining!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_SkipsNullForecastsInSums()
    {
        var result = Service.Compute(new EpicAggregationRequest(
            CreateEpic(),
        [
            CreateFeatureProgress(100, effectiveProgress: 0.5d, totalEffort: 2d),
            CreateFeatureProgress(101, effectiveProgress: 1d, totalEffort: 1d, forecastConsumedEffort: 30d, forecastRemainingEffort: 70d)
        ]));

        Assert.AreEqual(30d, result.EpicForecastConsumed!.Value, 0.001d);
        Assert.AreEqual(70d, result.EpicForecastRemaining!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsNullForecastTotals_WhenNoFeatureForecastExists()
    {
        var result = Service.Compute(new EpicAggregationRequest(
            CreateEpic(),
        [
            CreateFeatureProgress(100, effectiveProgress: 0.5d, totalEffort: 2d),
            CreateFeatureProgress(101, effectiveProgress: 1d, totalEffort: 1d)
        ]));

        Assert.IsNull(result.EpicForecastConsumed);
        Assert.IsNull(result.EpicForecastRemaining);
    }

    private static CanonicalWorkItem CreateEpic()
    {
        return new CanonicalWorkItem(
            100,
            CanonicalWorkItemTypes.Epic,
            parentWorkItemId: null,
            businessValue: null,
            storyPoints: null);
    }

    private static EpicFeatureProgress CreateFeatureProgress(
        int featureId,
        double effectiveProgress,
        double totalEffort,
        double? forecastConsumedEffort = null,
        double? forecastRemainingEffort = null)
    {
        return CreateFeatureProgress(
            featureId,
            CanonicalWorkItemTypes.Feature,
            effectiveProgress,
            totalEffort,
            forecastConsumedEffort,
            forecastRemainingEffort);
    }

    private static EpicFeatureProgress CreateFeatureProgress(
        int featureId,
        string workItemType,
        double effectiveProgress,
        double totalEffort,
        double? forecastConsumedEffort = null,
        double? forecastRemainingEffort = null)
    {
        return new EpicFeatureProgress(
            new CanonicalWorkItem(
                featureId,
                workItemType,
                parentWorkItemId: 100,
                businessValue: null,
                storyPoints: null,
                effort: totalEffort),
            effectiveProgress,
            totalEffort,
            forecastConsumedEffort,
            forecastRemainingEffort);
    }
}
