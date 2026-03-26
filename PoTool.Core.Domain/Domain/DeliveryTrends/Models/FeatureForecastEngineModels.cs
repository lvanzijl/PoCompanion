namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Pure calculation request for the canonical feature forecast engine.
/// </summary>
public sealed record FeatureForecastCalculationRequest(
    double? EffectiveProgress,
    double? Effort);

/// <summary>
/// Canonical feature forecast result expressed in forecast effort-hours.
/// </summary>
public sealed record FeatureForecastResult(
    double? Effort,
    double? EffectiveProgress,
    double? ForecastConsumedEffort,
    double? ForecastRemainingEffort);
