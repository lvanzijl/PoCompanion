using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class PipelineInsightsCalculatorTests
{
    private PipelineInsightsCalculator _calculator = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _calculator = new PipelineInsightsCalculator();
    }

    private PipelineRunDto CreateRun(
        int runId, 
        int pipelineId, 
        PipelineRunResult result, 
        DateTimeOffset? startTime = null,
        DateTimeOffset? finishTime = null,
        TimeSpan? duration = null)
    {
        var start = startTime ?? DateTimeOffset.UtcNow.AddHours(-1);
        var finish = finishTime ?? start.AddMinutes(30);
        var runDuration = duration ?? (finish - start);

        return new PipelineRunDto
        {
            RunId = runId,
            PipelineId = pipelineId,
            PipelineName = $"Pipeline {pipelineId}",
            StartTime = start,
            FinishTime = finish,
            Duration = runDuration,
            Result = result,
            Trigger = PipelineRunTrigger.ContinuousIntegration,
            TriggerInfo = null,
            Branch = "refs/heads/main",
            RequestedFor = "Test User",
            RetrievedAt = DateTimeOffset.UtcNow
        };
    }

    #region Build Success Rate Tests

    [TestMethod]
    public void CalculateBuildSuccessRate_EmptyRuns_ReturnsNull()
    {
        // Arrange
        var runs = new List<PipelineRunDto>();

        // Act
        var result = _calculator.CalculateBuildSuccessRate(runs);

        // Assert
        Assert.IsNull(result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateBuildSuccessRate_AllSuccessful_Returns100Percent()
    {
        // Arrange
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Succeeded),
            CreateRun(2, 1, PipelineRunResult.Succeeded),
            CreateRun(3, 1, PipelineRunResult.Succeeded)
        };

        // Act
        var result = _calculator.CalculateBuildSuccessRate(runs);

        // Assert
        Assert.AreEqual(100.0, result.Median);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void CalculateBuildSuccessRate_MixedResults_ReturnsCorrectPercentage()
    {
        // Arrange
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Succeeded),
            CreateRun(2, 1, PipelineRunResult.Failed),
            CreateRun(3, 1, PipelineRunResult.Succeeded),
            CreateRun(4, 1, PipelineRunResult.Succeeded)
        };

        // Act
        var result = _calculator.CalculateBuildSuccessRate(runs);

        // Assert
        Assert.AreEqual(75.0, result.Median); // 3 out of 4
        Assert.AreEqual(4, result.Count);
    }

    #endregion

    #region Build Failure Rate Tests

    [TestMethod]
    public void CalculateBuildFailureRate_EmptyRuns_ReturnsNull()
    {
        // Arrange
        var runs = new List<PipelineRunDto>();

        // Act
        var result = _calculator.CalculateBuildFailureRate(runs);

        // Assert
        Assert.IsNull(result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateBuildFailureRate_NoFailures_ReturnsZero()
    {
        // Arrange
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Succeeded),
            CreateRun(2, 1, PipelineRunResult.Succeeded)
        };

        // Act
        var result = _calculator.CalculateBuildFailureRate(runs);

        // Assert
        Assert.AreEqual(0.0, result.Median);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void CalculateBuildFailureRate_PartialSuccessCountsAsFailure()
    {
        // Arrange
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Succeeded),
            CreateRun(2, 1, PipelineRunResult.PartiallySucceeded),
            CreateRun(3, 1, PipelineRunResult.Failed)
        };

        // Act
        var result = _calculator.CalculateBuildFailureRate(runs);

        // Assert
        Assert.AreEqual(66.66666666666667, result.Median!.Value, 0.0001); // 2 out of 3
        Assert.AreEqual(3, result.Count);
    }

    #endregion

    #region MTTR Tests

    [TestMethod]
    public void CalculateMTTR_EmptyRuns_ReturnsNull()
    {
        // Arrange
        var runs = new List<PipelineRunDto>();

        // Act
        var result = _calculator.CalculateMTTR(runs);

        // Assert
        Assert.IsNull(result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateMTTR_NoRecoveries_ReturnsNull()
    {
        // Arrange
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Failed, DateTimeOffset.UtcNow.AddHours(-3)),
            CreateRun(2, 1, PipelineRunResult.Failed, DateTimeOffset.UtcNow.AddHours(-2))
        };

        // Act
        var result = _calculator.CalculateMTTR(runs);

        // Assert
        Assert.IsNull(result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateMTTR_SingleRecovery_ReturnsCorrectHours()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow.AddDays(-1);
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Failed, baseTime),
            CreateRun(2, 1, PipelineRunResult.Succeeded, baseTime.AddHours(2))
        };

        // Act
        var result = _calculator.CalculateMTTR(runs);

        // Assert
        Assert.AreEqual(2.0, result.Median);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void CalculateMTTR_MultipleRecoveries_ReturnsMedian()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow.AddDays(-1);
        var runs = new List<PipelineRunDto>
        {
            // First failure-recovery cycle: 1 hour
            CreateRun(1, 1, PipelineRunResult.Failed, baseTime),
            CreateRun(2, 1, PipelineRunResult.Succeeded, baseTime.AddHours(1)),
            // Second failure-recovery cycle: 3 hours
            CreateRun(3, 1, PipelineRunResult.Failed, baseTime.AddHours(2)),
            CreateRun(4, 1, PipelineRunResult.Succeeded, baseTime.AddHours(5))
        };

        // Act
        var result = _calculator.CalculateMTTR(runs);

        // Assert
        Assert.AreEqual(2.0, result.Median); // Median of [1, 3] = 2
        Assert.AreEqual(2, result.Count);
    }

    #endregion

    #region Pipeline Duration Tests

    [TestMethod]
    public void CalculatePipelineDuration_EmptyRuns_ReturnsNull()
    {
        // Arrange
        var runs = new List<PipelineRunDto>();

        // Act
        var result = _calculator.CalculatePipelineDuration(runs);

        // Assert
        Assert.IsNull(result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculatePipelineDuration_WithDurations_ReturnsMedian()
    {
        // Arrange
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Succeeded, duration: TimeSpan.FromMinutes(10)),
            CreateRun(2, 1, PipelineRunResult.Succeeded, duration: TimeSpan.FromMinutes(20)),
            CreateRun(3, 1, PipelineRunResult.Succeeded, duration: TimeSpan.FromMinutes(30))
        };

        // Act
        var result = _calculator.CalculatePipelineDuration(runs);

        // Assert
        Assert.AreEqual(20.0 / 60.0, result.Median); // 20 minutes in hours
        Assert.AreEqual(3, result.Count);
    }

    #endregion

    #region Time to First Failure Detection Tests

    [TestMethod]
    public void CalculateTimeToFirstFailureDetection_EmptyRuns_ReturnsNull()
    {
        // Arrange
        var runs = new List<PipelineRunDto>();

        // Act
        var result = _calculator.CalculateTimeToFirstFailureDetection(runs);

        // Assert
        Assert.IsNull(result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateTimeToFirstFailureDetection_OnlySuccesses_ReturnsNull()
    {
        // Arrange
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Succeeded),
            CreateRun(2, 1, PipelineRunResult.Succeeded)
        };

        // Act
        var result = _calculator.CalculateTimeToFirstFailureDetection(runs);

        // Assert
        Assert.IsNull(result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateTimeToFirstFailureDetection_WithFailures_ReturnsMedian()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Failed, baseTime, baseTime.AddMinutes(10)),
            CreateRun(2, 1, PipelineRunResult.Failed, baseTime, baseTime.AddMinutes(20)),
            CreateRun(3, 1, PipelineRunResult.Failed, baseTime, baseTime.AddMinutes(30))
        };

        // Act
        var result = _calculator.CalculateTimeToFirstFailureDetection(runs);

        // Assert
        Assert.AreEqual(20.0 / 60.0, result.Median); // 20 minutes in hours
        Assert.AreEqual(3, result.Count);
    }

    #endregion

    #region Flakiness Rate Tests

    [TestMethod]
    public void CalculateFlakinessRate_EmptyRuns_ReturnsNull()
    {
        // Arrange
        var runs = new List<PipelineRunDto>();

        // Act
        var result = _calculator.CalculateFlakinessRate(runs);

        // Assert
        Assert.IsNull(result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateFlakinessRate_OnlySuccesses_ReturnsZero()
    {
        // Arrange
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Succeeded),
            CreateRun(2, 1, PipelineRunResult.Succeeded)
        };

        // Act
        var result = _calculator.CalculateFlakinessRate(runs);

        // Assert
        Assert.AreEqual(0.0, result.Median);
        Assert.AreEqual(0, result.Count); // No flaky pipelines
    }

    [TestMethod]
    public void CalculateFlakinessRate_OnlyFailures_ReturnsZero()
    {
        // Arrange
        var runs = new List<PipelineRunDto>
        {
            CreateRun(1, 1, PipelineRunResult.Failed),
            CreateRun(2, 1, PipelineRunResult.Failed)
        };

        // Act
        var result = _calculator.CalculateFlakinessRate(runs);

        // Assert
        Assert.AreEqual(0.0, result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateFlakinessRate_MixedResults_ReturnsCorrectPercentage()
    {
        // Arrange
        var runs = new List<PipelineRunDto>
        {
            // Pipeline 1: Flaky (has both success and failure)
            CreateRun(1, 1, PipelineRunResult.Succeeded),
            CreateRun(2, 1, PipelineRunResult.Failed),
            // Pipeline 2: Not flaky (only successes)
            CreateRun(3, 2, PipelineRunResult.Succeeded),
            CreateRun(4, 2, PipelineRunResult.Succeeded),
            // Pipeline 3: Flaky (has both)
            CreateRun(5, 3, PipelineRunResult.Failed),
            CreateRun(6, 3, PipelineRunResult.Succeeded)
        };

        // Act
        var result = _calculator.CalculateFlakinessRate(runs);

        // Assert
        Assert.AreEqual(66.66666666666667, result.Median!.Value, 0.0001); // 2 out of 3 pipelines are flaky
        Assert.AreEqual(2, result.Count); // 2 flaky pipelines
    }

    #endregion
}
