using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Computes canonical forecast effort values from already-calculated feature progress.
/// </summary>
public interface IFeatureForecastService
{
    /// <summary>
    /// Computes the canonical feature forecast result for one feature.
    /// </summary>
    FeatureForecastResult Compute(FeatureForecastCalculationRequest request);
}

/// <summary>
/// Pure feature forecast engine used as the single source of truth for forecast consumed and remaining effort.
/// </summary>
public sealed class FeatureForecastService : IFeatureForecastService
{
    /// <inheritdoc />
    public FeatureForecastResult Compute(FeatureForecastCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Effort.HasValue)
        {
            return new FeatureForecastResult(
                request.Effort,
                request.EffectiveProgress,
                ForecastConsumedEffort: null,
                ForecastRemainingEffort: null);
        }

        var clampedProgressRatio = Math.Clamp((request.EffectiveProgress ?? 0d) / 100d, 0d, 1d);
        var forecastConsumedEffort = request.Effort.Value * clampedProgressRatio;
        var forecastRemainingEffort = Math.Max(0d, request.Effort.Value - forecastConsumedEffort);

        return new FeatureForecastResult(
            request.Effort,
            request.EffectiveProgress,
            forecastConsumedEffort,
            forecastRemainingEffort);
    }
}
