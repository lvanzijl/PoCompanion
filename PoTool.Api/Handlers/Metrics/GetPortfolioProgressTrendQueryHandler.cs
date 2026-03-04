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
///   - TotalScopeEffort:  sum of Effort of all resolved PBIs (excl. Removed) at the END of
///                        each sprint. Computed historically by replaying effort change events
///                        from the ActivityEventLedger (see ComputeHistoricalScopeEffort).
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
/// Historical scope reconstruction accuracy:
///   The ActivityEventLedger only contains events captured since the ledger was first seeded
///   for this product owner. Sprints predating the first ingest may show the current effort
///   value (no events to undo). Accuracy improves over time as more events accumulate.
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

        // Load resolved PBI IDs for these products (PBIs + Bugs used as PBIs)
        var resolvedPbiIds = await _context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId != null && productIds.Contains(r.ResolvedProductId.Value)
                && (r.WorkItemType == "Product Backlog Item" || r.WorkItemType == "Bug"))
            .Select(r => r.WorkItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Load current work item details needed for historical scope reconstruction.
        // We need Effort (current value), State (to detect Removed items), and CreatedDate
        // (to exclude items that didn't yet exist at a given sprint end).
        var pbiDetails = await _context.WorkItems
            .Where(w => resolvedPbiIds.Contains(w.TfsId))
            .Select(w => new PbiSnapshot { TfsId = w.TfsId, Effort = w.Effort, State = w.State, CreatedDate = w.CreatedDate })
            .ToListAsync(cancellationToken);
        var pbiDetailsById = pbiDetails.ToDictionary(w => w.TfsId);

        // Load all effort and state change events for these PBIs from the ActivityEventLedger.
        // We load the FULL history (not clamped to the sprint range) so we can reconstruct
        // historical scope at any sprint's end date by undoing future changes.
        // Field refs: "Microsoft.VSTS.Scheduling.Effort" and "System.State"
        var scopeChangeEvents = await _context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(e => e.ProductOwnerId == query.ProductOwnerId
                && resolvedPbiIds.Contains(e.WorkItemId)
                && (e.FieldRefName == EffortFieldRef || e.FieldRefName == StateFieldRef))
            .Select(e => new ScopeEvent
            {
                WorkItemId = e.WorkItemId,
                FieldRefName = e.FieldRefName,
                EventTimestamp = e.EventTimestamp,
                OldValue = e.OldValue,
                NewValue = e.NewValue
            })
            .ToListAsync(cancellationToken);

        var effortEventsByItem = scopeChangeEvents
            .Where(e => e.FieldRefName == EffortFieldRef)
            .GroupBy(e => e.WorkItemId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.EventTimestamp).ToList());

        var stateEventsByItem = scopeChangeEvents
            .Where(e => e.FieldRefName == StateFieldRef)
            .GroupBy(e => e.WorkItemId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.EventTimestamp).ToList());

        _logger.LogDebug(
            "Loaded {PbiCount} PBIs and {EventCount} scope history events for ProductOwner {ProductOwnerId}",
            pbiDetails.Count, scopeChangeEvents.Count, query.ProductOwnerId);

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
            var hasData  = throughputBySprint.TryGetValue(sprint.Id, out var throughput);
            // addedBySprint is derived from the same projections as throughputBySprint,
            // so hasAdded == hasData in practice. If a sprint has throughput data but
            // no planned effort (all items had 0 effort), addedEffort defaults to 0.0
            // which is correct (no committed work = pure backlog reduction).
            var hasAdded = addedBySprint.TryGetValue(sprint.Id, out var addedEffort);

            if (hasData)
            {
                cumulativeDone += throughput;
            }

            // Compute historical total scope at the end of this sprint.
            // Uses activity event replay to reconstruct the backlog state as it was then.
            var sprintEndUtc = sprint.EndDateUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(sprint.EndDateUtc.Value, DateTimeKind.Utc), TimeSpan.Zero)
                : DateTimeOffset.UtcNow;

            var sprintScopeEffort = ComputeHistoricalScopeEffort(
                sprintEndUtc, resolvedPbiIds, pbiDetailsById, effortEventsByItem, stateEventsByItem);

            // PercentDone and RemainingEffort are null when no scope data exists
            double? percentDone = null;
            double? remaining = null;

            if (sprintScopeEffort > 0)
            {
                // Cap cumulative done at total scope (completed effort cannot exceed scope)
                var effectiveDone = Math.Min(cumulativeDone, sprintScopeEffort);
                percentDone = effectiveDone / sprintScopeEffort * 100.0;
                remaining = sprintScopeEffort - effectiveDone;
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
                TotalScopeEffort = hasData ? sprintScopeEffort : null,
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
    /// Single-sprint ranges: cumulative Net Flow equals that sprint's Net Flow, which is
    /// meaningful (a single sprint where Net &gt; 5 is genuinely Contracting). No special
    /// handling is required.
    ///
    /// TotalScopeChangePts: reflects actual scope change between the first and last sprint in
    /// the range, computed using the historical scope reconstruction algorithm.
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

        // Total scope change: now uses historically-reconstructed per-sprint TotalScopeEffort
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

    /// <summary>
    /// Reconstructs the total portfolio scope effort at the END of the given sprint.
    ///
    /// Algorithm:
    ///   1. For each resolved PBI, start from its CURRENT effort value.
    ///   2. Undo all effort change events that occurred AFTER <paramref name="sprintEndUtc"/> by
    ///      subtracting (NewValue − OldValue) for each future event.
    ///      Formula:  historicalEffort = currentEffort + Σ(oldVal − newVal) for events after T
    ///   3. Exclude items that did not yet exist at <paramref name="sprintEndUtc"/> (CreatedDate &gt; T).
    ///   4. For items currently in a "Removed" state: check whether they were already in
    ///      "Removed" at <paramref name="sprintEndUtc"/>. If yes, exclude them. If not (they
    ///      were removed later), include them.
    ///
    /// Accuracy note:
    ///   If no activity events are available for a PBI (e.g. the ledger was seeded after the
    ///   sprint ended), the current effort is used as the historical value. This means older
    ///   sprints may over- or under-count scope if estimates changed significantly. The accuracy
    ///   improves as the ledger accumulates more history.
    /// </summary>
    internal static double ComputeHistoricalScopeEffort(
        DateTimeOffset sprintEndUtc,
        IReadOnlyList<int> resolvedPbiIds,
        IReadOnlyDictionary<int, PbiSnapshot> pbiDetailsById,
        IReadOnlyDictionary<int, List<ScopeEvent>> effortEventsByItem,
        IReadOnlyDictionary<int, List<ScopeEvent>> stateEventsByItem)
    {
        double totalScope = 0.0;

        foreach (var pbiId in resolvedPbiIds)
        {
            if (!pbiDetailsById.TryGetValue(pbiId, out var wi))
                continue;

            // 1. Exclude items that didn't exist at sprint end
            if (wi.CreatedDate.HasValue && wi.CreatedDate.Value > sprintEndUtc)
                continue;

            // 2. Handle items currently in a "Removed" state
            if (wi.State.Contains("Removed", StringComparison.OrdinalIgnoreCase))
            {
                // Check whether the item was already in "Removed" state at or before sprint end.
                // If ANY state-change event to "Removed" occurred on or before sprintEndUtc,
                // the item was out-of-scope at sprint end — exclude it.
                var stateEvts = stateEventsByItem.GetValueOrDefault(pbiId);
                var wasRemovedAtSprintEnd = stateEvts?.Any(e =>
                    e.NewValue != null
                    && e.NewValue.Contains("Removed", StringComparison.OrdinalIgnoreCase)
                    && e.EventTimestamp <= sprintEndUtc) == true;

                if (wasRemovedAtSprintEnd)
                    continue; // Was already removed at sprint end; exclude from scope

                // Item was removed AFTER sprint end → include it in historical scope
            }

            // 3. Reconstruct effort at sprint end by undoing future changes
            double historicalEffort = wi.Effort ?? 0.0;

            var effortEvts = effortEventsByItem.GetValueOrDefault(pbiId);
            if (effortEvts != null)
            {
                foreach (var evt in effortEvts.Where(e => e.EventTimestamp > sprintEndUtc))
                {
                    var oldVal = ParseEffortValue(evt.OldValue);
                    var newVal = ParseEffortValue(evt.NewValue);
                    // Undo future change: restore the value the item had before this event
                    historicalEffort += oldVal - newVal;
                }
            }

            // Clamp to 0 — effort should never be negative
            totalScope += Math.Max(0.0, historicalEffort);
        }

        return totalScope;
    }

    /// <summary>
    /// Parses an effort value string from the activity ledger.
    /// Returns 0.0 for null, empty, or unparseable strings.
    /// </summary>
    private static double ParseEffortValue(string? value) =>
        !string.IsNullOrWhiteSpace(value) && double.TryParse(value, out var d) ? d : 0.0;

    private static PortfolioProgressTrendDto EmptyResult() =>
        new()
        {
            Sprints = Array.Empty<PortfolioSprintProgressDto>(),
            Summary = new PortfolioProgressSummaryDto
            {
                Trajectory = PortfolioTrajectory.Stable
            }
        };

    // Field reference names for the ActivityEventLedger
    private const string EffortFieldRef = "Microsoft.VSTS.Scheduling.Effort";
    private const string StateFieldRef = "System.State";

    // Private projection types for EF Core queries
    internal sealed class PbiSnapshot
    {
        public int TfsId { get; init; }
        public int? Effort { get; init; }
        public string State { get; init; } = string.Empty;
        public DateTimeOffset? CreatedDate { get; init; }
    }

    internal sealed class ScopeEvent
    {
        public int WorkItemId { get; init; }
        public string FieldRefName { get; init; } = string.Empty;
        public DateTimeOffset EventTimestamp { get; init; }
        public string? OldValue { get; init; }
        public string? NewValue { get; init; }
    }
}
