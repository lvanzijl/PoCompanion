using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetPortfolioDeliveryQuery.
///
/// Produces an aggregated delivery snapshot across products for a selected sprint range.
/// Aggregates SprintMetricsProjectionEntity records per product, then adds feature-level
/// contribution data.
///
/// No time-series data is emitted — this is a composition/distribution view.
/// </summary>
public sealed class GetPortfolioDeliveryQueryHandler
    : IQueryHandler<GetPortfolioDeliveryQuery, PortfolioDeliveryDto>
{
    /// <summary>Maximum number of top feature contributors to return.</summary>
    private const int TopFeatureLimit = 10;

    private readonly PoToolDbContext _context;
    private readonly SprintTrendProjectionService _projectionService;
    private readonly IPortfolioDeliverySummaryService _portfolioDeliverySummaryService;
    private readonly ILogger<GetPortfolioDeliveryQueryHandler> _logger;

    public GetPortfolioDeliveryQueryHandler(
        PoToolDbContext context,
        SprintTrendProjectionService projectionService,
        IPortfolioDeliverySummaryService portfolioDeliverySummaryService,
        ILogger<GetPortfolioDeliveryQueryHandler> logger)
    {
        _context = context;
        _projectionService = projectionService;
        _portfolioDeliverySummaryService = portfolioDeliverySummaryService;
        _logger = logger;
    }

    public async ValueTask<PortfolioDeliveryDto> Handle(
        GetPortfolioDeliveryQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling GetPortfolioDeliveryQuery for ProductOwner {ProductOwnerId}, {SprintCount} sprints",
            query.ProductOwnerId, query.SprintIds.Count);

        // Resolve products for this product owner
        var ownerProductIds = await _context.Products
            .Where(p => p.ProductOwnerId == query.ProductOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (ownerProductIds.Count == 0)
        {
            _logger.LogWarning("No products found for ProductOwner {ProductOwnerId}", query.ProductOwnerId);
            return EmptyResult(query.SprintIds.Count);
        }

        // Load projections for all requested sprints, scoped to this product owner's products
        var sprintIdList = query.SprintIds.Distinct().ToList();
        var projections = await _context.SprintMetricsProjections
            .AsNoTracking()
            .Where(p => sprintIdList.Contains(p.SprintId) && ownerProductIds.Contains(p.ProductId))
            .ToListAsync(cancellationToken);

        if (projections.Count == 0)
        {
            return EmptyResult(query.SprintIds.Count);
        }

        // Load product names
        var productIds = projections.Select(p => p.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        // Load feature progress for the full sprint range (start of earliest sprint → end of latest)
        var sprints = await _context.Sprints
            .AsNoTracking()
            .Where(s => sprintIdList.Contains(s.Id) && s.StartDateUtc != null)
            .OrderBy(s => s.StartDateUtc)
            .ToListAsync(cancellationToken);

        var sprintRangeStart = sprints.FirstOrDefault()?.StartDateUtc is { } sd
            ? DateTime.SpecifyKind(sd, DateTimeKind.Utc)
            : (DateTime?)null;
        var sprintRangeEnd = sprints.LastOrDefault()?.EndDateUtc is { } ed
            ? DateTime.SpecifyKind(ed, DateTimeKind.Utc)
            : (DateTime?)null;

        var featureProgress = await _projectionService.ComputeFeatureProgressAsync(
            query.ProductOwnerId,
            sprintRangeStart,
            sprintRangeEnd,
            cancellationToken);

        var deliverySummary = _portfolioDeliverySummaryService.BuildSummary(
            new PortfolioDeliverySummaryRequest(
                projections.Select(projection => new PortfolioDeliveryProductProjectionInput(
                    projection.ProductId,
                    products.GetValueOrDefault(projection.ProductId, "Unknown"),
                    projection.CompletedPbiCount,
                    projection.CompletedPbiStoryPoints,
                    projection.BugsCreatedCount,
                    projection.BugsWorkedCount,
                    projection.BugsClosedCount,
                    projection.ProgressionDelta))
                    .ToList(),
                featureProgress.Select(feature => new PortfolioFeatureContributionInput(
                    feature.FeatureId,
                    feature.FeatureTitle,
                    feature.EpicTitle,
                    feature.ProductId,
                    products.GetValueOrDefault(feature.ProductId, "Unknown"),
                    feature.SprintCompletedEffort,
                    feature.TotalEffort,
                    feature.ProgressPercent))
                    .ToList(),
                TopFeatureLimit));

        _logger.LogInformation(
            "Portfolio delivery snapshot for ProductOwner {ProductOwnerId}: {ProductCount} products, {FeatureCount} top features, total effort {TotalEffort}",
            query.ProductOwnerId,
            deliverySummary.ProductSummaries.Count,
            deliverySummary.FeatureContributionSummaries.Count,
            deliverySummary.TotalDeliveredStoryPoints);

        return new PortfolioDeliveryDto
        {
            Summary = new PortfolioDeliverySummaryDto
            {
                TotalCompletedPbis = deliverySummary.TotalCompletedPbis,
                TotalCompletedEffort = deliverySummary.TotalDeliveredStoryPoints,
                AverageProgressPercent = deliverySummary.AverageProgressPercent,
                TotalBugsCreated = deliverySummary.TotalBugsCreated,
                TotalBugsWorked = deliverySummary.TotalBugsWorked,
                TotalBugsClosed = deliverySummary.TotalBugsClosed
            },
            Products = deliverySummary.ProductSummaries
                .Select(summary => new ProductDeliveryDto
                {
                    ProductId = summary.ProductId,
                    ProductName = summary.ProductName,
                    CompletedPbis = summary.CompletedPbis,
                    CompletedEffort = summary.DeliveredStoryPoints,
                    EffortShare = summary.DeliveredSharePercent,
                    BugsCreated = summary.BugsCreated,
                    BugsWorked = summary.BugsWorked,
                    BugsClosed = summary.BugsClosed,
                    ProgressionDelta = summary.ProgressionDelta
                })
                .ToList(),
            TopFeatures = deliverySummary.FeatureContributionSummaries
                .Select(summary => new FeatureDeliveryDto
                {
                    FeatureId = summary.WorkItemId,
                    FeatureTitle = summary.Title,
                    EpicTitle = summary.EpicTitle,
                    ProductId = summary.ProductId,
                    ProductName = summary.ProductName,
                    SprintCompletedEffort = summary.DeliveredStoryPoints,
                    TotalEffort = summary.TotalScopeStoryPoints,
                    TotalStoryPoints = summary.TotalScopeStoryPoints,
                    EffortShare = summary.DeliveredSharePercent,
                    ProgressPercent = summary.ProgressPercent
                })
                .ToList(),
            SprintCount = query.SprintIds.Distinct().Count(),
            HasData = true
        };
    }

    private static PortfolioDeliveryDto EmptyResult(int sprintCount) =>
        new()
        {
            Summary = new PortfolioDeliverySummaryDto(),
            Products = Array.Empty<ProductDeliveryDto>(),
            TopFeatures = Array.Empty<FeatureDeliveryDto>(),
            SprintCount = sprintCount,
            HasData = false
        };
}
