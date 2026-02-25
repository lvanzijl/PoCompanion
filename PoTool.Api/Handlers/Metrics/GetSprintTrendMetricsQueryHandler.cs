using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetSprintTrendMetricsQuery.
/// Returns planned vs worked metrics for sprints using revision-based data.
/// </summary>
public sealed class GetSprintTrendMetricsQueryHandler : IQueryHandler<GetSprintTrendMetricsQuery, GetSprintTrendMetricsResponse>
{
    private readonly PoToolDbContext _context;
    private readonly SprintTrendProjectionService _projectionService;
    private readonly ILogger<GetSprintTrendMetricsQueryHandler> _logger;

    public GetSprintTrendMetricsQueryHandler(
        PoToolDbContext context,
        SprintTrendProjectionService projectionService,
        ILogger<GetSprintTrendMetricsQueryHandler> logger)
    {
        _context = context;
        _projectionService = projectionService;
        _logger = logger;
    }

    public async ValueTask<GetSprintTrendMetricsResponse> Handle(
        GetSprintTrendMetricsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling GetSprintTrendMetricsQuery for ProductOwner {ProductOwnerId}, SprintCount {SprintCount}, Recompute {Recompute}",
            query.ProductOwnerId, query.SprintIds.Count, query.Recompute);

        try
        {
            IReadOnlyList<SprintMetricsProjectionEntity> projections;

            if (query.Recompute)
            {
                _logger.LogInformation(
                    "Recompute requested for ProductOwner {ProductOwnerId}. Computing projections for requested sprint range.",
                    query.ProductOwnerId);

                projections = await _projectionService.ComputeProjectionsAsync(
                    query.ProductOwnerId,
                    query.SprintIds,
                    cancellationToken);

                _logger.LogInformation(
                    "Computed {ProjectionCount} sprint trend projections for ProductOwner {ProductOwnerId} during recompute request.",
                    projections.Count, query.ProductOwnerId);
            }
            else
            {
                projections = await _projectionService.GetProjectionsAsync(
                    query.ProductOwnerId,
                    query.SprintIds,
                    cancellationToken);

                _logger.LogInformation(
                    "Retrieved {ProjectionCount} cached sprint trend projections for ProductOwner {ProductOwnerId}.",
                    projections.Count, query.ProductOwnerId);

                if (projections.Count == 0)
                {
                    _logger.LogWarning(
                        "No cached sprint trend projections found for ProductOwner {ProductOwnerId}. Triggering projection computation for requested sprint range.",
                        query.ProductOwnerId);

                    projections = await _projectionService.ComputeProjectionsAsync(
                        query.ProductOwnerId,
                        query.SprintIds,
                        cancellationToken);

                    _logger.LogInformation(
                        "Computed {ProjectionCount} sprint trend projections for ProductOwner {ProductOwnerId} after cache miss.",
                        projections.Count, query.ProductOwnerId);
                }
            }

            // Get sprints for additional info
            var sprints = await _context.Sprints
                .Where(s => query.SprintIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s, cancellationToken);

            // Get products for names
            var productIds = projections.Select(p => p.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);

            // Group by sprint
            var metricsBySprint = projections
                .GroupBy(p => p.SprintId)
                .Select(g =>
                {
                    var sprintId = g.Key;
                    var sprint = sprints.GetValueOrDefault(sprintId);

                    var productMetrics = g.Select(p => new ProductSprintMetricsDto
                    {
                        ProductId = p.ProductId,
                        ProductName = products.GetValueOrDefault(p.ProductId)?.Name ?? "Unknown",
                        PlannedCount = p.PlannedCount,
                        PlannedEffort = p.PlannedEffort,
                        WorkedCount = p.WorkedCount,
                        WorkedEffort = p.WorkedEffort,
                        BugsPlannedCount = p.BugsPlannedCount,
                        BugsWorkedCount = p.BugsWorkedCount,
                        CompletedPbiCount = p.CompletedPbiCount,
                        CompletedPbiEffort = p.CompletedPbiEffort,
                        ProgressionDelta = p.ProgressionDelta,
                        BugsCreatedCount = p.BugsCreatedCount,
                        BugsClosedCount = p.BugsClosedCount,
                        MissingEffortCount = p.MissingEffortCount,
                        IsApproximate = p.IsApproximate
                    }).ToList();

                    return new SprintTrendMetricsDto
                    {
                        SprintId = sprintId,
                        SprintName = sprint?.Name ?? "Unknown",
                        StartUtc = sprint?.StartUtc,
                        EndUtc = sprint?.EndUtc,
                        ProductMetrics = productMetrics,
                        TotalPlannedCount = g.Sum(p => p.PlannedCount),
                        TotalPlannedEffort = g.Sum(p => p.PlannedEffort),
                        TotalWorkedCount = g.Sum(p => p.WorkedCount),
                        TotalWorkedEffort = g.Sum(p => p.WorkedEffort),
                        TotalBugsPlannedCount = g.Sum(p => p.BugsPlannedCount),
                        TotalBugsWorkedCount = g.Sum(p => p.BugsWorkedCount),
                        TotalCompletedPbiCount = g.Sum(p => p.CompletedPbiCount),
                        TotalCompletedPbiEffort = g.Sum(p => p.CompletedPbiEffort),
                        TotalProgressionDelta = g.Sum(p => p.ProgressionDelta),
                        TotalBugsCreatedCount = g.Sum(p => p.BugsCreatedCount),
                        TotalBugsClosedCount = g.Sum(p => p.BugsClosedCount),
                        TotalMissingEffortCount = g.Sum(p => p.MissingEffortCount),
                        IsApproximate = g.Any(p => p.IsApproximate)
                    };
                })
                .OrderBy(m => m.StartUtc ?? DateTimeOffset.MaxValue)
                .ToList();

            _logger.LogInformation(
                "Returning sprint trend metrics for ProductOwner {ProductOwnerId}: {SprintMetricCount} sprint rows from {ProjectionCount} projections.",
                query.ProductOwnerId, metricsBySprint.Count, projections.Count);

            // Compute real feature progress from resolved hierarchy
            var featureProgress = await _projectionService.ComputeFeatureProgressAsync(
                query.ProductOwnerId,
                cancellationToken);

            // Compute epic progress from feature progress
            var epicProgress = await _projectionService.ComputeEpicProgressAsync(
                query.ProductOwnerId,
                featureProgress,
                cancellationToken);

            return new GetSprintTrendMetricsResponse
            {
                Success = true,
                Metrics = metricsBySprint,
                FeatureProgress = featureProgress,
                EpicProgress = epicProgress
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sprint trend metrics for ProductOwner {ProductOwnerId}", query.ProductOwnerId);
            return new GetSprintTrendMetricsResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
