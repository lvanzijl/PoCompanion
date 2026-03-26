namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Prepared feature-level inputs required by the canonical epic aggregation engine.
/// </summary>
public sealed record EpicAggregationRequest(
    IReadOnlyList<FeatureProgress> Features);

/// <summary>
/// Canonical epic aggregation output derived strictly from feature-level results.
/// </summary>
public sealed record EpicAggregationResult(
    double? EpicProgress,
    double? EpicForecastConsumed,
    double? EpicForecastRemaining,
    int ExcludedFeaturesCount,
    int IncludedFeaturesCount,
    double TotalWeight);
