namespace PoTool.Shared.Pipelines;

/// <summary>
/// Response for the per-sprint pipeline trends endpoint.
/// </summary>
public sealed class GetPipelineSprintTrendsResponse
{
    /// <summary>Whether the request succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Error message when Success is false.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Per-sprint pipeline metrics ordered by sprint start date.</summary>
    public IReadOnlyList<PipelineSprintMetricsDto> Sprints { get; set; } = Array.Empty<PipelineSprintMetricsDto>();

    /// <summary>Count of pipeline runs whose finish date falls outside any sprint boundary.</summary>
    public int UnmappedRunCount { get; set; }

    /// <summary>Percentage of runs that were mapped to a sprint (0–100).</summary>
    public double SprintCoveragePercent { get; set; }

    /// <summary>
    /// True when at least one sprint in the response has main-branch runs and uses
    /// MedianMainBranchDurationHours for the Time-to-Green signal.
    /// False means all sprints fall back to all-branch MedianDurationHours.
    /// </summary>
    public bool HasMainBranchData { get; set; }
}

/// <summary>
/// Aggregated pipeline metrics for a single sprint.
/// Sprint mapping rule: a run belongs to a sprint if its FinishedDate falls within [SprintStartUtc, SprintEndUtc].
/// </summary>
public sealed class PipelineSprintMetricsDto
{
    /// <summary>Sprint ID.</summary>
    public int SprintId { get; set; }

    /// <summary>Sprint display name.</summary>
    public string SprintName { get; set; } = string.Empty;

    /// <summary>Sprint start (UTC).</summary>
    public DateTimeOffset? StartUtc { get; set; }

    /// <summary>Sprint end (UTC).</summary>
    public DateTimeOffset? EndUtc { get; set; }

    /// <summary>Total pipeline runs that finished in this sprint.</summary>
    public int TotalRuns { get; set; }

    /// <summary>Completed (non-Unknown/None) runs.</summary>
    public int CompletedRuns { get; set; }

    /// <summary>Build success rate as percentage (0–100). Null when no completed runs.</summary>
    public double? SuccessRate { get; set; }

    /// <summary>Build failure rate as percentage (0–100). Null when no completed runs.</summary>
    public double? FailureRate { get; set; }

    /// <summary>Median MTTR in hours. Null when no failures with a subsequent success.</summary>
    public double? MedianMttrHours { get; set; }

    /// <summary>75th-percentile MTTR in hours.</summary>
    public double? P75MttrHours { get; set; }

    /// <summary>Median pipeline duration in hours. Null when no duration data.</summary>
    public double? MedianDurationHours { get; set; }

    /// <summary>75th-percentile pipeline duration in hours.</summary>
    public double? P75DurationHours { get; set; }

    /// <summary>90th-percentile pipeline duration in hours. Null when fewer than 3 runs have duration data.</summary>
    public double? P90DurationHours { get; set; }

    /// <summary>
    /// Median duration (h) of runs on the main/master branch per sprint.
    /// Used as the Time-to-Green signal when main-branch runs are present.
    /// Null when no main-branch runs in this sprint.
    /// </summary>
    public double? MedianMainBranchDurationHours { get; set; }

    /// <summary>Number of main/master branch runs with duration data in this sprint.</summary>
    public int MainBranchRunCount { get; set; }

    /// <summary>
    /// Flakiness rate: percentage of distinct pipelines that had both successes and failures.
    /// Null when no pipelines ran.
    /// </summary>
    public double? FlakinessRate { get; set; }

    /// <summary>Median time-to-first-failure-detection in hours (duration of failed runs). Null when no failures.</summary>
    public double? MedianTimeToFirstFailureHours { get; set; }
}
