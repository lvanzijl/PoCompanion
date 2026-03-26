using PoTool.Api.Adapters;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Adapters;

[TestClass]
public sealed class DeliveryTrendAnalyticsExposureMapperTests
{
    [TestMethod]
    public void ToProductDeliveryAnalyticsDto_PreservesNullAndNegativeCanonicalValues()
    {
        var product = new ProductAggregationResult(
            ProductProgress: null,
            ProductForecastConsumed: 12d,
            ProductForecastRemaining: null,
            ExcludedEpicsCount: 2,
            IncludedEpicsCount: 0,
            TotalWeight: 0d);
        var comparison = new SnapshotComparisonResult(
            ProgressDelta: null,
            ForecastConsumedDelta: -3d,
            ForecastRemainingDelta: null);
        var planningQuality = new PlanningQualityResult(
            55,
            [
                new PlanningQualitySignal(
                    PlanningQualitySignalCodes.ProductContainsExcludedEpics,
                    PlanningQualitySeverity.Warning,
                    PlanningQualityScope.Product,
                    "Excluded epics remain.",
                    7)
            ]);
        var insights = new InsightResult(
            [
                new Insight(
                    InsightCodes.ProgressUnknown,
                    InsightSeverity.Warning,
                    "Progress is unknown.",
                    new InsightContext(null, null, 55))
            ]);

        var dto = DeliveryTrendAnalyticsExposureMapper.ToProductDeliveryAnalyticsDto(
            7,
            "Product A",
            product,
            comparison,
            planningQuality,
            insights);

        Assert.AreEqual(7, dto.ProductId);
        Assert.AreEqual("Product A", dto.ProductName);
        Assert.IsNull(dto.Progress.ProductProgress);
        Assert.AreEqual(12d, dto.Progress.ProductForecastConsumed!.Value, 0.001d);
        Assert.IsNull(dto.Progress.ProductForecastRemaining);
        Assert.AreEqual(2, dto.Progress.ExcludedEpicsCount);
        Assert.IsNull(dto.Comparison.ProgressDelta);
        Assert.AreEqual(-3d, dto.Comparison.ForecastConsumedDelta!.Value, 0.001d);
        Assert.IsNull(dto.Comparison.ForecastRemainingDelta);
        Assert.AreEqual(55, dto.PlanningQuality.PlanningQualityScore);
        Assert.AreEqual("Warning", dto.PlanningQuality.PlanningQualitySignals[0].Severity);
        Assert.AreEqual("Product", dto.PlanningQuality.PlanningQualitySignals[0].Scope);
        Assert.AreEqual("IN-8", dto.Insights[0].Code);
        Assert.AreEqual("Warning", dto.Insights[0].Severity);
        Assert.IsNull(dto.Insights[0].Context.ProgressDelta);
        Assert.AreEqual(55, dto.Insights[0].Context.PlanningQualityScore);
    }
}
