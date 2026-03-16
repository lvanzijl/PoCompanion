using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
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
    private readonly ILogger<GetPortfolioProgressTrendQueryHandler> _logger;

    public GetPortfolioProgressTrendQueryHandler(
        PoToolDbContext context,
        ILogger<GetPortfolioProgressTrendQueryHandler> logger)
    {
        _context = context;
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
            .Select(p => new PortfolioFlowProjectionTotals
            {
                SprintId = p.SprintId,
                StockStoryPoints = p.StockStoryPoints,
                RemainingScopeStoryPoints = p.RemainingScopeStoryPoints,
                InflowStoryPoints = p.InflowStoryPoints,
                ThroughputStoryPoints = p.ThroughputStoryPoints
            })
            .ToListAsync(cancellationToken);

        var projectionsBySprint = projections
            .GroupBy(p => p.SprintId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sprintPoints = new List<PortfolioSprintProgressDto>();

        foreach (var sprint in sprints)
        {
            var hasData = projectionsBySprint.TryGetValue(sprint.Id, out var sprintProjectionRows);
            var stock = hasData ? sprintProjectionRows!.Sum(p => p.StockStoryPoints) : (double?)null;
            var remaining = hasData ? sprintProjectionRows!.Sum(p => p.RemainingScopeStoryPoints) : (double?)null;
            var inflow = hasData ? sprintProjectionRows!.Sum(p => p.InflowStoryPoints) : (double?)null;
            var throughput = hasData ? sprintProjectionRows!.Sum(p => p.ThroughputStoryPoints) : (double?)null;
            var completionPercent =
                hasData && stock > 0 && remaining.HasValue
                    ? (stock.Value - remaining.Value) / stock.Value * 100.0
                    : (double?)null;
            var netFlow = hasData && throughput.HasValue && inflow.HasValue
                ? throughput.Value - inflow.Value
                : (double?)null;

            sprintPoints.Add(new PortfolioSprintProgressDto
            {
                SprintId = sprint.Id,
                SprintName = sprint.Name,
                StartUtc = sprint.StartUtc,
                EndUtc = sprint.EndUtc,
                CompletionPercent = completionPercent,
                StockStoryPoints = stock,
                RemainingScopeStoryPoints = remaining,
                ThroughputStoryPoints = throughput,
                InflowStoryPoints = inflow,
                NetFlowStoryPoints = netFlow,
                HasData = hasData
            });
        }

        var summary = ComputeSummary(sprintPoints);

        _logger.LogInformation(
            "Portfolio progress trend computed for ProductOwner {ProductOwnerId}: {SprintCount} sprints, trajectory={Trajectory}",
            query.ProductOwnerId, sprintPoints.Count, summary.Trajectory);

        return new PortfolioProgressTrendDto
        {
            Sprints = sprintPoints,
            Summary = summary
        };
    }

    /// <summary>
    /// Computes the stock-and-flow summary for the selected sprint range.
    ///
    /// Classification rules:
    ///   Contracting — cumulative Net Flow &gt; +tolerance.
    ///                 Backlog is shrinking.
    ///   Expanding   — cumulative Net Flow &lt; −tolerance.
    ///                 Backlog is growing.
    ///   Stable      — |cumulative Net Flow| ≤ tolerance.
    ///
    /// Tolerance: 3 story points (absolute), chosen to ignore minor rounding differences
    /// while still preserving the representative canonical trend shape after migrating from
    /// effort proxies to story-point PortfolioFlow values.
    /// </summary>
    private static PortfolioProgressSummaryDto ComputeSummary(
        IReadOnlyList<PortfolioSprintProgressDto> sprints)
    {
        const double tolerance = 3.0;

        var withData = sprints.Where(s => s.HasData).ToList();

        if (withData.Count == 0)
        {
            return new PortfolioProgressSummaryDto
            {
                Trajectory = PortfolioTrajectory.Stable
            };
        }

        var first = withData.First();
        var last = withData.Last();

        var cumulativeNet = withData.Sum(s => s.NetFlowStoryPoints ?? 0.0);

        var totalScopeChangeStoryPoints =
            first.StockStoryPoints.HasValue && last.StockStoryPoints.HasValue
                ? last.StockStoryPoints.Value - first.StockStoryPoints.Value
                : (double?)null;

        var totalScopeChangePercent =
            totalScopeChangeStoryPoints.HasValue && first.StockStoryPoints.HasValue && first.StockStoryPoints.Value > 0
                ? totalScopeChangeStoryPoints.Value / first.StockStoryPoints.Value * 100.0
                : (double?)null;

        var remainingScopeChangeStoryPoints =
            first.RemainingScopeStoryPoints.HasValue && last.RemainingScopeStoryPoints.HasValue
                ? last.RemainingScopeStoryPoints.Value - first.RemainingScopeStoryPoints.Value
                : (double?)null;

        PortfolioTrajectory trajectory;
        if (cumulativeNet > tolerance)
        {
            trajectory = PortfolioTrajectory.Contracting;
        }
        else if (cumulativeNet < -tolerance)
        {
            trajectory = PortfolioTrajectory.Expanding;
        }
        else
        {
            trajectory = PortfolioTrajectory.Stable;
        }

        return new PortfolioProgressSummaryDto
        {
            CumulativeNetFlow = withData.Any(s => s.NetFlowStoryPoints.HasValue) ? cumulativeNet : null,
            TotalScopeChangeStoryPoints = totalScopeChangeStoryPoints,
            TotalScopeChangePercent = totalScopeChangePercent,
            RemainingScopeChangeStoryPoints = remainingScopeChangeStoryPoints,
            Trajectory = trajectory
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

    private sealed class PortfolioFlowProjectionTotals
    {
        public int SprintId { get; init; }
        public double StockStoryPoints { get; init; }
        public double RemainingScopeStoryPoints { get; init; }
        public double InflowStoryPoints { get; init; }
        public double ThroughputStoryPoints { get; init; }
    }
}
