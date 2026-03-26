using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class EpicAggregationServiceTests
{
    private static readonly IEpicAggregationService Service = new EpicAggregationService();

    [TestMethod]
    public void Compute_UsesWeightedAverageAcrossIncludedFeatures()
    {
        var result = Service.Compute(new EpicAggregationRequest(
        [
            CreateFeatureProgress(100, effectiveProgress: 50, weight: 2),
            CreateFeatureProgress(101, effectiveProgress: 100, weight: 1)
        ]));

        Assert.AreEqual(66.67d, result.EpicProgress!.Value, 0.01d);
        Assert.AreEqual(0, result.ExcludedFeaturesCount);
        Assert.AreEqual(2, result.IncludedFeaturesCount);
        Assert.AreEqual(3d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_IgnoresExcludedFeaturesForWeightedProgress()
    {
        var result = Service.Compute(new EpicAggregationRequest(
        [
            CreateFeatureProgress(100, effectiveProgress: 50, weight: 2),
            CreateFeatureProgress(101, effectiveProgress: 100, weight: 0, isExcluded: true)
        ]));

        Assert.AreEqual(50d, result.EpicProgress!.Value, 0.001d);
        Assert.AreEqual(1, result.ExcludedFeaturesCount);
        Assert.AreEqual(1, result.IncludedFeaturesCount);
        Assert.AreEqual(2d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsNullProgress_WhenAllFeaturesAreExcluded()
    {
        var result = Service.Compute(new EpicAggregationRequest(
        [
            CreateFeatureProgress(100, effectiveProgress: 50, weight: 0, isExcluded: true),
            CreateFeatureProgress(101, effectiveProgress: 100, weight: 0, isExcluded: true)
        ]));

        Assert.IsNull(result.EpicProgress);
        Assert.AreEqual(2, result.ExcludedFeaturesCount);
        Assert.AreEqual(0, result.IncludedFeaturesCount);
        Assert.AreEqual(0d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_SumsForecastsAcrossFeatures()
    {
        var result = Service.Compute(new EpicAggregationRequest(
        [
            CreateFeatureProgress(100, effectiveProgress: 50, weight: 2, forecastConsumedEffort: 20, forecastRemainingEffort: 80),
            CreateFeatureProgress(101, effectiveProgress: 100, weight: 1, forecastConsumedEffort: 30, forecastRemainingEffort: 70)
        ]));

        Assert.AreEqual(50d, result.EpicForecastConsumed!.Value, 0.001d);
        Assert.AreEqual(150d, result.EpicForecastRemaining!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_SkipsNullForecastsInSums()
    {
        var result = Service.Compute(new EpicAggregationRequest(
        [
            CreateFeatureProgress(100, effectiveProgress: 50, weight: 2, forecastConsumedEffort: null, forecastRemainingEffort: null),
            CreateFeatureProgress(101, effectiveProgress: 100, weight: 1, forecastConsumedEffort: 30, forecastRemainingEffort: 70)
        ]));

        Assert.AreEqual(30d, result.EpicForecastConsumed!.Value, 0.001d);
        Assert.AreEqual(70d, result.EpicForecastRemaining!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsNullForecastTotals_WhenNoFeatureForecastExists()
    {
        var result = Service.Compute(new EpicAggregationRequest(
        [
            CreateFeatureProgress(100, effectiveProgress: 50, weight: 2, forecastConsumedEffort: null, forecastRemainingEffort: null),
            CreateFeatureProgress(101, effectiveProgress: 100, weight: 1, forecastConsumedEffort: null, forecastRemainingEffort: null)
        ]));

        Assert.IsNull(result.EpicForecastConsumed);
        Assert.IsNull(result.EpicForecastRemaining);
    }

    private static FeatureProgress CreateFeatureProgress(
        int featureId,
        double? effectiveProgress,
        double weight,
        bool isExcluded = false,
        double? forecastConsumedEffort = null,
        double? forecastRemainingEffort = null)
    {
        var progressPercent = effectiveProgress.HasValue
            ? (int)Math.Round(effectiveProgress.Value, MidpointRounding.AwayFromZero)
            : 0;

        return new FeatureProgress(
            featureId,
            $"Feature {featureId}",
            1,
            100,
            "Epic X",
            progressPercent,
            totalScopeStoryPoints: weight,
            deliveredStoryPoints: 0,
            donePbiCount: 0,
            isDone: false,
            sprintDeliveredStoryPoints: 0,
            sprintProgressionDelta: new ProgressionDelta(0),
            sprintEffortDelta: 0,
            sprintCompletedPbiCount: 0,
            sprintCompletedInSprint: false,
            calculatedProgress: effectiveProgress,
            overrideProgress: null,
            effectiveProgress: effectiveProgress,
            validationSignals: null,
            forecastConsumedEffort: forecastConsumedEffort,
            forecastRemainingEffort: forecastRemainingEffort,
            weight: weight,
            isExcluded: isExcluded);
    }
}
