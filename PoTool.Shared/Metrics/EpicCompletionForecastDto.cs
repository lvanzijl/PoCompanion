namespace PoTool.Shared.Metrics;

/// <summary>
/// DTO representing completion forecast for an Epic or Feature.
/// Uses historical velocity to predict completion date.
/// The scope fields retain their legacy names for API compatibility,
/// but now represent canonical story-point rollups and may be fractional
/// when derived estimates are used.
/// </summary>
public sealed record EpicCompletionForecastDto(
    int EpicId,
    string Title,
    string Type,
    double TotalEffort,
    double CompletedEffort,
    double RemainingEffort,
    double EstimatedVelocity,
    int SprintsRemaining,
    DateTimeOffset? EstimatedCompletionDate,
    ForecastConfidence Confidence,
    IReadOnlyList<SprintForecast> ForecastByDate,
    string AreaPath,
    DateTimeOffset AnalysisTimestamp
);

/// <summary>
/// Sprint-by-sprint forecast showing predicted progress.
/// </summary>
public sealed record SprintForecast(
    string SprintName,
    string IterationPath,
    DateTimeOffset SprintStartDate,
    DateTimeOffset SprintEndDate,
    double ExpectedCompletedEffort,
    double RemainingEffortAfterSprint,
    double ProgressPercentage
);

/// <summary>
/// Confidence level for forecast prediction.
/// </summary>
public enum ForecastConfidence
{
    Low,      // Less than 3 sprints of historical data
    Medium,   // 3-5 sprints of historical data
    High      // 5+ sprints of historical data with stable velocity
}
