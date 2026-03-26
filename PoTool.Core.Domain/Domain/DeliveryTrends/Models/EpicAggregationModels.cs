using PoTool.Core.Domain.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Prepared feature-level input required by epic progress and aggregation services.
/// </summary>
public sealed record EpicFeatureProgress(
    CanonicalWorkItem Feature,
    double EffectiveProgress,
    double TotalEffort,
    double? ForecastConsumedEffort = null,
    double? ForecastRemainingEffort = null);

/// <summary>
/// Pure calculation request for the canonical epic progress engine.
/// </summary>
public sealed record EpicProgressCalculationRequest(
    CanonicalWorkItem Epic,
    IReadOnlyList<EpicFeatureProgress> Features);

/// <summary>
/// Canonical epic progress engine result.
/// </summary>
public sealed record EpicProgressResult(
    double EpicProgress,
    int ExcludedFeaturesCount,
    int IncludedFeaturesCount,
    double TotalWeight);

/// <summary>
/// Prepared feature-level inputs required by the canonical epic aggregation engine.
/// </summary>
public sealed record EpicAggregationRequest(
    CanonicalWorkItem Epic,
    IReadOnlyList<EpicFeatureProgress> Features);

/// <summary>
/// Canonical epic aggregation output derived strictly from feature-level results.
/// </summary>
public sealed record EpicAggregationResult(
    double EpicProgress,
    double? EpicForecastConsumed,
    double? EpicForecastRemaining,
    int ExcludedFeaturesCount,
    int IncludedFeaturesCount,
    double TotalWeight);
