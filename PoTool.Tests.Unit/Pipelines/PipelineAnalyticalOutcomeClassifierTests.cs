using PoTool.Core.Pipelines.Analytics;

namespace PoTool.Tests.Unit.Pipelines;

[TestClass]
public sealed class PipelineAnalyticalOutcomeClassifierTests
{
    [TestMethod]
    public void Normalize_MapsRawResultsToCanonicalAnalyticalOutcomes()
    {
        Assert.AreEqual(PipelineAnalyticalOutcome.Succeeded, PipelineAnalyticalOutcomeClassifier.Normalize("Succeeded"));
        Assert.AreEqual(PipelineAnalyticalOutcome.Failed, PipelineAnalyticalOutcomeClassifier.Normalize("Failed"));
        Assert.AreEqual(PipelineAnalyticalOutcome.Warning, PipelineAnalyticalOutcomeClassifier.Normalize("PartiallySucceeded"));
        Assert.AreEqual(PipelineAnalyticalOutcome.Canceled, PipelineAnalyticalOutcomeClassifier.Normalize("Canceled"));
        Assert.AreEqual(PipelineAnalyticalOutcome.Unknown, PipelineAnalyticalOutcomeClassifier.Normalize("Unknown"));
        Assert.AreEqual(PipelineAnalyticalOutcome.Unknown, PipelineAnalyticalOutcomeClassifier.Normalize("None"));
        Assert.AreEqual(PipelineAnalyticalOutcome.Unknown, PipelineAnalyticalOutcomeClassifier.Normalize("Deferred"));
        Assert.AreEqual(PipelineAnalyticalOutcome.Unknown, PipelineAnalyticalOutcomeClassifier.Normalize(string.Empty));
        Assert.AreEqual(PipelineAnalyticalOutcome.Unknown, PipelineAnalyticalOutcomeClassifier.Normalize(null));
    }

    [TestMethod]
    public void ApplyMetricInclusion_ExcludesWarning_WhenWarningToggleIsOff()
    {
        var actual = PipelineAnalyticalOutcomeClassifier.ApplyMetricInclusion(
            PipelineAnalyticalOutcome.Warning,
            includeWarnings: false,
            includeCanceled: true);

        Assert.AreEqual(PipelineAnalyticalOutcome.Ignored, actual);
    }

    [TestMethod]
    public void ApplyMetricInclusion_ExcludesCanceled_WhenCanceledToggleIsOff()
    {
        var actual = PipelineAnalyticalOutcomeClassifier.ApplyMetricInclusion(
            PipelineAnalyticalOutcome.Canceled,
            includeWarnings: true,
            includeCanceled: false);

        Assert.AreEqual(PipelineAnalyticalOutcome.Ignored, actual);
    }
}
