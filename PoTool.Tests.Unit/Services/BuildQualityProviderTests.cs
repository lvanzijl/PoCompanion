using PoTool.Api.Services.BuildQuality;
using PoTool.Shared.BuildQuality;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class BuildQualityProviderTests
{
    private readonly BuildQualityProvider _provider = new();

    [TestMethod]
    public void Compute_CalculatesSuccessRateFromAggregatedEligibleBuilds()
    {
        var result = _provider.Compute(
            [
                new BuildQualityBuildFact(1, "succeeded"),
                new BuildQualityBuildFact(2, "failed"),
                new BuildQualityBuildFact(3, "partiallysucceeded"),
                new BuildQualityBuildFact(4, "canceled")
            ],
            Array.Empty<BuildQualityTestRunFact>(),
            Array.Empty<BuildQualityCoverageFact>());

        Assert.AreEqual(1d / 3d, result.Metrics.SuccessRate!.Value, 0.000001d);
        Assert.AreEqual(3, result.Evidence.EligibleBuilds);
        Assert.AreEqual(1, result.Evidence.CanceledBuilds);
    }

    [TestMethod]
    public void Compute_CalculatesTestPassRateAndVolumeFromSummedTotals()
    {
        var result = _provider.Compute(
            Array.Empty<BuildQualityBuildFact>(),
            [
                new BuildQualityTestRunFact(1, 10, 8, 2),
                new BuildQualityTestRunFact(1, 8, 7, 1)
            ],
            Array.Empty<BuildQualityCoverageFact>());

        Assert.AreEqual(15, result.Metrics.TestVolume);
        Assert.AreEqual(1.0, result.Metrics.TestPassRate!.Value, 0.000001d);
        Assert.AreEqual(18, result.Evidence.TotalTests);
        Assert.AreEqual(15, result.Evidence.PassedTests);
        Assert.AreEqual(3, result.Evidence.NotApplicableTests);
    }

    [TestMethod]
    public void Compute_CalculatesCoverageFromSummedTotals()
    {
        var result = _provider.Compute(
            Array.Empty<BuildQualityBuildFact>(),
            Array.Empty<BuildQualityTestRunFact>(),
            [
                new BuildQualityCoverageFact(1, 50, 100),
                new BuildQualityCoverageFact(1, 30, 50)
            ]);

        Assert.AreEqual(80d / 150d, result.Metrics.Coverage!.Value, 0.000001d);
        Assert.AreEqual(80, result.Evidence.CoveredLines);
        Assert.AreEqual(150, result.Evidence.TotalLines);
    }

    [TestMethod]
    public void Compute_CalculatesConfidenceFromThresholdFlags()
    {
        var result = _provider.Compute(
            [
                new BuildQualityBuildFact(1, "succeeded"),
                new BuildQualityBuildFact(2, "succeeded"),
                new BuildQualityBuildFact(3, "failed")
            ],
            [
                new BuildQualityTestRunFact(1, 12, 10, 0),
                new BuildQualityTestRunFact(2, 10, 9, 0)
            ],
            Array.Empty<BuildQualityCoverageFact>());

        Assert.AreEqual(2, result.Metrics.Confidence);
        Assert.IsTrue(result.Evidence.BuildThresholdMet);
        Assert.IsTrue(result.Evidence.TestThresholdMet);
    }

    [TestMethod]
    public void Compute_NoBuilds_MarksSuccessRateUnknown()
    {
        var result = _provider.Compute(
            Array.Empty<BuildQualityBuildFact>(),
            Array.Empty<BuildQualityTestRunFact>(),
            Array.Empty<BuildQualityCoverageFact>());

        Assert.IsNull(result.Metrics.SuccessRate);
        Assert.IsTrue(result.Evidence.SuccessRateUnknown);
        Assert.AreEqual(BuildQualityUnknownReasons.NoEligibleBuilds, result.Evidence.SuccessRateUnknownReason);
    }

    [TestMethod]
    public void Compute_NoTests_MarksTestPassRateUnknown()
    {
        var result = _provider.Compute(
            [new BuildQualityBuildFact(1, "succeeded")],
            Array.Empty<BuildQualityTestRunFact>(),
            Array.Empty<BuildQualityCoverageFact>());

        Assert.IsNull(result.Metrics.TestPassRate);
        Assert.IsTrue(result.Evidence.TestPassRateUnknown);
        Assert.AreEqual(BuildQualityUnknownReasons.NoTestRuns, result.Evidence.TestPassRateUnknownReason);
    }

    [TestMethod]
    public void Compute_NoCoverage_MarksCoverageUnknown()
    {
        var result = _provider.Compute(
            [new BuildQualityBuildFact(1, "succeeded")],
            Array.Empty<BuildQualityTestRunFact>(),
            Array.Empty<BuildQualityCoverageFact>());

        Assert.IsNull(result.Metrics.Coverage);
        Assert.IsTrue(result.Evidence.CoverageUnknown);
        Assert.AreEqual(BuildQualityUnknownReasons.NoCoverage, result.Evidence.CoverageUnknownReason);
    }

    [TestMethod]
    public void Compute_ZeroTotalLines_MarksCoverageUnknown()
    {
        var result = _provider.Compute(
            [new BuildQualityBuildFact(1, "succeeded")],
            Array.Empty<BuildQualityTestRunFact>(),
            [new BuildQualityCoverageFact(1, 0, 0)]);

        Assert.IsNull(result.Metrics.Coverage);
        Assert.IsTrue(result.Evidence.CoverageUnknown);
        Assert.AreEqual(BuildQualityUnknownReasons.ZeroTotalLines, result.Evidence.CoverageUnknownReason);
    }

    [TestMethod]
    public void Compute_PartiallySucceededCountsAsFailure_AndCanceledIsExcluded()
    {
        var result = _provider.Compute(
            [
                new BuildQualityBuildFact(1, "succeeded"),
                new BuildQualityBuildFact(2, "partiallysucceeded"),
                new BuildQualityBuildFact(3, "canceled")
            ],
            Array.Empty<BuildQualityTestRunFact>(),
            Array.Empty<BuildQualityCoverageFact>());

        Assert.AreEqual(2, result.Evidence.EligibleBuilds);
        Assert.AreEqual(1, result.Evidence.PartiallySucceededBuilds);
        Assert.AreEqual(1d / 2d, result.Metrics.SuccessRate!.Value, 0.000001d);
    }

    [TestMethod]
    public void Compute_MultipleRowsAggregateTotalsInsteadOfAveragingPercentages()
    {
        var result = _provider.Compute(
            Array.Empty<BuildQualityBuildFact>(),
            [
                new BuildQualityTestRunFact(1, 100, 100, 0),
                new BuildQualityTestRunFact(2, 10, 0, 0)
            ],
            [
                new BuildQualityCoverageFact(1, 100, 100),
                new BuildQualityCoverageFact(2, 0, 100)
            ]);

        Assert.AreEqual(100d / 110d, result.Metrics.TestPassRate!.Value, 0.000001d);
        Assert.AreEqual(100d / 200d, result.Metrics.Coverage!.Value, 0.000001d);
    }
}
