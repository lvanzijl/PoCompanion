using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.WorkItems;

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
    private readonly IEpicProgressService _epicProgressService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpicAggregationService"/> class.
    /// </summary>
    public EpicAggregationService(IEpicProgressService? epicProgressService = null)
    {
        _epicProgressService = epicProgressService ?? new EpicProgressService();
    }

    /// <inheritdoc />
    public EpicAggregationResult Compute(EpicAggregationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var features = (request.Features ?? [])
            .Where(feature => CanonicalWorkItemTypes.IsFeature(feature.Feature.WorkItemType))
            .ToList();
        var epicProgress = _epicProgressService.Compute(new EpicProgressCalculationRequest(
            request.Epic,
            features.Select(feature => new EpicFeatureProgress(
                feature.Feature,
                feature.EffectiveProgress,
                feature.TotalEffort))
            .ToList()));

        var consumedForecasts = features
            .Where(feature => feature.ForecastConsumedEffort.HasValue)
            .Select(feature => feature.ForecastConsumedEffort!.Value)
            .ToList();
        var remainingForecasts = features
            .Where(feature => feature.ForecastRemainingEffort.HasValue)
            .Select(feature => feature.ForecastRemainingEffort!.Value)
            .ToList();

        return new EpicAggregationResult(
            epicProgress.EpicProgress * 100d,
            consumedForecasts.Count > 0 ? consumedForecasts.Sum() : null,
            remainingForecasts.Count > 0 ? remainingForecasts.Sum() : null,
            epicProgress.ExcludedFeaturesCount,
            epicProgress.IncludedFeaturesCount,
            epicProgress.TotalWeight);
    }
}
