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
/// Computes portfolio-level progress metrics across a selected sprint range.
///
/// Calculation approach:
///   - TotalScopeEffort:   sum of Effort of all resolved PBIs for the product owner's products,
///                         excluding items whose current State contains "Removed".
///                         This is a current-state snapshot; it represents the known baseline scope.
///   - CumulativeDoneEffort per sprint:
///                         running sum of CompletedPbiEffort from SprintMetricsProjection entities,
///                         ordered chronologically. Represents "how much effort has been completed
///                         through the end of each sprint".
///   - PercentDone:        CumulativeDoneEffort / TotalScopeEffort * 100.
///   - RemainingEffort:    TotalScopeEffort - CumulativeDoneEffort.
///   - ThroughputEffort:   CompletedPbiEffort for that sprint only (per-sprint throughput).
///
/// Edge cases:
///   - If TotalScopeEffort is 0 → PercentDone is null (avoid divide-by-zero).
///   - If a sprint has no projection rows → HasData = false, all values null.
///   - Multi-product: projections across all products are summed per sprint.
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

        // Group projections by sprint, sum CompletedPbiEffort across all products
        var throughputBySprint = projections
            .GroupBy(p => p.SprintId)
            .ToDictionary(
                g => g.Key,
                g => (double)g.Sum(p => p.CompletedPbiEffort));

        // Build per-sprint data points with cumulative done effort
        double cumulativeDone = 0.0;
        var sprintPoints = new List<PortfolioSprintProgressDto>();

        foreach (var sprint in sprints)
        {
            var hasData = throughputBySprint.TryGetValue(sprint.Id, out var throughput);

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
    /// Computes the top-level summary for the selected sprint range.
    ///
    /// Classification logic:
    ///   Improving — % done is rising (by ≥5 pts) AND remaining effort is falling (by ≥5%).
    ///   AtRisk    — remaining effort is increasing OR % done has dropped.
    ///   Stable    — all other cases (small movement within tolerance).
    /// </summary>
    private static PortfolioProgressSummaryDto ComputeSummary(
        IReadOnlyList<PortfolioSprintProgressDto> sprints)
    {
        var withData = sprints.Where(s => s.HasData).ToList();

        if (withData.Count < 2)
        {
            return new PortfolioProgressSummaryDto
            {
                FirstPercentDone = withData.FirstOrDefault()?.PercentDone,
                LastPercentDone = withData.LastOrDefault()?.PercentDone,
                ScopeChangePercent = null,
                RemainingEffortDelta = null,
                Trajectory = PortfolioTrajectory.Stable
            };
        }

        var first = withData.First();
        var last = withData.Last();

        var firstPct = first.PercentDone;
        var lastPct = last.PercentDone;
        var firstRemaining = first.RemainingEffort;
        var lastRemaining = last.RemainingEffort;

        // Scope change: TotalScopeEffort is a current-state snapshot; historical scope changes
        // are not tracked in the current data model, so this is always null.
        // TODO: Track scope additions/removals via activity events for true historical scope change.
        double? scopeChangePct = null;

        double? remainingDelta = firstRemaining.HasValue && lastRemaining.HasValue
            ? lastRemaining.Value - firstRemaining.Value
            : null;

        // Classify trajectory using a consistent 5% threshold for both directions:
        // Improving: % done rose by ≥5 pts AND remaining effort fell.
        // AtRisk:    % done dropped by ≥5 pts OR remaining effort increased by ≥5%.
        // Stable:    all other cases (movement within tolerance).
        PortfolioTrajectory trajectory;
        var pctDelta = lastPct.HasValue && firstPct.HasValue ? lastPct.Value - firstPct.Value : (double?)null;
        var remainingPctChange = firstRemaining.HasValue && firstRemaining.Value > 0 && remainingDelta.HasValue
            ? remainingDelta.Value / firstRemaining.Value * 100.0
            : (double?)null;

        if (pctDelta.HasValue && pctDelta >= 5.0
            && remainingDelta.HasValue && remainingDelta < 0)
        {
            trajectory = PortfolioTrajectory.Improving;
        }
        else if ((pctDelta.HasValue && pctDelta <= -5.0)
            || (remainingPctChange.HasValue && remainingPctChange >= 5.0))
        {
            trajectory = PortfolioTrajectory.AtRisk;
        }
        else
        {
            trajectory = PortfolioTrajectory.Stable;
        }

        return new PortfolioProgressSummaryDto
        {
            FirstPercentDone = firstPct,
            LastPercentDone = lastPct,
            ScopeChangePercent = scopeChangePct,
            RemainingEffortDelta = remainingDelta,
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
