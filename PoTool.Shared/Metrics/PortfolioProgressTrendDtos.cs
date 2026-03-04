namespace PoTool.Shared.Metrics;

/// <summary>
/// Overall trajectory classification for portfolio progress.
/// </summary>
public enum PortfolioTrajectory
{
    /// <summary>% done is rising AND remaining effort is falling.</summary>
    Improving,

    /// <summary>Movement is within tolerance thresholds (±5% done, ±10% remaining).</summary>
    Stable,

    /// <summary>Scope is rising significantly OR remaining effort is increasing.</summary>
    AtRisk
}

/// <summary>
/// Per-sprint portfolio progress data point.
/// </summary>
public record PortfolioSprintProgressDto
{
    /// <summary>Internal sprint ID.</summary>
    public required int SprintId { get; init; }

    /// <summary>Sprint display name.</summary>
    public required string SprintName { get; init; }

    /// <summary>Sprint start date (UTC).</summary>
    public DateTimeOffset? StartUtc { get; init; }

    /// <summary>Sprint end date (UTC).</summary>
    public DateTimeOffset? EndUtc { get; init; }

    /// <summary>
    /// Percentage of total scope completed as of the end of this sprint (0–100).
    /// Null when no effort data exists for this sprint.
    /// Definition: CumulativeDoneEffort / TotalScopeEffort * 100.
    /// </summary>
    public double? PercentDone { get; init; }

    /// <summary>
    /// Total effort in scope (sum of effort of all resolved PBIs, excluding Removed items).
    /// Uses current backlog snapshot; represents the known scope baseline.
    /// </summary>
    public double? TotalScopeEffort { get; init; }

    /// <summary>
    /// Remaining effort as of the end of this sprint.
    /// Definition: TotalScopeEffort - CumulativeDoneEffort.
    /// </summary>
    public double? RemainingEffort { get; init; }

    /// <summary>
    /// Effort completed (transitioned to Done) during this sprint only (throughput).
    /// Null when no effort data exists for this sprint.
    /// </summary>
    public double? ThroughputEffort { get; init; }

    /// <summary>Whether this sprint has any measurable effort data.</summary>
    public bool HasData { get; init; }
}

/// <summary>
/// Compact summary for the selected sprint range.
/// </summary>
public record PortfolioProgressSummaryDto
{
    /// <summary>% done at the start of the selected range (first sprint with data).</summary>
    public double? FirstPercentDone { get; init; }

    /// <summary>% done at the end of the selected range (last sprint with data).</summary>
    public double? LastPercentDone { get; init; }

    /// <summary>
    /// Total scope change across the range expressed as a percentage (+N% means growth).
    /// Currently always null — historical scope tracking requires future implementation.
    /// </summary>
    public double? ScopeChangePercent { get; init; }

    /// <summary>
    /// Change in remaining effort across the range (negative = decreasing = good).
    /// </summary>
    public double? RemainingEffortDelta { get; init; }

    /// <summary>Overall trajectory classification for the selected range.</summary>
    public PortfolioTrajectory Trajectory { get; init; }
}

/// <summary>
/// Portfolio Progress Trend response DTO.
/// Contains per-sprint progress data points and a high-level summary.
/// </summary>
public record PortfolioProgressTrendDto
{
    /// <summary>Per-sprint progress data, ordered chronologically.</summary>
    public required IReadOnlyList<PortfolioSprintProgressDto> Sprints { get; init; }

    /// <summary>Compact summary for the selected sprint range.</summary>
    public required PortfolioProgressSummaryDto Summary { get; init; }
}
