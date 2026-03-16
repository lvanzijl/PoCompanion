namespace PoTool.Shared.Metrics;

/// <summary>
/// Stock-and-flow classification for portfolio trajectory.
/// The DTO surface exposes canonical story-point PortfolioFlow metrics and retains
/// a few legacy property names as explicit compatibility aliases.
///
/// Classification rules (documented in GetPortfolioProgressTrendQueryHandler.ComputeSummary):
///   Expanding   — cumulative Net Flow &lt; -tolerance.
///                 Backlog is growing; inflow exceeds throughput.
///   Contracting — cumulative Net Flow &gt; +tolerance.
///                 Backlog is shrinking; throughput exceeds inflow.
///   Stable      — small net movements within tolerance (|cumulative Net| ≤ tolerance).
/// </summary>
public enum PortfolioTrajectory
{
    /// <summary>Backlog is shrinking: delivering more than adding.</summary>
    Contracting,

    /// <summary>Movement is within tolerance thresholds.</summary>
    Stable,

    /// <summary>Backlog is growing: adding more scope than delivering.</summary>
    Expanding
}

/// <summary>
/// Per-sprint portfolio progress data point.
///
/// Canonical fields use story-point PortfolioFlow semantics:
/// StockStoryPoints, RemainingScopeStoryPoints, ThroughputStoryPoints, InflowStoryPoints,
/// NetFlowStoryPoints, and CompletionPercent.
///
/// Compatibility aliases:
/// - PercentDone = CompletionPercent
/// - TotalScopeEffort = StockStoryPoints
/// - RemainingEffort = RemainingScopeStoryPoints
/// - ThroughputEffort = ThroughputStoryPoints
/// - AddedEffort = InflowStoryPoints
/// - NetFlow = NetFlowStoryPoints
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
    /// Percentage of portfolio stock completed as of the end of this sprint (0–100).
    /// Null when stock is zero or unavailable.
    /// </summary>
    public double? CompletionPercent { get; init; }

    /// <summary>
    /// Total portfolio stock story points at the end of this sprint, excluding Removed items.
    /// </summary>
    public double? StockStoryPoints { get; init; }

    /// <summary>
    /// Remaining open backlog story points at the end of this sprint.
    /// </summary>
    public double? RemainingScopeStoryPoints { get; init; }

    /// <summary>
    /// Story points delivered during this sprint only (throughput / outflow).
    /// </summary>
    public double? ThroughputStoryPoints { get; init; }

    /// <summary>
    /// Story points that newly entered the portfolio backlog during this sprint.
    /// </summary>
    public double? InflowStoryPoints { get; init; }

    /// <summary>
    /// Net Flow = ThroughputStoryPoints − InflowStoryPoints.
    /// Positive = backlog shrinking (good); Negative = backlog expanding (growth or risk).
    /// Null when either ThroughputStoryPoints or InflowStoryPoints is null.
    /// </summary>
    public double? NetFlowStoryPoints { get; init; }

    /// <summary>Whether this sprint has any measurable trend data.</summary>
    public bool HasData { get; init; }

    /// <summary>
    /// Compatibility alias for CompletionPercent.
    /// Retained for legacy consumers and intended for removal after the canonical field migration.
    /// </summary>
    public double? PercentDone => CompletionPercent;

    /// <summary>
    /// Compatibility alias for StockStoryPoints.
    /// Retained for legacy consumers and intended for removal after the canonical field migration.
    /// </summary>
    public double? TotalScopeEffort => StockStoryPoints;

    /// <summary>
    /// Compatibility alias for RemainingScopeStoryPoints.
    /// Retained for legacy consumers and intended for removal after the canonical field migration.
    /// </summary>
    public double? RemainingEffort => RemainingScopeStoryPoints;

    /// <summary>
    /// Compatibility alias for ThroughputStoryPoints.
    /// Retained for legacy consumers and intended for removal after the canonical field migration.
    /// </summary>
    public double? ThroughputEffort => ThroughputStoryPoints;

    /// <summary>
    /// Compatibility alias for InflowStoryPoints.
    /// Retained for legacy consumers and intended for removal after the canonical field migration.
    /// </summary>
    public double? AddedEffort => InflowStoryPoints;

    /// <summary>
    /// Compatibility alias for NetFlowStoryPoints.
    /// Retained for legacy consumers and intended for removal after the canonical field migration.
    /// </summary>
    public double? NetFlow => NetFlowStoryPoints;
}

/// <summary>
/// Stock-and-flow summary for the selected sprint range.
/// </summary>
public record PortfolioProgressSummaryDto
{
    /// <summary>
    /// Sum of NetFlow across all sprints in the range (positive = backlog contracted).
    /// </summary>
    public double? CumulativeNetFlow { get; init; }

    /// <summary>
    /// Change in stock story points across the selected range (last − first).
    /// Positive = scope grew; Negative = scope contracted.
    /// </summary>
    public double? TotalScopeChangeStoryPoints { get; init; }

    /// <summary>
    /// Total scope change as a percentage of first-sprint scope.
    /// Null when first-sprint scope is zero or unavailable.
    /// </summary>
    public double? TotalScopeChangePercent { get; init; }

    /// <summary>
    /// Change in remaining scope story points across the selected range (last − first).
    /// Negative = backlog digestion pressure decreased (good).
    /// </summary>
    public double? RemainingScopeChangeStoryPoints { get; init; }

    /// <summary>Overall stock-and-flow classification for the selected range.</summary>
    public PortfolioTrajectory Trajectory { get; init; }

    /// <summary>
    /// Compatibility alias for TotalScopeChangeStoryPoints.
    /// Retained for legacy consumers and intended for removal after the canonical field migration.
    /// </summary>
    public double? TotalScopeChangePts => TotalScopeChangeStoryPoints;

    /// <summary>
    /// Compatibility alias for RemainingScopeChangeStoryPoints.
    /// Retained for legacy consumers and intended for removal after the canonical field migration.
    /// </summary>
    public double? RemainingEffortChangePts => RemainingScopeChangeStoryPoints;
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
