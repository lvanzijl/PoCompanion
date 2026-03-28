using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
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
    private readonly IProductAggregationService _productAggregationService;
    private readonly IPlanningQualityService _planningQualityService;
    private readonly ISnapshotComparisonService _snapshotComparisonService;
    private readonly IInsightService _insightService;
    private readonly ILogger<GetSprintTrendMetricsQueryHandler> _logger;

    public GetSprintTrendMetricsQueryHandler(
        PoToolDbContext context,
        SprintTrendProjectionService projectionService,
        IProductAggregationService productAggregationService,
        IPlanningQualityService planningQualityService,
        ISnapshotComparisonService snapshotComparisonService,
        IInsightService insightService,
        ILogger<GetSprintTrendMetricsQueryHandler> logger)
    {
        _context = context;
        _projectionService = projectionService;
        _productAggregationService = productAggregationService;
        _planningQualityService = planningQualityService;
        _snapshotComparisonService = snapshotComparisonService;
        _insightService = insightService;
        _logger = logger;
    }

    public async ValueTask<GetSprintTrendMetricsResponse> Handle(
        GetSprintTrendMetricsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling GetSprintTrendMetricsQuery for ProductOwner {ProductOwnerId}, SprintCount {SprintCount}, Recompute {Recompute}",
            query.ProductOwnerId, query.EffectiveFilter.SprintIds.Count, query.Recompute);

        try
        {
            var scopedProductIds = query.EffectiveFilter.Context.ProductIds.IsAll
                ? null
                : query.EffectiveFilter.Context.ProductIds.Values;
            IReadOnlyList<SprintMetricsProjectionEntity> projections;

            if (query.Recompute)
            {
                _logger.LogInformation(
                    "Recompute requested for ProductOwner {ProductOwnerId}. Computing projections for requested sprint range.",
                    query.ProductOwnerId);

                projections = scopedProductIds is null
                    ? await _projectionService.ComputeProjectionsAsync(
                        query.ProductOwnerId,
                        query.EffectiveFilter.SprintIds,
                        cancellationToken)
                    : await _projectionService.ComputeProjectionsAsync(
                        query.ProductOwnerId,
                        query.EffectiveFilter.SprintIds,
                        scopedProductIds,
                        cancellationToken);

                _logger.LogInformation(
                    "Computed {ProjectionCount} sprint trend projections for ProductOwner {ProductOwnerId} during recompute request.",
                    projections.Count, query.ProductOwnerId);
            }
            else
            {
                projections = scopedProductIds is null
                    ? await _projectionService.GetProjectionsAsync(
                        query.ProductOwnerId,
                        query.EffectiveFilter.SprintIds,
                        cancellationToken)
                    : await _projectionService.GetProjectionsAsync(
                        query.ProductOwnerId,
                        query.EffectiveFilter.SprintIds,
                        scopedProductIds,
                        cancellationToken);

                _logger.LogInformation(
                    "Retrieved {ProjectionCount} cached sprint trend projections for ProductOwner {ProductOwnerId}.",
                    projections.Count, query.ProductOwnerId);

                if (projections.Count == 0)
                {
                    _logger.LogWarning(
                        "No cached sprint trend projections found for ProductOwner {ProductOwnerId}. Triggering projection computation for requested sprint range.",
                        query.ProductOwnerId);

                    projections = scopedProductIds is null
                        ? await _projectionService.ComputeProjectionsAsync(
                            query.ProductOwnerId,
                            query.EffectiveFilter.SprintIds,
                            cancellationToken)
                        : await _projectionService.ComputeProjectionsAsync(
                            query.ProductOwnerId,
                            query.EffectiveFilter.SprintIds,
                            scopedProductIds,
                            cancellationToken);

                    _logger.LogInformation(
                        "Computed {ProjectionCount} sprint trend projections for ProductOwner {ProductOwnerId} after cache miss.",
                        projections.Count, query.ProductOwnerId);
                }
            }

            // Get sprints for additional info
            var sprints = await _context.Sprints
                .Where(s => query.EffectiveFilter.SprintIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s, cancellationToken);

            IReadOnlyList<FeatureProgressDto> featureProgress = Array.Empty<FeatureProgressDto>();
            IReadOnlyList<EpicProgressDto> epicProgress = Array.Empty<EpicProgressDto>();
            IReadOnlyList<FeatureProgressDto> currentFeatureProgress = Array.Empty<FeatureProgressDto>();
            IReadOnlyList<EpicProgressDto> currentEpicProgress = Array.Empty<EpicProgressDto>();
            IReadOnlyList<ProductDeliveryAnalyticsDto> productAnalytics = Array.Empty<ProductDeliveryAnalyticsDto>();
            IReadOnlyDictionary<int, ProductDeliveryProgressSummary> deliverySummaryByProduct = new Dictionary<int, ProductDeliveryProgressSummary>();
            int? mostRecentSprintId = null;
            IReadOnlyList<FeatureProgressDto> previousFeatureProgress = Array.Empty<FeatureProgressDto>();
            IReadOnlyList<EpicProgressDto> previousEpicProgress = Array.Empty<EpicProgressDto>();

            if (query.EffectiveFilter.SprintIds.Count > 0)
            {
                var orderedAnalyticsSprints = sprints.Values
                    .Where(sprint => sprint.StartDateUtc != null)
                    .OrderBy(sprint => sprint.StartDateUtc)
                    .ThenBy(sprint => sprint.Id)
                    .ToList();

                var mostRecentSprint = query.EffectiveFilter.CurrentSprintId.HasValue
                    ? sprints.GetValueOrDefault(query.EffectiveFilter.CurrentSprintId.Value)
                    : orderedAnalyticsSprints.LastOrDefault();
                var previousSprint = query.EffectiveFilter.PreviousSprintId.HasValue
                    ? sprints.GetValueOrDefault(query.EffectiveFilter.PreviousSprintId.Value)
                    : orderedAnalyticsSprints.Count > 1
                        ? orderedAnalyticsSprints[^2]
                        : null;

                mostRecentSprintId = mostRecentSprint?.Id;
                var (sprintStartForFilter, sprintEndForFilter) = GetSprintWindow(mostRecentSprint);

                // Compute real feature progress from resolved hierarchy, filtered to sprint activity
                currentFeatureProgress = scopedProductIds is null
                    ? await _projectionService.ComputeFeatureProgressAsync(
                        query.ProductOwnerId,
                        FeatureProgressMode.StoryPoints,
                        sprintStartForFilter,
                        sprintEndForFilter,
                        cancellationToken,
                        mostRecentSprint?.Id)
                    : await _projectionService.ComputeFeatureProgressForScopeAsync(
                        query.ProductOwnerId,
                        FeatureProgressMode.StoryPoints,
                        sprintStartForFilter,
                        sprintEndForFilter,
                        cancellationToken,
                        mostRecentSprint?.Id,
                        scopedProductIds);

                // Compute epic progress from feature progress
                currentEpicProgress = scopedProductIds is null
                    ? await _projectionService.ComputeEpicProgressAsync(
                        query.ProductOwnerId,
                        currentFeatureProgress,
                        cancellationToken)
                    : await _projectionService.ComputeEpicProgressAsync(
                        query.ProductOwnerId,
                        currentFeatureProgress,
                        cancellationToken,
                        scopedProductIds);

                deliverySummaryByProduct = currentEpicProgress.ToProductDeliveryProgressSummaries();

                if (previousSprint is not null)
                {
                    var (previousSprintStartForFilter, previousSprintEndForFilter) = GetSprintWindow(previousSprint);

                    previousFeatureProgress = scopedProductIds is null
                        ? await _projectionService.ComputeFeatureProgressAsync(
                            query.ProductOwnerId,
                            FeatureProgressMode.StoryPoints,
                            previousSprintStartForFilter,
                            previousSprintEndForFilter,
                            cancellationToken,
                            previousSprint.Id)
                        : await _projectionService.ComputeFeatureProgressForScopeAsync(
                            query.ProductOwnerId,
                            FeatureProgressMode.StoryPoints,
                            previousSprintStartForFilter,
                            previousSprintEndForFilter,
                            cancellationToken,
                            previousSprint.Id,
                            scopedProductIds);

                    previousEpicProgress = scopedProductIds is null
                        ? await _projectionService.ComputeEpicProgressAsync(
                            query.ProductOwnerId,
                            previousFeatureProgress,
                            cancellationToken)
                        : await _projectionService.ComputeEpicProgressAsync(
                            query.ProductOwnerId,
                            previousFeatureProgress,
                            cancellationToken,
                            scopedProductIds);
                }

                featureProgress = query.IncludeDetails
                    ? currentFeatureProgress
                    : Array.Empty<FeatureProgressDto>();
                epicProgress = query.IncludeDetails
                    ? currentEpicProgress
                    : Array.Empty<EpicProgressDto>();
            }

            var productIds = projections.Select(p => p.ProductId)
                .Concat(currentFeatureProgress.Select(progress => progress.ProductId))
                .Concat(currentEpicProgress.Select(progress => progress.ProductId))
                .Concat(previousFeatureProgress.Select(progress => progress.ProductId))
                .Concat(previousEpicProgress.Select(progress => progress.ProductId))
                .Distinct()
                .ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p, cancellationToken);

            if (previousEpicProgress.Count > 0 || currentEpicProgress.Count > 0)
            {
                var currentFeatureProgressByProduct = currentFeatureProgress
                    .Select(progress => progress.ToFeatureProgress())
                    .GroupBy(progress => progress.ProductId)
                    .ToDictionary(group => group.Key, group => (IReadOnlyList<FeatureProgress>)group.ToList());
                var currentEpicProgressByProduct = currentEpicProgress
                    .Select(progress => progress.ToEpicProgress())
                    .GroupBy(progress => progress.ProductId)
                    .ToDictionary(group => group.Key, group => (IReadOnlyList<EpicProgress>)group.ToList());
                var currentProducts = currentEpicProgress
                    .GroupBy(progress => progress.ProductId)
                    .ToDictionary(
                        group => group.Key,
                        group => _productAggregationService.Compute(new ProductAggregationRequest(
                            group.Select(progress => progress.ToProductAggregationEpicInput()).ToList())));
                var previousProducts = previousEpicProgress
                    .GroupBy(progress => progress.ProductId)
                    .ToDictionary(
                        group => group.Key,
                        group => _productAggregationService.Compute(new ProductAggregationRequest(
                            group.Select(progress => progress.ToProductAggregationEpicInput()).ToList())));

                productAnalytics = currentProducts
                    .Select(pair =>
                    {
                        var productId = pair.Key;
                        var currentProduct = pair.Value;
                        previousProducts.TryGetValue(productId, out var previousProduct);

                        var comparison = _snapshotComparisonService.Compare(new SnapshotComparisonRequest(
                            previousProduct?.ToProductSnapshot(),
                            currentProduct.ToProductSnapshot()));
                        var planningQuality = _planningQualityService.Analyze(new PlanningQualityRequest(
                            productId,
                            currentFeatureProgressByProduct.GetValueOrDefault(productId) ?? Array.Empty<FeatureProgress>(),
                            currentEpicProgressByProduct.GetValueOrDefault(productId) ?? Array.Empty<EpicProgress>(),
                            currentProduct));
                        var insights = _insightService.Analyze(new InsightRequest(
                            currentProduct,
                            comparison,
                            planningQuality));

                        return DeliveryTrendAnalyticsExposureMapper.ToProductDeliveryAnalyticsDto(
                            productId,
                            products.GetValueOrDefault(productId)?.Name ?? "Unknown",
                            currentProduct,
                            comparison,
                            planningQuality,
                            insights);
                    })
                    .OrderBy(analytics => analytics.ProductName)
                    .ToList();
            }

            // Group by sprint
            var metricsBySprint = projections
                .GroupBy(p => p.SprintId)
                .Select(g =>
                {
                    var sprintId = g.Key;
                    var sprint = sprints.GetValueOrDefault(sprintId);

                    var productMetrics = g.Select(p =>
                    {
                        var deliverySummary = sprintId == mostRecentSprintId && deliverySummaryByProduct.TryGetValue(p.ProductId, out var summary)
                            ? summary
                            : null;

                        return new ProductSprintMetricsDto
                        {
                            ProductId = p.ProductId,
                            ProductName = products.GetValueOrDefault(p.ProductId)?.Name ?? "Unknown",
                            PlannedCount = p.PlannedCount,
                            PlannedEffort = p.PlannedEffort,
                            PlannedStoryPoints = p.PlannedStoryPoints,
                            WorkedCount = p.WorkedCount,
                            WorkedEffort = p.WorkedEffort,
                            BugsPlannedCount = p.BugsPlannedCount,
                            BugsWorkedCount = p.BugsWorkedCount,
                            CompletedPbiCount = p.CompletedPbiCount,
                            CompletedPbiEffort = p.CompletedPbiEffort,
                            CompletedPbiStoryPoints = p.CompletedPbiStoryPoints,
                            SpilloverCount = p.SpilloverCount,
                            SpilloverEffort = p.SpilloverEffort,
                            SpilloverStoryPoints = p.SpilloverStoryPoints,
                            ProgressionDelta = p.ProgressionDelta,
                            BugsCreatedCount = p.BugsCreatedCount,
                            BugsClosedCount = p.BugsClosedCount,
                            MissingEffortCount = p.MissingEffortCount,
                            MissingStoryPointCount = p.MissingStoryPointCount,
                            DerivedStoryPointCount = p.DerivedStoryPointCount,
                            DerivedStoryPoints = p.DerivedStoryPoints,
                            UnestimatedDeliveryCount = p.UnestimatedDeliveryCount,
                            IsApproximate = p.IsApproximate,
                            ScopeChangeEffort = deliverySummary?.ScopeChangeEffort,
                            CompletedFeatureCount = deliverySummary?.CompletedFeatureCount
                        };
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
                        TotalPlannedStoryPoints = g.Sum(p => p.PlannedStoryPoints),
                        TotalWorkedCount = g.Sum(p => p.WorkedCount),
                        TotalWorkedEffort = g.Sum(p => p.WorkedEffort),
                        TotalBugsPlannedCount = g.Sum(p => p.BugsPlannedCount),
                        TotalBugsWorkedCount = g.Sum(p => p.BugsWorkedCount),
                        TotalCompletedPbiCount = g.Sum(p => p.CompletedPbiCount),
                        TotalCompletedPbiEffort = g.Sum(p => p.CompletedPbiEffort),
                        TotalCompletedPbiStoryPoints = g.Sum(p => p.CompletedPbiStoryPoints),
                        TotalSpilloverCount = g.Sum(p => p.SpilloverCount),
                        TotalSpilloverEffort = g.Sum(p => p.SpilloverEffort),
                        TotalSpilloverStoryPoints = g.Sum(p => p.SpilloverStoryPoints),
                        TotalProgressionDelta = g.Sum(p => p.ProgressionDelta),
                        TotalBugsCreatedCount = g.Sum(p => p.BugsCreatedCount),
                        TotalBugsClosedCount = g.Sum(p => p.BugsClosedCount),
                        TotalMissingEffortCount = g.Sum(p => p.MissingEffortCount),
                        TotalMissingStoryPointCount = g.Sum(p => p.MissingStoryPointCount),
                        TotalDerivedStoryPointCount = g.Sum(p => p.DerivedStoryPointCount),
                        TotalDerivedStoryPoints = g.Sum(p => p.DerivedStoryPoints),
                        TotalUnestimatedDeliveryCount = g.Sum(p => p.UnestimatedDeliveryCount),
                        IsApproximate = g.Any(p => p.IsApproximate)
                    };
                })
                .OrderBy(m => m.StartUtc ?? DateTimeOffset.MaxValue)
                .ToList();

            _logger.LogInformation(
                "Returning sprint trend metrics for ProductOwner {ProductOwnerId}: {SprintMetricCount} sprint rows from {ProjectionCount} projections.",
                query.ProductOwnerId, metricsBySprint.Count, projections.Count);

            // Detect staleness: activity events ingested after last projection computation
            var cacheState = await _context.ProductOwnerCacheStates
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync(s => s.ProductOwnerId == query.ProductOwnerId, cancellationToken);

            var projectionsAsOf = cacheState?.SprintTrendProjectionAsOfUtc;
            var activityWatermark = cacheState?.ActivityEventWatermark;
            var isStale = !query.Recompute
                && projectionsAsOf.HasValue
                && activityWatermark.HasValue
                && activityWatermark.Value > projectionsAsOf.Value;

            return new GetSprintTrendMetricsResponse
            {
                Success = true,
                Metrics = metricsBySprint,
                FeatureProgress = featureProgress,
                EpicProgress = epicProgress,
                ProductAnalytics = productAnalytics,
                IsStale = isStale,
                ProjectionsAsOfUtc = projectionsAsOf
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

    private static (DateTime? StartUtc, DateTime? EndUtc) GetSprintWindow(SprintEntity? sprint)
    {
        DateTime? startUtc = sprint?.StartDateUtc is { } startDate
            ? DateTime.SpecifyKind(startDate, DateTimeKind.Utc)
            : null;
        DateTime? endUtc = sprint?.EndDateUtc is { } endDate
            ? DateTime.SpecifyKind(endDate, DateTimeKind.Utc)
            : null;

        return (startUtc, endUtc);
    }
}
