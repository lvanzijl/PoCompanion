using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Forecasting.Services;
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
    private readonly IVelocityCalibrationService _velocityCalibrationService;
    private readonly ILogger<GetCapacityCalibrationQueryHandler> _logger;

    public GetCapacityCalibrationQueryHandler(
        PoToolDbContext context,
        IVelocityCalibrationService velocityCalibrationService,
        ILogger<GetCapacityCalibrationQueryHandler> logger)
    {
        _context = context;
        _velocityCalibrationService = velocityCalibrationService;
        _logger = logger;
    }

    public async ValueTask<CapacityCalibrationDto> Handle(
        GetCapacityCalibrationQuery query,
        CancellationToken cancellationToken)
    {
        var effectiveProductIds = query.EffectiveFilter.Context.ProductIds.Values
            .Distinct()
            .ToList();
        var effectiveSprintIds = query.EffectiveFilter.SprintIds
            .Distinct()
            .ToList();

        _logger.LogInformation(
            "Handling GetCapacityCalibrationQuery for {ProductCount} products across {SprintCount} sprints",
            effectiveProductIds.Count,
            effectiveSprintIds.Count);

        if (effectiveProductIds.Count == 0 || effectiveSprintIds.Count == 0)
        {
            return Empty();
        }
        var sprints = await _context.Sprints
            .Where(s => effectiveSprintIds.Contains(s.Id))
            .OrderBy(s => s.StartDateUtc)
            .ToListAsync(cancellationToken);

        if (sprints.Count == 0)
        {
            return Empty();
        }

        var projections = await _context.SprintMetricsProjections
            .Where(p => effectiveSprintIds.Contains(p.SprintId) && effectiveProductIds.Contains(p.ProductId))
            .ToListAsync(cancellationToken);

        var projectionsBySprintId = projections
            .GroupBy(projection => projection.SprintId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var calibration = _velocityCalibrationService.Calibrate(
            sprints
                .Select(sprint =>
                {
                    var sprintProjections = projectionsBySprintId.GetValueOrDefault(sprint.Id) ?? [];
                    return new VelocityCalibrationSample(
                        sprint.Name,
                        sprintProjections.Sum(static projection => projection.PlannedStoryPoints),
                        sprintProjections.Sum(static projection => projection.DerivedStoryPoints),
                        sprintProjections.Sum(static projection => projection.CompletedPbiStoryPoints),
                        sprintProjections.Sum(static projection => projection.CompletedPbiEffort));
                })
                .ToList());

        _logger.LogInformation(
            "Capacity calibration computed: {Sprints} sprints, median={Median}, P25={P25}, P75={P75}, predictability={Pred:P1}",
            calibration.Entries.Count, calibration.MedianVelocity, calibration.P25Velocity, calibration.P75Velocity, calibration.MedianPredictability);

        return new CapacityCalibrationDto(
            Sprints: calibration.Entries
                .Select(entry => new SprintCalibrationEntry(
                    entry.SprintName,
                    entry.CommittedStoryPoints,
                    entry.DeliveredStoryPoints,
                    entry.DeliveredEffort,
                    entry.HoursPerStoryPoint,
                    entry.PredictabilityRatio))
                .ToList(),
            MedianVelocity: calibration.MedianVelocity,
            P25Velocity: calibration.P25Velocity,
            P75Velocity: calibration.P75Velocity,
            MedianPredictability: calibration.MedianPredictability,
            OutlierSprintNames: calibration.OutlierSprintNames);
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
