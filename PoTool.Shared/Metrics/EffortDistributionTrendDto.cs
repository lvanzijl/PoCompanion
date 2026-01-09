namespace PoTool.Shared.Metrics;

/// <summary>
/// DTO representing effort distribution trends over time.
/// Shows how distribution patterns change sprint by sprint.
/// </summary>
public sealed record EffortDistributionTrendDto(
    IReadOnlyList<SprintTrendData> TrendBySprint,
    IReadOnlyList<AreaPathTrendData> TrendByAreaPath,
    EffortTrendDirection OverallTrend,
    double TrendSlope,
    IReadOnlyList<DistributionForecast> Forecasts,
    DateTimeOffset AnalysisTimestamp
);

/// <summary>
/// Trend data for a specific sprint.
/// </summary>
public sealed record SprintTrendData(
    string IterationPath,
    string SprintName,
    int TotalEffort,
    int WorkItemCount,
    double UtilizationPercentage,
    double ChangeFromPrevious,
    EffortTrendDirection Direction
);

/// <summary>
/// Trend data for a specific area path across sprints.
/// </summary>
public sealed record AreaPathTrendData(
    string AreaPath,
    IReadOnlyList<int> EffortBySprint,
    double AverageEffort,
    double StandardDeviation,
    EffortTrendDirection Direction,
    double TrendSlope
);

/// <summary>
/// Forecasted effort distribution for future sprints.
/// </summary>
public sealed record DistributionForecast(
    string SprintName,
    int ForecastedEffort,
    int LowEstimate,
    int HighEstimate,
    double ConfidenceLevel
);

/// <summary>
/// Direction of effort trend movement over time.
/// </summary>
public enum EffortTrendDirection
{
    Stable,
    Increasing,
    Decreasing,
    Volatile
}
