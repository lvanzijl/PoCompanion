using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Aggregates canonical feature outputs into epic-level progress and forecast totals.
/// </summary>
public interface IEpicAggregationService
{
    /// <summary>
    /// Computes epic-level progress and forecast values from canonical feature outputs only.
    /// </summary>
    EpicAggregationResult Compute(EpicAggregationRequest request);
}

/// <summary>
/// Implements the canonical bottom-up epic aggregation rules.
/// </summary>
public sealed class EpicAggregationService : IEpicAggregationService
{
    /// <inheritdoc />
    public EpicAggregationResult Compute(EpicAggregationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var features = request.Features ?? [];
        var excludedFeaturesCount = features.Count(feature => feature.IsExcluded);

        var includedFeatures = features
            .Where(feature => !feature.IsExcluded
                && feature.Weight > 0
                && feature.EffectiveProgress.HasValue)
            .ToList();

        var totalWeight = includedFeatures.Sum(feature => feature.Weight);
        double? epicProgress = totalWeight > 0
            ? includedFeatures.Sum(feature => feature.EffectiveProgress!.Value * feature.Weight) / totalWeight
            : null;

        var consumedForecasts = features
            .Where(feature => feature.ForecastConsumedEffort.HasValue)
            .Select(feature => feature.ForecastConsumedEffort!.Value)
            .ToList();
        var remainingForecasts = features
            .Where(feature => feature.ForecastRemainingEffort.HasValue)
            .Select(feature => feature.ForecastRemainingEffort!.Value)
            .ToList();

        return new EpicAggregationResult(
            epicProgress,
            consumedForecasts.Count > 0 ? consumedForecasts.Sum() : null,
            remainingForecasts.Count > 0 ? remainingForecasts.Sum() : null,
            excludedFeaturesCount,
            includedFeatures.Count,
            totalWeight);
    }
}
