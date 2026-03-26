using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Aggregates canonical epic outputs into product-level progress and forecast totals.
/// </summary>
public interface IProductAggregationService
{
    /// <summary>
    /// Computes product-level progress and forecast values from canonical epic outputs only.
    /// </summary>
    ProductAggregationResult Compute(ProductAggregationRequest request);
}

/// <summary>
/// Implements the canonical bottom-up product aggregation rules.
/// </summary>
public sealed class ProductAggregationService : IProductAggregationService
{
    /// <inheritdoc />
    public ProductAggregationResult Compute(ProductAggregationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var epics = request.Epics ?? [];
        var excludedEpicsCount = epics.Count(epic =>
            epic.IsExcluded
            || epic.Weight <= 0
            || !epic.EpicProgress.HasValue);

        var includedEpics = epics
            .Where(epic => !epic.IsExcluded
                && epic.Weight > 0
                && epic.EpicProgress.HasValue)
            .ToList();

        var totalWeight = includedEpics.Sum(epic => epic.Weight);
        double? productProgress = totalWeight > 0
            ? includedEpics.Sum(epic => epic.EpicProgress!.Value * epic.Weight) / totalWeight
            : null;

        var consumedForecasts = epics
            .Where(epic => epic.EpicForecastConsumed.HasValue)
            .Select(epic => epic.EpicForecastConsumed!.Value)
            .ToList();
        var remainingForecasts = epics
            .Where(epic => epic.EpicForecastRemaining.HasValue)
            .Select(epic => epic.EpicForecastRemaining!.Value)
            .ToList();

        return new ProductAggregationResult(
            productProgress,
            consumedForecasts.Count > 0 ? consumedForecasts.Sum() : null,
            remainingForecasts.Count > 0 ? remainingForecasts.Sum() : null,
            excludedEpicsCount,
            includedEpics.Count,
            totalWeight);
    }
}
