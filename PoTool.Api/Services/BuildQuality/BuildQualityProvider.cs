using PoTool.Core.Pipelines.Analytics;
using PoTool.Shared.BuildQuality;

namespace PoTool.Api.Services.BuildQuality;

/// <summary>
/// Shared canonical BuildQuality provider.
/// </summary>
public sealed class BuildQualityProvider : IBuildQualityProvider
{
    private const int MinimumBuilds = 3;
    private const int MinimumTests = 20;

    public BuildQualityResultDto Compute(
        IEnumerable<BuildQualityBuildFact> builds,
        IEnumerable<BuildQualityTestRunFact> testRuns,
        IEnumerable<BuildQualityCoverageFact> coverages)
    {
        var buildList = builds.ToList();
        var testRunList = testRuns.ToList();
        var coverageList = coverages.ToList();
        var normalizedBuildOutcomes = buildList
            .Select(build => PipelineAnalyticalOutcomeClassifier.Normalize(build.Result))
            .ToList();

        var succeededBuilds = normalizedBuildOutcomes.Count(outcome => outcome == PipelineAnalyticalOutcome.Succeeded);
        var failedBuilds = normalizedBuildOutcomes.Count(outcome => outcome == PipelineAnalyticalOutcome.Failed);
        var partiallySucceededBuilds = normalizedBuildOutcomes.Count(outcome => outcome == PipelineAnalyticalOutcome.Warning);
        var canceledBuilds = normalizedBuildOutcomes.Count(outcome => outcome == PipelineAnalyticalOutcome.Canceled);
        var eligibleBuilds = succeededBuilds + failedBuilds + partiallySucceededBuilds;

        var totalTests = testRunList.Sum(testRun => testRun.TotalTests);
        var passedTests = testRunList.Sum(testRun => testRun.PassedTests);
        var notApplicableTests = testRunList.Sum(testRun => testRun.NotApplicableTests);
        var testVolume = totalTests - notApplicableTests;

        var coveredLines = coverageList.Sum(coverage => coverage.CoveredLines);
        var totalLines = coverageList.Sum(coverage => coverage.TotalLines);

        var successRateUnknown = eligibleBuilds == 0;
        var testPassRateUnknown = testRunList.Count == 0;
        var coverageUnknown = coverageList.Count == 0 || totalLines == 0;

        var buildThresholdMet = eligibleBuilds >= MinimumBuilds;
        var testThresholdMet = testVolume >= MinimumTests;

        return new BuildQualityResultDto
        {
            Metrics = new BuildQualityMetricsDto
            {
                SuccessRate = successRateUnknown ? null : (double)succeededBuilds / eligibleBuilds,
                TestPassRate = testPassRateUnknown
                    ? null
                    : testVolume > 0
                        ? (double)passedTests / testVolume
                        : 0d,
                TestVolume = testVolume,
                Coverage = coverageUnknown ? null : (double)coveredLines / totalLines,
                Confidence = (buildThresholdMet ? 1 : 0) + (testThresholdMet ? 1 : 0)
            },
            Evidence = new BuildQualityEvidenceDto
            {
                EligibleBuilds = eligibleBuilds,
                SucceededBuilds = succeededBuilds,
                FailedBuilds = failedBuilds,
                PartiallySucceededBuilds = partiallySucceededBuilds,
                CanceledBuilds = canceledBuilds,
                TotalTests = totalTests,
                PassedTests = passedTests,
                NotApplicableTests = notApplicableTests,
                CoveredLines = coveredLines,
                TotalLines = totalLines,
                BuildThresholdMet = buildThresholdMet,
                TestThresholdMet = testThresholdMet,
                SuccessRateUnknown = successRateUnknown,
                SuccessRateUnknownReason = successRateUnknown ? BuildQualityUnknownReasons.NoEligibleBuilds : null,
                TestPassRateUnknown = testPassRateUnknown,
                TestPassRateUnknownReason = testPassRateUnknown ? BuildQualityUnknownReasons.NoTestRuns : null,
                CoverageUnknown = coverageUnknown,
                CoverageUnknownReason = coverageList.Count == 0
                    ? BuildQualityUnknownReasons.NoCoverage
                    : totalLines == 0
                        ? BuildQualityUnknownReasons.ZeroTotalLines
                        : null
            }
        };
    }

}
