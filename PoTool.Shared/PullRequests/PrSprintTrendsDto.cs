namespace PoTool.Shared.PullRequests;

/// <summary>
/// Response for the per-sprint PR trends endpoint.
/// </summary>
public sealed class GetPrSprintTrendsResponse
{
    /// <summary>Whether the request succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Error message when Success is false.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Per-sprint PR metrics ordered by sprint start date.</summary>
    public IReadOnlyList<PrSprintMetricsDto> Sprints { get; set; } = Array.Empty<PrSprintMetricsDto>();
}

/// <summary>
/// Aggregated PR metrics for a single sprint.
/// Sprint mapping rule: a PR belongs to a sprint if its CreatedDateUtc falls within [SprintStartDateUtc, SprintEndDateUtc).
/// </summary>
public sealed class PrSprintMetricsDto
{
    /// <summary>Sprint ID.</summary>
    public int SprintId { get; set; }

    /// <summary>Sprint display name.</summary>
    public string SprintName { get; set; } = string.Empty;

    /// <summary>Sprint start (UTC).</summary>
    public DateTimeOffset? StartUtc { get; set; }

    /// <summary>Sprint end (UTC).</summary>
    public DateTimeOffset? EndUtc { get; set; }

    /// <summary>Total PRs created in this sprint.</summary>
    public int TotalPrs { get; set; }

    /// <summary>
    /// Metric 1: Median PR size.
    /// Primary: sum of lines added + deleted (lines changed) per PR.
    /// Fallback: count of distinct files changed per PR when all lines data is zero.
    /// Null when no PRs in this sprint have size data.
    /// </summary>
    public double? MedianPrSize { get; set; }

    /// <summary>
    /// true = MedianPrSize represents lines changed; false = represents files changed (fallback).
    /// </summary>
    public bool PrSizeIsLinesChanged { get; set; }

    /// <summary>
    /// Metric 2: Median time to first review (hours).
    /// Computed as: earliest non-author comment CreatedDate minus PR CreatedDate.
    /// Choice rationale: PullRequestComments are the earliest non-author activity available in cached data.
    /// Null when no sprint PRs have a non-author comment within the sprint range data.
    /// </summary>
    public double? MedianTimeToFirstReviewHours { get; set; }

    /// <summary>
    /// Metric 3: Median time to merge (hours).
    /// Computed as: CompletedDate minus CreatedDate for completed/merged PRs.
    /// Null when no completed PRs exist in this sprint.
    /// </summary>
    public double? MedianTimeToMergeHours { get; set; }

    /// <summary>
    /// Metric 4: P90 time to merge (hours).
    /// 90th percentile of time-to-merge for completed PRs.
    /// Null when the sample size is too small (&lt; 3 PRs) or no completed PRs exist.
    /// </summary>
    public double? P90TimeToMergeHours { get; set; }
}
