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
/// Computes portfolio-level stock-and-flow metrics across a selected sprint range.
///
/// Stock metrics (level at end of each sprint):
///   - TotalScopeEffort:  sum of Effort of all resolved PBIs, excluding Removed items.
///                        Uses a current-state snapshot — the same value is applied to all
///                        sprints in the range. Historical per-sprint scope tracking is not
///                        implemented.
///   - RemainingEffort:   TotalScopeEffort − CumulativeDoneEffort.
///
/// Flow metrics (per-sprint deltas):
///   - ThroughputEffort:  CompletedPbiEffort for that sprint (outflow / deliveries).
///   - AddedEffort:       PlannedEffort for that sprint (inflow proxy — see limitation below).
///   - NetFlow:           ThroughputEffort − AddedEffort.
///                        Positive = backlog shrinking; Negative = backlog expanding.
///
/// LIMITATION — AddedEffort definition:
///   AddedEffort is proxied from SprintMetricsProjection.PlannedEffort, which is the effort
///   of items committed to the sprint backlog. It does NOT represent items newly added to the
///   product backlog during the sprint. True scope-inflow tracking requires per-event history
///   (ActivityEventLedger extension) which is not yet available.
///   Additionally, PlannedEffort includes re-estimated items; large re-estimations may distort
///   Net Flow temporarily, since creation vs. estimation deltas cannot be distinguished.
///
/// Edge cases:
///   - TotalScopeEffort = 0 → PercentDone is null (avoid divide-by-zero).
///   - Sprint with no projection rows → HasData = false, all fields null.
///   - Multi-product: projections are summed across all products per sprint.
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

        // Resolve product IDs for this product owner
        var allOwnerProductIds = await _context.Products
            .Where(p => p.ProductOwnerId == query.ProductOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (allOwnerProductIds.Count == 0)
        {
            _logger.LogWarning("No products found for ProductOwner {ProductOwnerId}", query.ProductOwnerId);
            return EmptyResult();
        }

        // If a product filter is specified, intersect with the owner's products to prevent
        // cross-owner data leakage. If no filter, use all owner products (Default = All Products).
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

        // Load sprints for the requested IDs, ordered by start date
        var sprintIdList = query.SprintIds.Distinct().ToList();
        var sprints = await _context.Sprints
            .Where(s => sprintIdList.Contains(s.Id))
            .OrderBy(s => s.StartDateUtc)
            .ToListAsync(cancellationToken);

        if (sprints.Count == 0)
        {
            return EmptyResult();
        }

        // Compute total scope from current work item state:
        // Sum effort of all resolved PBIs (type "Product Backlog Item" or "Bug" that acts as PBI)
        // for these products, excluding items in a "Removed" state.
        // Note: we use the current snapshot as the baseline for scope.
        var resolvedPbiIds = await _context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId != null && productIds.Contains(r.ResolvedProductId.Value)
                && (r.WorkItemType == "Product Backlog Item" || r.WorkItemType == "Bug"))
            .Select(r => r.WorkItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var pbiEffortSum = await _context.WorkItems
            .Where(w => resolvedPbiIds.Contains(w.TfsId)
                && !w.State.Contains("Removed"))
            .SumAsync(w => (double?)(w.Effort ?? 0), cancellationToken) ?? 0.0;

        var totalScopeEffort = pbiEffortSum;

        _logger.LogDebug(
            "Total scope effort for ProductOwner {ProductOwnerId}: {TotalScope} pts across {PbiCount} PBIs",
            query.ProductOwnerId, totalScopeEffort, resolvedPbiIds.Count);

        // Load sprint projections for all requested sprints and products
        var projections = await _context.SprintMetricsProjections
            .Where(p => sprintIdList.Contains(p.SprintId) && productIds.Contains(p.ProductId))
            .ToListAsync(cancellationToken);

        // Group projections by sprint, sum CompletedPbiEffort and PlannedEffort across all products
        var throughputBySprint = projections
            .GroupBy(p => p.SprintId)
            .ToDictionary(
                g => g.Key,
                g => (double)g.Sum(p => p.CompletedPbiEffort));

        // AddedEffort proxy: sum PlannedEffort per sprint.
        // See class-level limitation comment on the AddedEffort definition.
        var addedBySprint = projections
            .GroupBy(p => p.SprintId)
            .ToDictionary(
                g => g.Key,
                g => (double)g.Sum(p => p.PlannedEffort));

        // Build per-sprint data points with cumulative done effort
        double cumulativeDone = 0.0;
        var sprintPoints = new List<PortfolioSprintProgressDto>();

        foreach (var sprint in sprints)
        {
            var hasData = throughputBySprint.TryGetValue(sprint.Id, out var throughput);
            addedBySprint.TryGetValue(sprint.Id, out var addedEffort);

            if (hasData)
            {
                cumulativeDone += throughput;
            }

            // PercentDone and RemainingEffort are null when no scope data exists
            double? percentDone = null;
            double? remaining = null;

            if (totalScopeEffort > 0)
            {
                // Cap cumulative done at total scope (completed effort cannot exceed scope)
                var effectiveDone = Math.Min(cumulativeDone, totalScopeEffort);
                percentDone = effectiveDone / totalScopeEffort * 100.0;
                remaining = totalScopeEffort - effectiveDone;
            }

            // NetFlow = Throughput − Added (positive = backlog shrinking, negative = expanding)
            double? netFlow = hasData ? throughput - addedEffort : null;

            sprintPoints.Add(new PortfolioSprintProgressDto
            {
                SprintId = sprint.Id,
                SprintName = sprint.Name,
                StartUtc = sprint.StartUtc,
                EndUtc = sprint.EndUtc,
                PercentDone = hasData ? percentDone : null,
                TotalScopeEffort = hasData ? totalScopeEffort : null,
                RemainingEffort = hasData ? remaining : null,
                ThroughputEffort = hasData ? throughput : null,
                AddedEffort = hasData ? addedEffort : null,
                NetFlow = netFlow,
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
    ///   Contracting — cumulative Net Flow &gt; +tolerance (delivering more than adding).
    ///                 Backlog is shrinking.
    ///   Expanding   — cumulative Net Flow &lt; −tolerance (adding more than delivering).
    ///                 Backlog is growing.
    ///   Stable      — |cumulative Net Flow| ≤ tolerance (small movement within threshold).
    ///
    /// Tolerance: 5 story points (absolute), chosen to ignore minor rounding differences.
    ///
    /// TotalScopeChangePts: always 0 in the current data model because TotalScopeEffort is a
    /// current-state snapshot applied uniformly to all sprints in the range. Historical
    /// per-sprint scope tracking is not yet implemented.
    /// </summary>
    private static PortfolioProgressSummaryDto ComputeSummary(
        IReadOnlyList<PortfolioSprintProgressDto> sprints)
    {
        const double tolerance = 5.0;

        var withData = sprints.Where(s => s.HasData).ToList();

        if (withData.Count == 0)
        {
            return new PortfolioProgressSummaryDto
            {
                Trajectory = PortfolioTrajectory.Stable
            };
        }

        var first = withData.First();
        var last  = withData.Last();

        // Cumulative Net Flow across the range
        var cumulativeNet = withData.Sum(s => s.NetFlow ?? 0.0);

        // Total scope change: snapshot model means first == last, so always 0
        var totalScopeChangePts = (first.TotalScopeEffort.HasValue && last.TotalScopeEffort.HasValue)
            ? last.TotalScopeEffort.Value - first.TotalScopeEffort.Value
            : (double?)null;

        var totalScopeChangePercent =
            totalScopeChangePts.HasValue && first.TotalScopeEffort.HasValue && first.TotalScopeEffort.Value > 0
            ? totalScopeChangePts.Value / first.TotalScopeEffort.Value * 100.0
            : (double?)null;

        // Remaining effort change (negative = good)
        var remainingChangePts = (first.RemainingEffort.HasValue && last.RemainingEffort.HasValue)
            ? last.RemainingEffort.Value - first.RemainingEffort.Value
            : (double?)null;

        // Classification based on cumulative Net Flow
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
            CumulativeNetFlow = withData.Any(s => s.NetFlow.HasValue) ? cumulativeNet : null,
            TotalScopeChangePts = totalScopeChangePts,
            TotalScopeChangePercent = totalScopeChangePercent,
            RemainingEffortChangePts = remainingChangePts,
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
}
