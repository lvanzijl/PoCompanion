using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class SnapshotComparisonServiceTests
{
    private static readonly ISnapshotComparisonService Service = new SnapshotComparisonService();

    [TestMethod]
    public void Compare_ReturnsStandardProgressDelta_WhenBothProgressValuesExist()
    {
        var result = Service.Compare(new SnapshotComparisonRequest(
            new ProductSnapshot(ProductProgress: 50, ProductForecastConsumed: null, ProductForecastRemaining: null),
            new ProductSnapshot(ProductProgress: 70, ProductForecastConsumed: null, ProductForecastRemaining: null)));

        Assert.AreEqual(20d, result.ProgressDelta!.Value, 0.001d);
    }

    [TestMethod]
    public void Compare_PreservesNegativeProgressDelta_WhenCurrentDecreases()
    {
        var result = Service.Compare(new SnapshotComparisonRequest(
            new ProductSnapshot(ProductProgress: 70, ProductForecastConsumed: null, ProductForecastRemaining: null),
            new ProductSnapshot(ProductProgress: 50, ProductForecastConsumed: null, ProductForecastRemaining: null)));

        Assert.AreEqual(-20d, result.ProgressDelta!.Value, 0.001d);
    }

    [TestMethod]
    public void Compare_ReturnsNullDeltas_WhenPreviousSnapshotIsMissing()
    {
        var result = Service.Compare(new SnapshotComparisonRequest(
            Previous: null,
            Current: new ProductSnapshot(ProductProgress: 50, ProductForecastConsumed: 60, ProductForecastRemaining: 40)));

        Assert.IsNull(result.ProgressDelta);
        Assert.IsNull(result.ForecastConsumedDelta);
        Assert.IsNull(result.ForecastRemainingDelta);
    }

    [TestMethod]
    public void Compare_ReturnsNullProgressDelta_WhenEitherProgressValueIsNull()
    {
        var previousProgressNull = Service.Compare(new SnapshotComparisonRequest(
            new ProductSnapshot(ProductProgress: null, ProductForecastConsumed: null, ProductForecastRemaining: null),
            new ProductSnapshot(ProductProgress: 50, ProductForecastConsumed: null, ProductForecastRemaining: null)));

        var currentProgressNull = Service.Compare(new SnapshotComparisonRequest(
            new ProductSnapshot(ProductProgress: 50, ProductForecastConsumed: null, ProductForecastRemaining: null),
            new ProductSnapshot(ProductProgress: null, ProductForecastConsumed: null, ProductForecastRemaining: null)));

        Assert.IsNull(previousProgressNull.ProgressDelta);
        Assert.IsNull(currentProgressNull.ProgressDelta);
    }

    [TestMethod]
    public void Compare_ReturnsForecastConsumedDelta_WhenBothConsumedValuesExist()
    {
        var result = Service.Compare(new SnapshotComparisonRequest(
            new ProductSnapshot(ProductProgress: null, ProductForecastConsumed: 40, ProductForecastRemaining: null),
            new ProductSnapshot(ProductProgress: null, ProductForecastConsumed: 60, ProductForecastRemaining: null)));

        Assert.AreEqual(20d, result.ForecastConsumedDelta!.Value, 0.001d);
    }

    [TestMethod]
    public void Compare_ReturnsNegativeForecastRemainingDelta_WhenRemainingDrops()
    {
        var result = Service.Compare(new SnapshotComparisonRequest(
            new ProductSnapshot(ProductProgress: null, ProductForecastConsumed: null, ProductForecastRemaining: 80),
            new ProductSnapshot(ProductProgress: null, ProductForecastConsumed: null, ProductForecastRemaining: 50)));

        Assert.AreEqual(-30d, result.ForecastRemainingDelta!.Value, 0.001d);
    }

    [TestMethod]
    public void Compare_DoesNotTreatNullForecastValuesAsZero()
    {
        var result = Service.Compare(new SnapshotComparisonRequest(
            new ProductSnapshot(ProductProgress: null, ProductForecastConsumed: null, ProductForecastRemaining: 80),
            new ProductSnapshot(ProductProgress: null, ProductForecastConsumed: 10, ProductForecastRemaining: null)));

        Assert.IsNull(result.ForecastConsumedDelta);
        Assert.IsNull(result.ForecastRemainingDelta);
    }
}
