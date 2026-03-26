using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class InsightServiceTests
{
    private static readonly IInsightService Service = new InsightService();

    [TestMethod]
    public void Analyze_EmitsProgressStalledInsight_WhenProgressDeltaIsZero()
    {
        var result = Service.Analyze(CreateRequest(progressDelta: 0d, forecastRemainingDelta: 0d, planningQualityScore: 100));

        AssertInsight(result, InsightCodes.ProgressStalled, InsightSeverity.Warning, 0d, 0d, 100);
        Assert.IsFalse(result.Insights.Any(insight => insight.Code == InsightCodes.ProgressUnknown));
        Assert.HasCount(1, result.Insights);
    }

    [TestMethod]
    public void Analyze_EmitsProgressUnknownInsight_WhenProgressDeltaIsNull()
    {
        var result = Service.Analyze(CreateRequest(progressDelta: null, forecastRemainingDelta: 5d, planningQualityScore: 100));

        AssertInsight(result, InsightCodes.ProgressUnknown, InsightSeverity.Warning, null, 5d, 100);
        Assert.IsFalse(result.Insights.Any(insight => insight.Code == InsightCodes.ProgressStalled));
        Assert.HasCount(1, result.Insights);
    }

    [TestMethod]
    public void Analyze_EmitsProgressReversedAndScopeIncreaseInsights_WhenDeliveryFallsBehind()
    {
        var result = Service.Analyze(CreateRequest(progressDelta: -10d, forecastRemainingDelta: 12d, planningQualityScore: 100));

        AssertInsight(result, InsightCodes.ProgressReversed, InsightSeverity.Critical, -10d, 12d, 100);
        AssertInsight(result, InsightCodes.ScopeIncreasedFasterThanDelivery, InsightSeverity.Critical, -10d, 12d, 100);
        Assert.IsFalse(result.Insights.Any(insight => insight.Code is InsightCodes.ProgressStalled or InsightCodes.ProgressUnknown));
        Assert.HasCount(2, result.Insights);
    }

    [TestMethod]
    public void Analyze_EmitsHealthyProgressInsight_WhenProgressImprovesAndRemainingDrops()
    {
        var result = Service.Analyze(CreateRequest(progressDelta: 8d, forecastRemainingDelta: -6d, planningQualityScore: 100));

        AssertInsight(result, InsightCodes.HealthyProgress, InsightSeverity.Info, 8d, -6d, 100);
        Assert.HasCount(1, result.Insights);
    }

    [TestMethod]
    public void Analyze_EmitsPlanningQualityInsights_AndForecastUnreliable_WhenThresholdsAndSignalsMatch()
    {
        var result = Service.Analyze(CreateRequest(
            progressDelta: 4d,
            forecastRemainingDelta: 2d,
            planningQualityScore: 40,
            planningQualityCodes:
            [
                PlanningQualitySignalCodes.FeatureMissingEffort,
                PlanningQualitySignalCodes.MissingForecastData
            ]));

        AssertInsight(result, InsightCodes.LowPlanningQuality, InsightSeverity.Warning, 4d, 2d, 40);
        AssertInsight(result, InsightCodes.VeryLowPlanningQuality, InsightSeverity.Critical, 4d, 2d, 40);
        AssertInsight(result, InsightCodes.ForecastUnreliable, InsightSeverity.Warning, 4d, 2d, 40);
        Assert.HasCount(3, result.Insights);
    }

    [TestMethod]
    public void Analyze_EmitsProgressUnknownAlongsidePlanningQualityInsights_WhenProgressCannotBeMeasured()
    {
        var result = Service.Analyze(CreateRequest(
            progressDelta: null,
            forecastRemainingDelta: 6d,
            planningQualityScore: 40));

        AssertInsight(result, InsightCodes.ProgressUnknown, InsightSeverity.Warning, null, 6d, 40);
        AssertInsight(result, InsightCodes.LowPlanningQuality, InsightSeverity.Warning, null, 6d, 40);
        AssertInsight(result, InsightCodes.VeryLowPlanningQuality, InsightSeverity.Critical, null, 6d, 40);
        Assert.IsFalse(result.Insights.Any(insight => insight.Code == InsightCodes.ProgressStalled));
        Assert.HasCount(3, result.Insights);
    }

    private static void AssertInsight(
        InsightResult result,
        string code,
        InsightSeverity severity,
        double? progressDelta,
        double? forecastRemainingDelta,
        int planningQualityScore)
    {
        var insight = result.Insights.Single(candidate => candidate.Code == code);

        Assert.AreEqual(severity, insight.Severity);
        Assert.IsFalse(string.IsNullOrWhiteSpace(insight.Message));
        Assert.AreEqual(progressDelta, insight.Context.ProgressDelta);
        Assert.AreEqual(forecastRemainingDelta, insight.Context.ForecastRemainingDelta);
        Assert.AreEqual(planningQualityScore, insight.Context.PlanningQualityScore);
    }

    private static InsightRequest CreateRequest(
        double? progressDelta,
        double? forecastRemainingDelta,
        int planningQualityScore,
        params string[] planningQualityCodes)
    {
        var signals = planningQualityCodes
            .Select(code => new PlanningQualitySignal(
                code,
                PlanningQualitySeverity.Warning,
                PlanningQualityScope.Product,
                $"Signal {code}",
                9001))
            .ToList();

        return new InsightRequest(
            new ProductAggregationResult(55d, 20d, 30d, 0, 1, 10d),
            new SnapshotComparisonResult(progressDelta, ForecastConsumedDelta: null, ForecastRemainingDelta: forecastRemainingDelta),
            new PlanningQualityResult(planningQualityScore, signals));
    }
}
