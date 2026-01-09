namespace PoTool.Shared.Metrics;

/// <summary>
/// Velocity trend DTO showing historical velocity data and averages.
/// </summary>
public sealed record VelocityTrendDto(
    IReadOnlyList<SprintMetricsDto> Sprints,
    double AverageVelocity,
    double ThreeSprintAverage,
    double SixSprintAverage,
    int TotalCompletedStoryPoints,
    int TotalSprints
);
