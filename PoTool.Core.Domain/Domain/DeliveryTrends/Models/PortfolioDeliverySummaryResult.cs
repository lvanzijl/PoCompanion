namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Product-level delivery projection row supplied by an application adapter.
/// </summary>
public sealed record PortfolioDeliveryProductProjectionInput(
    int ProductId,
    string ProductName,
    int CompletedPbis,
    double DeliveredStoryPoints,
    int BugsCreated,
    int BugsWorked,
    int BugsClosed,
    double ProgressionDelta);

/// <summary>
/// Feature-level delivery contribution input supplied by an application adapter.
/// </summary>
public sealed record PortfolioFeatureContributionInput(
    int WorkItemId,
    string Title,
    string? EpicTitle,
    int ProductId,
    string ProductName,
    double DeliveredStoryPoints,
    double TotalScopeStoryPoints,
    int ProgressPercent);

/// <summary>
/// Input contract for canonical portfolio delivery composition.
/// </summary>
public sealed record PortfolioDeliverySummaryRequest(
    IReadOnlyList<PortfolioDeliveryProductProjectionInput> ProductProjections,
    IReadOnlyList<PortfolioFeatureContributionInput> FeatureContributions,
    int TopFeatureLimit);

/// <summary>
/// Canonical portfolio delivery summary for the selected sprint range.
/// </summary>
public sealed record PortfolioDeliverySummaryResult(
    double TotalDeliveredStoryPoints,
    int TotalCompletedPbis,
    double AverageProgressPercent,
    int TotalBugsCreated,
    int TotalBugsWorked,
    int TotalBugsClosed,
    IReadOnlyList<PortfolioProductDeliverySummaryResult> ProductSummaries,
    IReadOnlyList<PortfolioFeatureContributionSummaryResult> FeatureContributionSummaries)
{
    /// <summary>
    /// Compatibility alias for total completed bugs, which currently maps to closed bugs.
    /// </summary>
    public int TotalCompletedBugs => TotalBugsClosed;
}

/// <summary>
/// Canonical product-level delivery summary within the portfolio.
/// </summary>
public sealed record PortfolioProductDeliverySummaryResult(
    int ProductId,
    string ProductName,
    int CompletedPbis,
    double DeliveredStoryPoints,
    double DeliveredSharePercent,
    int BugsCreated,
    int BugsWorked,
    int BugsClosed,
    double ProgressionDelta);

/// <summary>
/// Canonical feature-level delivery contribution summary within the portfolio.
/// </summary>
public sealed record PortfolioFeatureContributionSummaryResult(
    int WorkItemId,
    string Title,
    string? EpicTitle,
    int ProductId,
    string ProductName,
    double DeliveredStoryPoints,
    double DeliveredSharePercent,
    double TotalScopeStoryPoints,
    int ProgressPercent);
