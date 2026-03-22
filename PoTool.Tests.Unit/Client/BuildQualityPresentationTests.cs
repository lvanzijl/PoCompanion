using PoTool.Client.ApiClient;
using PoTool.Client.Components.Common;

namespace PoTool.Tests.Unit.Client;

[TestClass]
public sealed class BuildQualityPresentationTests
{
    [TestMethod]
    public void GetDimensionState_ReturnsUnknown_WhenMetricIsUnknown()
    {
        var result = CreateResult();
        result.Metrics.SuccessRate = null;
        result.Evidence.SuccessRateUnknown = true;
        result.Evidence.SuccessRateUnknownReason = "NoEligibleBuilds";

        var state = BuildQualityPresentation.GetDimensionState(result, BuildQualityDimension.Builds);

        Assert.AreEqual(BuildQualityVisualState.Unknown, state.State);
        Assert.AreEqual("Unknown", BuildQualityPresentation.FormatPercent(result.Metrics.SuccessRate, result.Evidence.SuccessRateUnknown));
    }

    [TestMethod]
    public void GetDimensionState_ReturnsWarning_WhenTestThresholdIsNotMet()
    {
        var result = CreateResult();
        result.Metrics.TestPassRate = 0.98d;
        result.Metrics.TestVolume = 12;
        result.Evidence.TestThresholdMet = false;

        var state = BuildQualityPresentation.GetDimensionState(result, BuildQualityDimension.Tests);

        Assert.AreEqual(BuildQualityVisualState.Warning, state.State);
    }

    [TestMethod]
    public void GetOverallState_ReturnsBad_WhenAnyDimensionIsBad()
    {
        var result = CreateResult();
        result.Metrics.Coverage = 0.55d;

        var state = BuildQualityPresentation.GetOverallState(result);

        Assert.AreEqual(BuildQualityVisualState.Bad, state.State);
    }

    [TestMethod]
    public void GetConfidenceSummary_UsesLockedThresholdText()
    {
        var result = CreateResult();
        result.Evidence.BuildThresholdMet = true;
        result.Evidence.TestThresholdMet = false;

        var summary = BuildQualityPresentation.GetConfidenceSummary(result);

        StringAssert.Contains(summary, "Minimum builds (3): met.");
        StringAssert.Contains(summary, "Minimum tests (20): not met.");
    }

    private static BuildQualityResultDto CreateResult()
    {
        return new BuildQualityResultDto
        {
            Metrics = new BuildQualityMetricsDto
            {
                SuccessRate = 0.96d,
                TestPassRate = 0.92d,
                TestVolume = 40,
                Coverage = 0.84d,
                Confidence = 2
            },
            Evidence = new BuildQualityEvidenceDto
            {
                EligibleBuilds = 10,
                SucceededBuilds = 9,
                FailedBuilds = 1,
                TotalTests = 44,
                PassedTests = 41,
                NotApplicableTests = 4,
                CoveredLines = 840,
                TotalLines = 1000,
                BuildThresholdMet = true,
                TestThresholdMet = true
            }
        };
    }
}
