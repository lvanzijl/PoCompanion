namespace PoTool.Shared.Metrics;

/// <summary>
/// Stock-and-flow classification for portfolio trajectory.
/// The DTO surface retains several legacy <c>*Effort</c> names for compatibility,
/// and in this API they represent effort-based scope proxies rather than canonical
/// story-point scope.
///
/// Classification rules (documented in GetPortfolioProgressTrendQueryHandler.ComputeSummary):
///   Expanding   — cumulative Net Flow &lt; -tolerance AND Remaining Effort is increasing.
///                 Backlog is growing; adding more scope than delivering.
///   Contracting — cumulative Net Flow &gt; +tolerance AND Remaining Effort is decreasing.
///                 Backlog is shrinking; delivering more than adding.
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
/// Stock fields:  TotalScopeEffort, RemainingEffort
/// Flow fields:   ThroughputEffort (completed), AddedEffort, NetFlow
///
/// LIMITATION — AddedEffort definition:
///   AddedEffort is computed from SprintMetricsProjection.PlannedEffort, which represents
///   items committed to the sprint backlog at projection time.
///   It does NOT represent items newly created in the product backlog during the sprint.
///   True scope-inflow tracking requires per-event backlog change history (not stored here).
///   PlannedEffort also includes re-estimated items; large re-estimations may distort
///   Net Flow temporarily, as creation vs. estimation deltas cannot be distinguished.
///   If this limitation is resolved in a future data model update, this comment should be
///   updated to reflect which definition is actually implemented.
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
    /// Total effort in scope (sum of effort of all resolved PBIs, excluding Removed items)
    /// at the END of this sprint.
    ///
    /// Computed historically by replaying <c>Microsoft.VSTS.Scheduling.Effort</c> change
    /// events from the ActivityEventLedger. Items created after this sprint's end date are
    /// excluded; items currently in "Removed" state that were removed after this sprint's end
    /// date are included.
    ///
    /// Accuracy: if the activity ledger does not contain events going back to this sprint's
    /// end date, the current effort value is used as a best-effort approximation.
    /// </summary>
    public double? TotalScopeEffort { get; init; }

    /// <summary>
    /// Remaining scope effort as of the end of this sprint.
    /// This is the effort-based stock proxy for backlog still open after cumulative deliveries.
    /// Definition: TotalScopeEffort - CumulativeDoneEffort.
    /// </summary>
    public double? RemainingEffort { get; init; }

    /// <summary>
    /// Effort completed (transitioned to Done) during this sprint only (throughput / outflow).
    /// Null when no effort data exists for this sprint.
    /// </summary>
    public double? ThroughputEffort { get; init; }

    /// <summary>
    /// Added effort (scope inflow) for this sprint.
    /// Proxy: PlannedEffort from SprintMetricsProjection (see class-level limitation note).
    /// Null when no effort data exists for this sprint.
    /// </summary>
    public double? AddedEffort { get; init; }

    /// <summary>
    /// Net Flow = ThroughputEffort − AddedEffort.
    /// Positive = backlog shrinking (good); Negative = backlog expanding (growth or risk).
    /// Null when either ThroughputEffort or AddedEffort is null.
    /// </summary>
    public double? NetFlow { get; init; }

    /// <summary>Whether this sprint has any measurable effort data.</summary>
    public bool HasData { get; init; }
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
    /// Change in TotalScopeEffort across the selected range (last − first), in effort units.
    /// Computed from historically-reconstructed per-sprint scope values.
    /// Positive = scope grew; Negative = scope contracted.
    /// </summary>
    public double? TotalScopeChangePts { get; init; }

    /// <summary>
    /// Total scope change as a percentage of first-sprint scope.
    /// Null when first-sprint scope is zero or unavailable.
    /// </summary>
    public double? TotalScopeChangePercent { get; init; }

    /// <summary>
    /// Change in RemainingEffort across the selected range (last − first), in effort units.
    /// This is a remaining-scope-effort delta even though the legacy property name says effort.
    /// Negative = backlog digestion pressure decreased (good).
    /// </summary>
    public double? RemainingEffortChangePts { get; init; }

    /// <summary>Overall stock-and-flow classification for the selected range.</summary>
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
