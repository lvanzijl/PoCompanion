using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ProductAggregationServiceTests
{
    private static readonly IProductAggregationService Service = new ProductAggregationService();

    [TestMethod]
    public void Compute_UsesWeightedAverageAcrossIncludedEpics()
    {
        var result = Service.Compute(new ProductAggregationRequest(
        [
            new ProductAggregationEpicInput(EpicProgress: 50, EpicForecastConsumed: null, EpicForecastRemaining: null, Weight: 2, IsExcluded: false),
            new ProductAggregationEpicInput(EpicProgress: 100, EpicForecastConsumed: null, EpicForecastRemaining: null, Weight: 1, IsExcluded: false)
        ]));

        Assert.AreEqual(66.67d, result.ProductProgress!.Value, 0.01d);
        Assert.AreEqual(0, result.ExcludedEpicsCount);
        Assert.AreEqual(2, result.IncludedEpicsCount);
        Assert.AreEqual(3d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_ExcludesNullProgressEpicsFromWeightedProgressAndCountsThem()
    {
        var result = Service.Compute(new ProductAggregationRequest(
        [
            new ProductAggregationEpicInput(EpicProgress: null, EpicForecastConsumed: null, EpicForecastRemaining: null, Weight: 2, IsExcluded: false),
            new ProductAggregationEpicInput(EpicProgress: 100, EpicForecastConsumed: null, EpicForecastRemaining: null, Weight: 1, IsExcluded: false)
        ]));

        Assert.AreEqual(100d, result.ProductProgress!.Value, 0.001d);
        Assert.AreEqual(1, result.ExcludedEpicsCount);
        Assert.AreEqual(1, result.IncludedEpicsCount);
        Assert.AreEqual(1d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsNullProgress_WhenAllEpicsAreInvalid()
    {
        var result = Service.Compute(new ProductAggregationRequest(
        [
            new ProductAggregationEpicInput(EpicProgress: null, EpicForecastConsumed: null, EpicForecastRemaining: null, Weight: 0, IsExcluded: true),
            new ProductAggregationEpicInput(EpicProgress: 25, EpicForecastConsumed: null, EpicForecastRemaining: null, Weight: 0, IsExcluded: false)
        ]));

        Assert.IsNull(result.ProductProgress);
        Assert.AreEqual(2, result.ExcludedEpicsCount);
        Assert.AreEqual(0, result.IncludedEpicsCount);
        Assert.AreEqual(0d, result.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Compute_SumsForecastsAcrossEpics()
    {
        var result = Service.Compute(new ProductAggregationRequest(
        [
            new ProductAggregationEpicInput(EpicProgress: 50, EpicForecastConsumed: 40, EpicForecastRemaining: 60, Weight: 2, IsExcluded: false),
            new ProductAggregationEpicInput(EpicProgress: 100, EpicForecastConsumed: 10, EpicForecastRemaining: 90, Weight: 1, IsExcluded: false)
        ]));

        Assert.AreEqual(50d, result.ProductForecastConsumed!.Value, 0.001d);
        Assert.AreEqual(150d, result.ProductForecastRemaining!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_SkipsNullForecastsInSums()
    {
        var result = Service.Compute(new ProductAggregationRequest(
        [
            new ProductAggregationEpicInput(EpicProgress: 50, EpicForecastConsumed: null, EpicForecastRemaining: null, Weight: 2, IsExcluded: false),
            new ProductAggregationEpicInput(EpicProgress: 100, EpicForecastConsumed: 10, EpicForecastRemaining: 90, Weight: 1, IsExcluded: true)
        ]));

        Assert.AreEqual(10d, result.ProductForecastConsumed!.Value, 0.001d);
        Assert.AreEqual(90d, result.ProductForecastRemaining!.Value, 0.001d);
        Assert.AreEqual(1, result.ExcludedEpicsCount, "Explicitly excluded epics should be counted even when their forecast values remain summable.");
    }

    [TestMethod]
    public void Compute_ReturnsNullForecastTotals_WhenNoEpicForecastExists()
    {
        var result = Service.Compute(new ProductAggregationRequest(
        [
            new ProductAggregationEpicInput(EpicProgress: 50, EpicForecastConsumed: null, EpicForecastRemaining: null, Weight: 2, IsExcluded: false),
            new ProductAggregationEpicInput(EpicProgress: 100, EpicForecastConsumed: null, EpicForecastRemaining: null, Weight: 1, IsExcluded: false)
        ]));

        Assert.IsNull(result.ProductForecastConsumed);
        Assert.IsNull(result.ProductForecastRemaining);
    }
}
