namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Prepared epic-level inputs required by the canonical product aggregation engine.
/// </summary>
public sealed record ProductAggregationRequest(
    IReadOnlyList<ProductAggregationEpicInput> Epics);

/// <summary>
/// Prepared canonical epic outputs required for product-level aggregation.
/// </summary>
public sealed record ProductAggregationEpicInput(
    double? EpicProgress,
    double? EpicForecastConsumed,
    double? EpicForecastRemaining,
    double Weight,
    bool IsExcluded);

/// <summary>
/// Canonical product aggregation output derived strictly from epic-level results.
/// </summary>
public sealed record ProductAggregationResult(
    double? ProductProgress,
    double? ProductForecastConsumed,
    double? ProductForecastRemaining,
    int ExcludedEpicsCount,
    int IncludedEpicsCount,
    double TotalWeight);
