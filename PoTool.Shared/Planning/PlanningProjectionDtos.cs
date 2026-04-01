using PoTool.Shared.Metrics;

namespace PoTool.Shared.Planning;

/// <summary>
/// Read-only planning projection for a roadmap epic backed by persisted forecast data.
/// </summary>
public sealed record PlanningEpicProjectionDto(
    int EpicId,
    string EpicTitle,
    int RoadmapOrder,
    int? SprintsRemaining,
    DateTimeOffset? EstimatedCompletionDate,
    ForecastConfidence? Confidence,
    bool HasForecast,
    DateTimeOffset? LastUpdated);
