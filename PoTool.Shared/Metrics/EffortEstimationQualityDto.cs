namespace PoTool.Shared.Metrics;

/// <summary>
/// DTO representing effort estimation quality metrics comparing estimates vs actuals.
/// </summary>
public sealed record EffortEstimationQualityDto(
    double AverageEstimationAccuracy,
    int TotalCompletedWorkItems,
    int WorkItemsWithEstimates,
    IReadOnlyList<WorkItemTypeEstimationQuality> QualityByType,
    IReadOnlyList<EstimationTrend> TrendOverTime
);

/// <summary>
/// Estimation quality metrics for a specific work item type.
/// </summary>
public sealed record WorkItemTypeEstimationQuality(
    string WorkItemType,
    int Count,
    double AverageAccuracy,
    int TypicalEffortMin,
    int TypicalEffortMax,
    int AverageEffort
);

/// <summary>
/// Estimation quality trend over a specific time period.
/// </summary>
public sealed record EstimationTrend(
    string Period,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    double AverageAccuracy,
    int EstimatedCount
);
