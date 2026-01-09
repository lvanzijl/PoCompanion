namespace PoTool.Shared.Pipelines;

/// <summary>
/// Aggregated metrics for a pipeline.
/// </summary>
public record PipelineMetricsDto(
    int PipelineId,
    string PipelineName,
    PipelineType Type,
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    int PartiallySucceededRuns,
    int CanceledRuns,
    double FailureRate,
    TimeSpan? AverageDuration,
    TimeSpan? MinDuration,
    TimeSpan? MaxDuration,
    double? DurationVariance,
    PipelineRunResult? LastRunResult,
    DateTimeOffset? LastRunTime,
    int ConsecutiveFailures
);
