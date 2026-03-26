using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class FeatureForecastServiceTests
{
    private static readonly IFeatureForecastService Service = new FeatureForecastService();

    [TestMethod]
    public void Compute_ReturnsExpectedForecast_ForStandardCase()
    {
        var result = Service.Compute(new FeatureForecastCalculationRequest(
            EffectiveProgress: 50d,
            Effort: 100d));

        Assert.AreEqual(50d, result.ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(50d, result.ForecastRemainingEffort!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsZeroRemaining_ForFullCompletion()
    {
        var result = Service.Compute(new FeatureForecastCalculationRequest(
            EffectiveProgress: 100d,
            Effort: 100d));

        Assert.AreEqual(100d, result.ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(0d, result.ForecastRemainingEffort!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsFullRemaining_ForZeroProgress()
    {
        var result = Service.Compute(new FeatureForecastCalculationRequest(
            EffectiveProgress: 0d,
            Effort: 100d));

        Assert.AreEqual(0d, result.ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(100d, result.ForecastRemainingEffort!.Value, 0.001d);
    }

    [TestMethod]
    public void Compute_ReturnsNullForecast_WhenEffortIsMissing()
    {
        var result = Service.Compute(new FeatureForecastCalculationRequest(
            EffectiveProgress: 70d,
            Effort: null));

        Assert.IsNull(result.ForecastConsumedEffort);
        Assert.IsNull(result.ForecastRemainingEffort);
    }

    [TestMethod]
    public void Compute_ReturnsNullForecast_WhenEffectiveProgressIsMissing()
    {
        var result = Service.Compute(new FeatureForecastCalculationRequest(
            EffectiveProgress: null,
            Effort: 80d));

        Assert.IsNull(result.ForecastConsumedEffort);
        Assert.IsNull(result.ForecastRemainingEffort);
    }

    [TestMethod]
    public void Compute_HandlesDecimalProgressDeterministically()
    {
        var result = Service.Compute(new FeatureForecastCalculationRequest(
            EffectiveProgress: 37.5d,
            Effort: 80d));

        Assert.AreEqual(30d, result.ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(50d, result.ForecastRemainingEffort!.Value, 0.001d);
    }
}
