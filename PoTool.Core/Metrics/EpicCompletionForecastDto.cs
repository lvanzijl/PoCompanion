using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics;

/// <summary>
/// DTO representing completion forecast for an Epic or Feature.
/// Uses historical velocity to predict completion date.
/// </summary>
public sealed record EpicCompletionForecastDto(
    int EpicId,
    string Title,
    string Type,
    int TotalEffort,
    int CompletedEffort,
    int RemainingEffort,
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
    int ExpectedCompletedEffort,
    int RemainingEffortAfterSprint,
    double ProgressPercentage
);
