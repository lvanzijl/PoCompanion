using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Forecasting.Components.DeliveryForecast;
using PoTool.Core.Domain.Forecasting.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ForecastingDomainServicesTests
{
    [TestMethod]
    public void DeliveryForecastProjector_Project_PreservesProjectionSemantics()
    {
        IDeliveryForecastProjector projector = new DeliveryForecastProjector();

        var result = projector.Project(
            workItemId: 42,
            workItemType: "Epic",
            totalScopeStoryPoints: 26,
            completedScopeStoryPoints: 5,
            historicalSprints:
            [
                new HistoricalVelocitySample("Sprint 1", new DateTimeOffset(2026, 1, 14, 0, 0, 0, TimeSpan.Zero), 10),
                new HistoricalVelocitySample("Sprint 2", new DateTimeOffset(2026, 1, 28, 0, 0, 0, TimeSpan.Zero), 10),
                new HistoricalVelocitySample("Sprint 3", new DateTimeOffset(2026, 2, 11, 0, 0, 0, TimeSpan.Zero), 10)
            ],
            lastUpdated: new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(42, result.WorkItemId);
        Assert.AreEqual("Epic", result.WorkItemType);
        Assert.AreEqual(21d, result.RemainingScopeStoryPoints, 0.001);
        Assert.AreEqual(10d, result.EstimatedVelocity, 0.001);
        Assert.AreEqual(3, result.SprintsRemaining);
        Assert.AreEqual(ForecastConfidenceLevel.Medium, result.Confidence);
        Assert.AreEqual(new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero), result.EstimatedCompletionDate);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), result.LastUpdated);
        Assert.HasCount(3, result.ForecastByDate);
    }

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
    public void ForecastingServices_RepeatedWithSameInputs_ProduceIdenticalOutputs()
    {
        ICompletionForecastService completionForecastService = new CompletionForecastService();
        IVelocityCalibrationService velocityCalibrationService = new VelocityCalibrationService();
        IEffortTrendForecastService effortTrendForecastService = new EffortTrendForecastService();

        HistoricalVelocitySample[] historicalSprints =
        [
            new HistoricalVelocitySample("Sprint 1", new DateTimeOffset(2026, 1, 14, 0, 0, 0, TimeSpan.Zero), 10),
            new HistoricalVelocitySample("Sprint 2", new DateTimeOffset(2026, 1, 28, 0, 0, 0, TimeSpan.Zero), 8),
            new HistoricalVelocitySample("Sprint 3", new DateTimeOffset(2026, 2, 11, 0, 0, 0, TimeSpan.Zero), 12)
        ];

        VelocityCalibrationSample[] velocitySamples =
        [
            new VelocityCalibrationSample("Sprint 1", 12, 2, 10, 24),
            new VelocityCalibrationSample("Sprint 2", 8, 0, 6, 16),
            new VelocityCalibrationSample("Sprint 3", 13, 1, 9, 21)
        ];

        EffortDistributionWorkItem[] effortWorkItems =
        [
            new EffortDistributionWorkItem("TeamA", "Sprint 1", 10),
            new EffortDistributionWorkItem("TeamA", "Sprint 2", 20),
            new EffortDistributionWorkItem("TeamA", "Sprint 3", 30),
            new EffortDistributionWorkItem("TeamB", "Sprint 1", 5),
            new EffortDistributionWorkItem("TeamB", "Sprint 2", 15),
            new EffortDistributionWorkItem("TeamB", "Sprint 3", 10)
        ];

        var firstForecast = completionForecastService.Forecast(34, 8, historicalSprints);
        var secondForecast = completionForecastService.Forecast(34, 8, historicalSprints);
        var firstCalibration = velocityCalibrationService.Calibrate(velocitySamples);
        var secondCalibration = velocityCalibrationService.Calibrate(velocitySamples);
        var firstEffortAnalysis = effortTrendForecastService.Analyze(effortWorkItems, maxIterations: 10, defaultCapacityPerIteration: 50);
        var secondEffortAnalysis = effortTrendForecastService.Analyze(effortWorkItems, maxIterations: 10, defaultCapacityPerIteration: 50);

        Assert.AreEqual(firstForecast.TotalScopeStoryPoints, secondForecast.TotalScopeStoryPoints, 0.001d);
        Assert.AreEqual(firstForecast.CompletedScopeStoryPoints, secondForecast.CompletedScopeStoryPoints, 0.001d);
        Assert.AreEqual(firstForecast.RemainingScopeStoryPoints, secondForecast.RemainingScopeStoryPoints, 0.001d);
        Assert.AreEqual(firstForecast.EstimatedVelocity, secondForecast.EstimatedVelocity, 0.001d);
        Assert.AreEqual(firstForecast.SprintsRemaining, secondForecast.SprintsRemaining);
        Assert.AreEqual(firstForecast.EstimatedCompletionDate, secondForecast.EstimatedCompletionDate);
        Assert.AreEqual(firstForecast.Confidence, secondForecast.Confidence);
        CollectionAssert.AreEqual(firstForecast.Projections.ToList(), secondForecast.Projections.ToList());

        Assert.AreEqual(firstCalibration.MedianVelocity, secondCalibration.MedianVelocity, 0.001d);
        Assert.AreEqual(firstCalibration.P25Velocity, secondCalibration.P25Velocity, 0.001d);
        Assert.AreEqual(firstCalibration.P75Velocity, secondCalibration.P75Velocity, 0.001d);
        Assert.AreEqual(firstCalibration.MedianPredictability, secondCalibration.MedianPredictability, 0.001d);
        CollectionAssert.AreEqual(firstCalibration.Entries.ToList(), secondCalibration.Entries.ToList());
        CollectionAssert.AreEqual(firstCalibration.OutlierSprintNames.ToList(), secondCalibration.OutlierSprintNames.ToList());

        Assert.AreEqual(firstEffortAnalysis.OverallTrend, secondEffortAnalysis.OverallTrend);
        Assert.AreEqual(firstEffortAnalysis.TrendSlope, secondEffortAnalysis.TrendSlope, 0.001d);
        CollectionAssert.AreEqual(firstEffortAnalysis.TrendBySprint.ToList(), secondEffortAnalysis.TrendBySprint.ToList());
        CollectionAssert.AreEqual(firstEffortAnalysis.Forecasts.ToList(), secondEffortAnalysis.Forecasts.ToList());

        Assert.HasCount(firstEffortAnalysis.TrendByAreaPath.Count, secondEffortAnalysis.TrendByAreaPath);

        for (var index = 0; index < firstEffortAnalysis.TrendByAreaPath.Count; index++)
        {
            var firstAreaTrend = firstEffortAnalysis.TrendByAreaPath[index];
            var secondAreaTrend = secondEffortAnalysis.TrendByAreaPath[index];

            Assert.AreEqual(firstAreaTrend.AreaPath, secondAreaTrend.AreaPath);
            CollectionAssert.AreEqual(firstAreaTrend.EffortBySprint.ToList(), secondAreaTrend.EffortBySprint.ToList());
            Assert.AreEqual(firstAreaTrend.AverageEffort, secondAreaTrend.AverageEffort, 0.001d);
            Assert.AreEqual(firstAreaTrend.StandardDeviation, secondAreaTrend.StandardDeviation, 0.001d);
            Assert.AreEqual(firstAreaTrend.Direction, secondAreaTrend.Direction);
            Assert.AreEqual(firstAreaTrend.TrendSlope, secondAreaTrend.TrendSlope, 0.001d);
        }
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
