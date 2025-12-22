namespace PoTool.Core.Metrics;

/// <summary>
/// DTO representing aggregated backlog health across multiple iterations.
/// Provides trend analysis and comparison capabilities.
/// </summary>
public sealed record MultiIterationBacklogHealthDto(
    IReadOnlyList<BacklogHealthDto> IterationHealth,
    BacklogHealthTrend Trend,
    int TotalWorkItems,
    int TotalIssues,
    DateTimeOffset AnalysisTimestamp
);

/// <summary>
/// Trend analysis across multiple sprints showing improvement or degradation.
/// </summary>
public sealed record BacklogHealthTrend(
    TrendDirection EffortTrend,
    TrendDirection ValidationTrend,
    TrendDirection BlockerTrend,
    string Summary
);

/// <summary>
/// Direction of a trend across sprints.
/// </summary>
public enum TrendDirection
{
    Improving,
    Stable,
    Degrading,
    Unknown
}
