using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Computes product-level delivery progress summaries from canonical epic progress outputs.
/// </summary>
public static class DeliveryProgressSummaryCalculator
{
    /// <summary>
    /// Aggregates epic progress into product-level delivery summaries.
    /// </summary>
    public static IReadOnlyDictionary<int, ProductDeliveryProgressSummary> ComputeProductSummaries(
        IReadOnlyList<EpicProgress> epicProgress)
    {
        ArgumentNullException.ThrowIfNull(epicProgress);

        return epicProgress
            .GroupBy(epic => epic.ProductId)
            .ToDictionary(
                group => group.Key,
                group => new ProductDeliveryProgressSummary(
                    group.Key,
                    group.Sum(epic => epic.SprintEffortDelta),
                    group.Sum(epic => epic.SprintCompletedFeatureCount)));
    }
}
