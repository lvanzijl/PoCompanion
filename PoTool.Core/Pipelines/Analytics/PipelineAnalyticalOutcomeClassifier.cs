namespace PoTool.Core.Pipelines.Analytics;

/// <summary>
/// Canonical analytical outcome categories used across pipeline analytics slices.
/// </summary>
public enum PipelineAnalyticalOutcome
{
    Succeeded = 0,
    Failed = 1,
    Warning = 2,
    Canceled = 3,
    Unknown = 4,
    Ignored = 5
}

/// <summary>
/// Normalizes raw pipeline/build results into canonical analytical outcome categories.
/// </summary>
public static class PipelineAnalyticalOutcomeClassifier
{
    public static PipelineAnalyticalOutcome Normalize(string? rawResult)
    {
        if (string.IsNullOrWhiteSpace(rawResult))
        {
            return PipelineAnalyticalOutcome.Unknown;
        }

        return rawResult.Trim().ToLowerInvariant() switch
        {
            "succeeded" => PipelineAnalyticalOutcome.Succeeded,
            "failed" => PipelineAnalyticalOutcome.Failed,
            "partiallysucceeded" => PipelineAnalyticalOutcome.Warning,
            "canceled" => PipelineAnalyticalOutcome.Canceled,
            "unknown" or "none" => PipelineAnalyticalOutcome.Unknown,
            _ => PipelineAnalyticalOutcome.Unknown
        };
    }

    public static PipelineAnalyticalOutcome ApplyMetricInclusion(
        PipelineAnalyticalOutcome outcome,
        bool includeWarnings,
        bool includeCanceled)
    {
        return outcome switch
        {
            PipelineAnalyticalOutcome.Warning when !includeWarnings => PipelineAnalyticalOutcome.Ignored,
            PipelineAnalyticalOutcome.Canceled when !includeCanceled => PipelineAnalyticalOutcome.Ignored,
            _ => outcome
        };
    }
}
