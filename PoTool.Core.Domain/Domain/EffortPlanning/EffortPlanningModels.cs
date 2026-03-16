using PoTool.Shared.Metrics;

namespace PoTool.Core.Domain.EffortPlanning;

/// <summary>
/// Canonical effort-planning work-item input prepared by application adapters.
/// </summary>
public sealed record EffortPlanningWorkItem(
    int WorkItemId,
    string WorkItemType,
    string Title,
    string AreaPath,
    string IterationPath,
    string State,
    DateTimeOffset RetrievedAt,
    int? Effort);

/// <summary>
/// Canonical effort distribution rollup for the selected snapshot scope.
/// </summary>
public sealed record EffortDistributionResult(
    IReadOnlyList<EffortAreaDistributionResult> EffortByArea,
    IReadOnlyList<EffortIterationDistributionResult> EffortByIteration,
    IReadOnlyList<EffortHeatMapCellResult> HeatMapData,
    int TotalEffort);

/// <summary>
/// Canonical effort distribution by area path.
/// </summary>
public sealed record EffortAreaDistributionResult(
    string AreaPath,
    int TotalEffort,
    int WorkItemCount,
    double AverageEffortPerItem);

/// <summary>
/// Canonical effort distribution by iteration.
/// </summary>
public sealed record EffortIterationDistributionResult(
    string IterationPath,
    string SprintName,
    int TotalEffort,
    int WorkItemCount,
    int? Capacity,
    double UtilizationPercentage);

/// <summary>
/// Canonical effort heat-map cell at the intersection of area path and iteration.
/// </summary>
public sealed record EffortHeatMapCellResult(
    string AreaPath,
    string IterationPath,
    int Effort,
    int WorkItemCount,
    CapacityStatus Status);

/// <summary>
/// Canonical effort-estimation quality rollup for completed work items with effort.
/// </summary>
public sealed record EffortEstimationQualityResult(
    double AverageEstimationAccuracy,
    int TotalCompletedWorkItems,
    int WorkItemsWithEstimates,
    IReadOnlyList<EffortTypeQualityResult> QualityByType,
    IReadOnlyList<EffortQualityTrendResult> TrendOverTime);

/// <summary>
/// Canonical effort-estimation quality rollup for one work-item type.
/// </summary>
public sealed record EffortTypeQualityResult(
    string WorkItemType,
    int Count,
    double AverageAccuracy,
    int TypicalEffortMin,
    int TypicalEffortMax,
    int AverageEffort);

/// <summary>
/// Canonical effort-estimation quality rollup for one period.
/// </summary>
public sealed record EffortQualityTrendResult(
    string Period,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    double AverageAccuracy,
    int EstimatedCount);

/// <summary>
/// Canonical effort suggestion for a single unestimated work item.
/// </summary>
public sealed record EffortEstimationSuggestionResult(
    int WorkItemId,
    string WorkItemTitle,
    string WorkItemType,
    int? CurrentEffort,
    int SuggestedEffort,
    double Confidence,
    int HistoricalMatchCount,
    int HistoricalEffortMin,
    int HistoricalEffortMax,
    IReadOnlyList<EffortHistoricalExampleResult> SimilarWorkItems);

/// <summary>
/// Canonical historical effort example returned with a suggestion.
/// </summary>
public sealed record EffortHistoricalExampleResult(
    int WorkItemId,
    string Title,
    int Effort,
    string State,
    double SimilarityScore);
