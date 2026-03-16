using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Produces canonical portfolio delivery composition summaries from DeliveryTrends inputs.
/// </summary>
public interface IPortfolioDeliverySummaryService
{
    /// <summary>
    /// Builds the canonical portfolio delivery summary for a selected sprint range.
    /// </summary>
    PortfolioDeliverySummaryResult BuildSummary(PortfolioDeliverySummaryRequest request);
}

/// <summary>
/// Implements canonical portfolio delivery totals and contribution-share formulas.
/// </summary>
public sealed class PortfolioDeliverySummaryService : IPortfolioDeliverySummaryService
{
    /// <inheritdoc />
    public PortfolioDeliverySummaryResult BuildSummary(PortfolioDeliverySummaryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var productSummaries = request.ProductProjections
            .GroupBy(projection => new { projection.ProductId, projection.ProductName })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.ProductName,
                CompletedPbis = group.Sum(projection => projection.CompletedPbis),
                DeliveredStoryPoints = group.Sum(projection => projection.DeliveredStoryPoints),
                BugsCreated = group.Sum(projection => projection.BugsCreated),
                BugsWorked = group.Sum(projection => projection.BugsWorked),
                BugsClosed = group.Sum(projection => projection.BugsClosed),
                ProgressionDelta = group.Sum(projection => projection.ProgressionDelta)
            })
            .ToList();

        var totalDeliveredStoryPoints = productSummaries.Sum(summary => summary.DeliveredStoryPoints);

        var orderedProductSummaries = productSummaries
            .Select(summary => new PortfolioProductDeliverySummaryResult(
                summary.ProductId,
                summary.ProductName,
                summary.CompletedPbis,
                summary.DeliveredStoryPoints,
                DeliveredSharePercent: totalDeliveredStoryPoints > 0d
                    ? summary.DeliveredStoryPoints / totalDeliveredStoryPoints * 100d
                    : 0d,
                summary.BugsCreated,
                summary.BugsWorked,
                summary.BugsClosed,
                summary.ProgressionDelta))
            .OrderByDescending(summary => summary.DeliveredStoryPoints)
            .ThenBy(summary => summary.ProductName, StringComparer.Ordinal)
            .ToList();

        var topFeatureLimit = Math.Max(0, request.TopFeatureLimit);
        var orderedFeatureContributions = request.FeatureContributions
            .Where(feature => feature.DeliveredStoryPoints > 0d)
            .OrderByDescending(feature => feature.DeliveredStoryPoints)
            .ThenBy(feature => feature.Title, StringComparer.Ordinal)
            .Take(topFeatureLimit)
            .Select(feature => new PortfolioFeatureContributionSummaryResult(
                feature.WorkItemId,
                feature.Title,
                feature.EpicTitle,
                feature.ProductId,
                feature.ProductName,
                feature.DeliveredStoryPoints,
                DeliveredSharePercent: totalDeliveredStoryPoints > 0d
                    ? feature.DeliveredStoryPoints / totalDeliveredStoryPoints * 100d
                    : 0d,
                feature.TotalScopeStoryPoints,
                feature.ProgressPercent))
            .ToList();

        return new PortfolioDeliverySummaryResult(
            TotalDeliveredStoryPoints: totalDeliveredStoryPoints,
            TotalCompletedPbis: orderedProductSummaries.Sum(summary => summary.CompletedPbis),
            AverageProgressPercent: orderedProductSummaries.Count > 0
                ? orderedProductSummaries.Sum(summary => summary.ProgressionDelta) / orderedProductSummaries.Count
                : 0d,
            TotalBugsCreated: orderedProductSummaries.Sum(summary => summary.BugsCreated),
            TotalBugsWorked: orderedProductSummaries.Sum(summary => summary.BugsWorked),
            TotalBugsClosed: orderedProductSummaries.Sum(summary => summary.BugsClosed),
            ProductSummaries: orderedProductSummaries,
            FeatureContributionSummaries: orderedFeatureContributions);
    }
}
