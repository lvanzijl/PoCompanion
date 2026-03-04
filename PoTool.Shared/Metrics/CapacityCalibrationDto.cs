namespace PoTool.Shared.Metrics;

/// <summary>
/// Per-sprint entry for capacity calibration.
/// Committed = planned effort; Done = completed PBI effort (velocity).
/// PredictabilityRatio = Done / Committed; 0 when Committed is 0 (no planned work).
/// </summary>
public sealed record SprintCalibrationEntry(
    string SprintName,
    int Committed,
    int Done,
    double PredictabilityRatio
);

/// <summary>
/// Capacity calibration payload for a selected sprint range.
///
/// Velocity percentiles use the P25/P50/P75 convention (quartiles).
/// Outlier detection uses P10/P90: sprints whose Done effort falls below P10
/// or above P90 are flagged as annotation-only signals.
///
/// Primary planning signals:
///   MedianVelocity  — use as "typical capacity" (P50)
///   P25Velocity     — use as "conservative/high-confidence capacity"
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
