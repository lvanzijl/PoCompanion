namespace PoTool.Shared.Metrics;

/// <summary>
/// Per-sprint entry for capacity calibration.
/// Velocity uses canonical delivered PBI story points.
/// Effort remains a diagnostic metric only.
/// PredictabilityRatio = DeliveredStoryPoints / CommittedStoryPoints; 0 when CommittedStoryPoints is 0.
/// </summary>
public sealed record SprintCalibrationEntry(
    string SprintName,
    double CommittedStoryPoints,
    double DeliveredStoryPoints,
    int DeliveredEffort,
    double HoursPerSP,
    double PredictabilityRatio
);

/// <summary>
/// Capacity calibration payload for a selected sprint range.
///
/// Velocity percentiles use the P25/P50/P75 convention (quartiles).
/// Outlier detection uses P10/P90: sprints whose delivered story points fall below P10
/// or above P90 are flagged as annotation-only signals.
///
/// Primary planning signals:
///   MedianVelocity       — use as "typical capacity" (P50 delivered story points)
///   P25Velocity          — use as "conservative/high-confidence capacity"
///   MedianPredictability — 1.0 = team delivers exactly what it commits; lower = under-delivery
/// </summary>
public sealed record CapacityCalibrationDto(
    IReadOnlyList<SprintCalibrationEntry> Sprints,
    double MedianVelocity,
    double P25Velocity,
    double P75Velocity,
    double MedianPredictability,
    IReadOnlyList<string> OutlierSprintNames
);
