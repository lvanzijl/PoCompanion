using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetCapacityCalibrationQuery.
///
/// Computes velocity distribution and predictability ratios across a selected sprint range.
///
/// Calculation approach:
///   Committed per sprint: sum of canonical planned PBI story points, excluding derived estimates.
///   Done per sprint:      sum of delivered PBI story points whose first Done transition occurred in sprint.
///   Velocity:             delivered story points.
///   Delivered effort:     completed PBI effort-hours, exposed only as diagnostics.
///
/// Percentile method — linear interpolation on sorted values:
///   Index = (p / 100.0) * (n − 1); value = lower + frac * (upper − lower).
///   P25/P50/P75 used for velocity band (quartiles).
///   P10/P90 used for outlier detection only.
///
/// PredictabilityRatio = Done / Committed; sprints with Committed == 0 are excluded from
/// the predictability aggregate (no plan = no commitment signal).
/// </summary>
public sealed class GetCapacityCalibrationQueryHandler
    : IQueryHandler<GetCapacityCalibrationQuery, CapacityCalibrationDto>
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<GetCapacityCalibrationQueryHandler> _logger;

    public GetCapacityCalibrationQueryHandler(
        PoToolDbContext context,
        ILogger<GetCapacityCalibrationQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async ValueTask<CapacityCalibrationDto> Handle(
        GetCapacityCalibrationQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling GetCapacityCalibrationQuery for ProductOwner {ProductOwnerId}, {SprintCount} sprints",
            query.ProductOwnerId, query.SprintIds.Count);

        // Resolve product IDs for this product owner
        var allOwnerProductIds = await _context.Products
            .Where(p => p.ProductOwnerId == query.ProductOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (allOwnerProductIds.Count == 0)
        {
            _logger.LogWarning("No products found for ProductOwner {ProductOwnerId}", query.ProductOwnerId);
            return Empty();
        }

        // Intersect with optional product filter to prevent cross-owner data leakage
        List<int> productIds;
        if (query.ProductIds is { Length: > 0 })
        {
            productIds = allOwnerProductIds.Intersect(query.ProductIds).ToList();
            if (productIds.Count == 0)
            {
                _logger.LogWarning(
                    "Requested product IDs {RequestedIds} do not belong to ProductOwner {ProductOwnerId}",
                    string.Join(", ", query.ProductIds), query.ProductOwnerId);
                return Empty();
            }
        }
        else
        {
            productIds = allOwnerProductIds;
        }

        // Load sprints for the requested IDs, ordered chronologically
        var sprintIdList = query.SprintIds.Distinct().ToList();
        var sprints = await _context.Sprints
            .Where(s => sprintIdList.Contains(s.Id))
            .OrderBy(s => s.StartDateUtc)
            .ToListAsync(cancellationToken);

        if (sprints.Count == 0)
        {
            return Empty();
        }

        // Load sprint–product projections for all requested sprints and owner products
        var projections = await _context.SprintMetricsProjections
            .Where(p => sprintIdList.Contains(p.SprintId) && productIds.Contains(p.ProductId))
            .ToListAsync(cancellationToken);

        // PlannedStoryPoints includes derived estimates for aggregation scenarios.
        // Capacity calibration must exclude derived points from committed scope.
        var committedStoryPointsBySprint = projections
            .GroupBy(p => p.SprintId)
            .ToDictionary(g => g.Key, g => g.Sum(p => Math.Max(0d, p.PlannedStoryPoints - p.DerivedStoryPoints)));

        // CompletedPbiStoryPoints already uses canonical delivery semantics:
        // PBIs only, bugs/tasks excluded, BusinessValue fallback allowed, derived estimates excluded.
        var deliveredStoryPointsBySprint = projections
            .GroupBy(p => p.SprintId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.CompletedPbiStoryPoints));

        var deliveredEffortBySprint = projections
            .GroupBy(p => p.SprintId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.CompletedPbiEffort));

        // Build per-sprint calibration entries
        var entries = new List<SprintCalibrationEntry>(sprints.Count);
        foreach (var sprint in sprints)
        {
            var committedStoryPoints = committedStoryPointsBySprint.TryGetValue(sprint.Id, out var committed) ? committed : 0d;
            var deliveredStoryPoints = deliveredStoryPointsBySprint.TryGetValue(sprint.Id, out var delivered) ? delivered : 0d;
            var deliveredEffort = deliveredEffortBySprint.TryGetValue(sprint.Id, out var effort) ? effort : 0;
            var hoursPerSp = deliveredStoryPoints > 0
                ? (double)deliveredEffort / deliveredStoryPoints
                : 0d;

            // Predictability: how much of the commitment was delivered (0 when uncommitted)
            var predictability = committedStoryPoints > 0 ? deliveredStoryPoints / committedStoryPoints : 0d;

            entries.Add(new SprintCalibrationEntry(
                SprintName: sprint.Name,
                CommittedStoryPoints: Math.Round(committedStoryPoints, 3),
                DeliveredStoryPoints: Math.Round(deliveredStoryPoints, 3),
                DeliveredEffort: deliveredEffort,
                HoursPerSP: Math.Round(hoursPerSp, 3),
                PredictabilityRatio: predictability));
        }

        if (entries.Count == 0)
        {
            return Empty();
        }

        // Velocity distribution (sorted ascending for percentile computation)
        var velocities = entries.Select(e => e.DeliveredStoryPoints).OrderBy(v => v).ToList();

        var medianVelocity = Percentile(velocities, 50);
        var p25Velocity = Percentile(velocities, 25);
        var p75Velocity = Percentile(velocities, 75);

        // Outliers: sprints whose velocity falls below P10 or above P90
        var p10 = Percentile(velocities, 10);
        var p90 = Percentile(velocities, 90);
        var outlierNames = entries
            .Where(e => e.DeliveredStoryPoints < p10 || e.DeliveredStoryPoints > p90)
            .Select(e => e.SprintName)
            .ToList();

        // Predictability aggregate: only sprints with a non-zero commitment
        var predictabilityValues = entries
            .Where(e => e.CommittedStoryPoints > 0)
            .Select(e => e.PredictabilityRatio)
            .OrderBy(v => v)
            .ToList();

        var medianPredictability = predictabilityValues.Count > 0
            ? Percentile(predictabilityValues, 50)
            : 0.0;

        _logger.LogInformation(
            "Capacity calibration computed: {Sprints} sprints, median={Median}, P25={P25}, P75={P75}, predictability={Pred:P1}",
            entries.Count, medianVelocity, p25Velocity, p75Velocity, medianPredictability);

        return new CapacityCalibrationDto(
            Sprints: entries.AsReadOnly(),
            MedianVelocity: Math.Round(medianVelocity, 1),
            P25Velocity: Math.Round(p25Velocity, 1),
            P75Velocity: Math.Round(p75Velocity, 1),
            MedianPredictability: Math.Round(medianPredictability, 3),
            OutlierSprintNames: outlierNames.AsReadOnly());
    }

    /// <summary>
    /// Computes the p-th percentile of a pre-sorted ascending list using linear interpolation.
    /// Returns 0.0 for empty lists.
    /// </summary>
    private static double Percentile(IList<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0.0;
        if (sorted.Count == 1) return sorted[0];

        var index = (p / 100.0) * (sorted.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        var frac = index - lower;

        return sorted[lower] + frac * (sorted[upper] - sorted[lower]);
    }

    private static CapacityCalibrationDto Empty() =>
        new(
            Sprints: Array.Empty<SprintCalibrationEntry>(),
            MedianVelocity: 0,
            P25Velocity: 0,
            P75Velocity: 0,
            MedianPredictability: 0,
            OutlierSprintNames: Array.Empty<string>());
}
