using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Core.Domain.Portfolio;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetPortfolioProgressTrendQuery.
///
/// Computes portfolio-level stock-and-flow metrics across a selected sprint range by aggregating
/// canonical PortfolioFlow projection rows.
///
/// Canonical metrics (story points):
///   - StockStoryPoints
///   - RemainingScopeStoryPoints
///   - InflowStoryPoints
///   - ThroughputStoryPoints
///   - CompletionPercent
///
/// Compatibility aliases on the DTO continue to exist temporarily, but they now map to the
/// canonical story-point projection rather than legacy effort-based proxies.
/// </summary>
public sealed class GetPortfolioProgressTrendQueryHandler
    : IQueryHandler<GetPortfolioProgressTrendQuery, PortfolioProgressTrendDto>
{
    private readonly PoToolDbContext _context;
    private readonly IPortfolioFlowSummaryService _portfolioFlowSummaryService;
    private readonly ILogger<GetPortfolioProgressTrendQueryHandler> _logger;

    public GetPortfolioProgressTrendQueryHandler(
        PoToolDbContext context,
        IPortfolioFlowSummaryService portfolioFlowSummaryService,
        ILogger<GetPortfolioProgressTrendQueryHandler> logger)
    {
        _context = context;
        _portfolioFlowSummaryService = portfolioFlowSummaryService;
        _logger = logger;
    }

    public async ValueTask<PortfolioProgressTrendDto> Handle(
        GetPortfolioProgressTrendQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling GetPortfolioProgressTrendQuery for ProductOwner {ProductOwnerId}, {SprintCount} sprints",
            query.ProductOwnerId, query.SprintIds.Count);

        var allOwnerProductIds = await _context.Products
            .Where(p => p.ProductOwnerId == query.ProductOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (allOwnerProductIds.Count == 0)
        {
            _logger.LogWarning("No products found for ProductOwner {ProductOwnerId}", query.ProductOwnerId);
            return EmptyResult();
        }

        List<int> productIds;
        if (query.ProductIds is { Length: > 0 })
        {
            productIds = allOwnerProductIds
                .Intersect(query.ProductIds)
                .ToList();

            if (productIds.Count == 0)
            {
                _logger.LogWarning(
                    "Requested product IDs {RequestedIds} do not belong to ProductOwner {ProductOwnerId}",
                    string.Join(", ", query.ProductIds), query.ProductOwnerId);
                return EmptyResult();
            }
        }
        else
        {
            productIds = allOwnerProductIds;
        }

        var sprintIdList = query.SprintIds.Distinct().ToList();
        var sprints = await _context.Sprints
            .Where(s => sprintIdList.Contains(s.Id))
            .OrderBy(s => s.StartDateUtc)
            .ToListAsync(cancellationToken);

        if (sprints.Count == 0)
        {
            return EmptyResult();
        }

        var projections = await _context.PortfolioFlowProjections
            .AsNoTracking()
            .Where(p => sprintIdList.Contains(p.SprintId) && productIds.Contains(p.ProductId))
            .Select(p => new PortfolioFlowProjectionInput(
                p.SprintId,
                p.ProductId,
                p.StockStoryPoints,
                p.RemainingScopeStoryPoints,
                p.InflowStoryPoints,
                p.ThroughputStoryPoints))
            .ToListAsync(cancellationToken);

        var trend = _portfolioFlowSummaryService.BuildTrend(
            new PortfolioFlowTrendRequest(
                sprints.Select(sprint => new PortfolioFlowSprintInfo(
                    sprint.Id,
                    sprint.Name,
                    sprint.StartUtc,
                    sprint.EndUtc))
                    .ToList(),
                projections));

        _logger.LogInformation(
            "Portfolio progress trend computed for ProductOwner {ProductOwnerId}: {SprintCount} sprints, trajectory={Trajectory}",
            query.ProductOwnerId, trend.Sprints.Count, trend.Summary.Trajectory);

        return new PortfolioProgressTrendDto
        {
            Sprints = sprints
                .Zip(
                    trend.Sprints,
                    (sprint, summary) => new PortfolioSprintProgressDto
                    {
                        SprintId = sprint.Id,
                        SprintName = sprint.Name,
                        StartUtc = sprint.StartUtc,
                        EndUtc = sprint.EndUtc,
                        CompletionPercent = summary.CompletionPercent,
                        StockStoryPoints = summary.StockStoryPoints,
                        RemainingScopeStoryPoints = summary.RemainingScopeStoryPoints,
                        ThroughputStoryPoints = summary.ThroughputStoryPoints,
                        InflowStoryPoints = summary.InflowStoryPoints,
                        NetFlowStoryPoints = summary.NetFlowStoryPoints,
                        HasData = summary.HasData
                    })
                .ToList(),
            Summary = new PortfolioProgressSummaryDto
            {
                CumulativeNetFlow = trend.Summary.CumulativeNetFlowStoryPoints,
                TotalScopeChangeStoryPoints = trend.Summary.TotalScopeChangeStoryPoints,
                TotalScopeChangePercent = trend.Summary.TotalScopeChangePercent,
                RemainingScopeChangeStoryPoints = trend.Summary.RemainingScopeChangeStoryPoints,
                Trajectory = trend.Summary.Trajectory
            }
        };
    }

    private static PortfolioProgressTrendDto EmptyResult() =>
        new()
        {
            Sprints = Array.Empty<PortfolioSprintProgressDto>(),
            Summary = new PortfolioProgressSummaryDto
            {
                Trajectory = PortfolioTrajectory.Stable
            }
        };

}
