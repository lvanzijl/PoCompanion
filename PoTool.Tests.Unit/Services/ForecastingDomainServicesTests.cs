using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Forecasting.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ForecastingDomainServicesTests
{
    [TestMethod]
    public void CompletionForecastService_Forecast_PreservesProjectionSemantics()
    {
        ICompletionForecastService service = new CompletionForecastService();

        var result = service.Forecast(
            totalScopeStoryPoints: 26,
            completedScopeStoryPoints: 5,
            historicalSprints:
            [
                new HistoricalVelocitySample("Sprint 1", new DateTimeOffset(2026, 1, 14, 0, 0, 0, TimeSpan.Zero), 10),
                new HistoricalVelocitySample("Sprint 2", new DateTimeOffset(2026, 1, 28, 0, 0, 0, TimeSpan.Zero), 10),
                new HistoricalVelocitySample("Sprint 3", new DateTimeOffset(2026, 2, 11, 0, 0, 0, TimeSpan.Zero), 10)
            ]);

        Assert.AreEqual(21d, result.RemainingScopeStoryPoints, 0.001);
        Assert.AreEqual(10d, result.EstimatedVelocity, 0.001);
        Assert.AreEqual(3, result.SprintsRemaining);
        Assert.AreEqual(ForecastConfidenceLevel.Medium, result.Confidence);
        Assert.AreEqual(new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero), result.EstimatedCompletionDate);
        Assert.HasCount(3, result.Projections);
        Assert.AreEqual(10d, result.Projections[0].ExpectedCompletedStoryPoints, 0.001);
        Assert.AreEqual(0d, result.Projections[2].RemainingStoryPointsAfterSprint, 0.001);
        Assert.AreEqual(100d, result.Projections[2].ProgressPercentage, 0.001);
    }

    [TestMethod]
    public void CompletionForecastService_Forecast_CapsForecastAtFinalCompletionState()
    {
        ICompletionForecastService service = new CompletionForecastService();

        var result = service.Forecast(
            totalScopeStoryPoints: 20,
            completedScopeStoryPoints: 0,
            historicalSprints:
            [
                new HistoricalVelocitySample("Sprint 1", new DateTimeOffset(2026, 1, 14, 0, 0, 0, TimeSpan.Zero), 8),
                new HistoricalVelocitySample("Sprint 2", new DateTimeOffset(2026, 1, 28, 0, 0, 0, TimeSpan.Zero), 8),
                new HistoricalVelocitySample("Sprint 3", new DateTimeOffset(2026, 2, 11, 0, 0, 0, TimeSpan.Zero), 8),
                new HistoricalVelocitySample("Sprint 4", new DateTimeOffset(2026, 2, 25, 0, 0, 0, TimeSpan.Zero), 8),
                new HistoricalVelocitySample("Sprint 5", new DateTimeOffset(2026, 3, 11, 0, 0, 0, TimeSpan.Zero), 8)
            ]);

        Assert.AreEqual(ForecastConfidenceLevel.High, result.Confidence);
        Assert.HasCount(3, result.Projections);
        Assert.AreEqual(100d, result.Projections[^1].ProgressPercentage, 0.001);
        Assert.AreEqual(0d, result.Projections[^1].RemainingStoryPointsAfterSprint, 0.001);
    }

    [TestMethod]
    public void VelocityCalibrationService_Calibrate_UsesDerivedExclusionAndPredictabilitySemantics()
    {
        IVelocityCalibrationService service = new VelocityCalibrationService();

        var result = service.Calibrate(
        [
            new VelocityCalibrationSample("Sprint 1", 10, 4, 6, 24),
            new VelocityCalibrationSample("Sprint 2", 8, 0, 4, 10),
            new VelocityCalibrationSample("Sprint 3", 0, 0, 5, 5)
        ]);

        Assert.HasCount(3, result.Entries);
        Assert.AreEqual(6d, result.Entries[0].CommittedStoryPoints, 0.001);
        Assert.AreEqual(4d, result.Entries[0].HoursPerStoryPoint, 0.001);
        Assert.AreEqual(0d, result.Entries[2].CommittedStoryPoints, 0.001);
        Assert.AreEqual(0.75d, result.MedianPredictability, 0.001);
        Assert.AreEqual(5d, result.MedianVelocity, 0.001);
    }

    [TestMethod]
    public void VelocityCalibrationService_Calibrate_FlagsVelocityOutliers()
    {
        IVelocityCalibrationService service = new VelocityCalibrationService();

        var samples = Enumerable.Range(1, 10)
            .Select(index => new VelocityCalibrationSample(
                $"Sprint {index}",
                10,
                0,
                index switch
                {
                    1 => 1,
                    10 => 100,
                    _ => 10
                },
                10))
            .ToList();

        var result = service.Calibrate(samples);

        CollectionAssert.Contains(result.OutlierSprintNames.ToList(), "Sprint 1");
        CollectionAssert.Contains(result.OutlierSprintNames.ToList(), "Sprint 10");
    }

    [TestMethod]
    public void EffortTrendForecastService_Analyze_PreservesForecastConsistency()
    {
        IEffortTrendForecastService service = new EffortTrendForecastService();

        var result = service.Analyze(
        [
            new EffortDistributionWorkItem("TeamA", "Sprint 1", 10),
            new EffortDistributionWorkItem("TeamA", "Sprint 2", 20),
            new EffortDistributionWorkItem("TeamA", "Sprint 3", 30),
            new EffortDistributionWorkItem("TeamB", "Sprint 1", 5),
            new EffortDistributionWorkItem("TeamB", "Sprint 2", 5),
            new EffortDistributionWorkItem("TeamB", "Sprint 3", 5)
        ],
        maxIterations: 10,
        defaultCapacityPerIteration: 50);

        Assert.AreEqual(EffortForecastDirection.Increasing, result.OverallTrend);
        Assert.IsGreaterThan(0d, result.TrendSlope);
        Assert.HasCount(3, result.TrendBySprint);
        Assert.HasCount(3, result.Forecasts);
        Assert.IsTrue(result.Forecasts.All(forecast => forecast.HighEstimate >= forecast.LowEstimate));
        Assert.IsTrue(result.Forecasts.All(forecast => forecast.ConfidenceLevel is >= 0 and <= 1));
    }

    [TestMethod]
    public void ForecastingDomainModels_RejectInvalidCanonicalValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CompletionProjection("Sprint +1", "Forecast/1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, 0, 101));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new VelocityCalibration(Array.Empty<VelocityCalibrationEntry>(), 5, 6, 4, 0.5, Array.Empty<string>()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new EffortDistributionForecast("Sprint +1", 1, 2, 1, 1.1));
    }
}
