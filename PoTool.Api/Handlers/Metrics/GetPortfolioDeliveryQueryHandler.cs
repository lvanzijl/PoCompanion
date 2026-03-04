using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
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
    private readonly ILogger<GetPortfolioDeliveryQueryHandler> _logger;

    public GetPortfolioDeliveryQueryHandler(
        PoToolDbContext context,
        SprintTrendProjectionService projectionService,
        ILogger<GetPortfolioDeliveryQueryHandler> logger)
    {
        _context = context;
        _projectionService = projectionService;
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

        // Aggregate per product across all sprints
        var byProduct = projections
            .GroupBy(p => p.ProductId)
            .Select(g => new ProductDeliveryDto
            {
                ProductId = g.Key,
                ProductName = products.GetValueOrDefault(g.Key, "Unknown"),
                CompletedPbis = g.Sum(p => p.CompletedPbiCount),
                CompletedEffort = g.Sum(p => p.CompletedPbiEffort),
                BugsCreated = g.Sum(p => p.BugsCreatedCount),
                BugsWorked = g.Sum(p => p.BugsWorkedCount),
                BugsClosed = g.Sum(p => p.BugsClosedCount),
                ProgressionDelta = g.Sum(p => p.ProgressionDelta)
            })
            .ToList();

        // Compute effort shares
        var totalEffort = byProduct.Sum(p => p.CompletedEffort);
        var productsWithShares = byProduct
            .Select(p => p with
            {
                EffortShare = totalEffort > 0 ? p.CompletedEffort / (double)totalEffort * 100.0 : 0.0
            })
            .OrderByDescending(p => p.CompletedEffort)
            .ToList();

        // Build summary metrics
        var summary = new PortfolioDeliverySummaryDto
        {
            TotalCompletedPbis = byProduct.Sum(p => p.CompletedPbis),
            TotalCompletedEffort = totalEffort,
            AverageProgressPercent = byProduct.Count > 0
                ? byProduct.Sum(p => p.ProgressionDelta) / byProduct.Count
                : 0.0,
            TotalBugsCreated = byProduct.Sum(p => p.BugsCreated),
            TotalBugsWorked = byProduct.Sum(p => p.BugsWorked),
            TotalBugsClosed = byProduct.Sum(p => p.BugsClosed)
        };

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

        // Build top feature contributors ordered by delivered effort in the range
        var topFeatures = featureProgress
            .Where(f => f.SprintCompletedEffort > 0)
            .OrderByDescending(f => f.SprintCompletedEffort)
            .Take(TopFeatureLimit)
            .Select(f => new FeatureDeliveryDto
            {
                FeatureId = f.FeatureId,
                FeatureTitle = f.FeatureTitle,
                EpicTitle = f.EpicTitle,
                ProductId = f.ProductId,
                ProductName = products.GetValueOrDefault(f.ProductId, "Unknown"),
                SprintCompletedEffort = f.SprintCompletedEffort,
                TotalEffort = f.TotalEffort,
                EffortShare = totalEffort > 0
                    ? f.SprintCompletedEffort / (double)totalEffort * 100.0
                    : 0.0,
                ProgressPercent = f.ProgressPercent
            })
            .ToList();

        _logger.LogInformation(
            "Portfolio delivery snapshot for ProductOwner {ProductOwnerId}: {ProductCount} products, {FeatureCount} top features, total effort {TotalEffort}",
            query.ProductOwnerId, productsWithShares.Count, topFeatures.Count, totalEffort);

        return new PortfolioDeliveryDto
        {
            Summary = summary,
            Products = productsWithShares,
            TopFeatures = topFeatures,
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
