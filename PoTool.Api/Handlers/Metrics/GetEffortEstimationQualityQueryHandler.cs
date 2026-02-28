using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;

using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEffortEstimationQualityQuery.
/// Analyzes historical effort estimation accuracy by comparing estimates with completion patterns.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetEffortEstimationQualityQueryHandler
    : IQueryHandler<GetEffortEstimationQualityQuery, EffortEstimationQualityDto>
{
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly ILogger<GetEffortEstimationQualityQueryHandler> _logger;

    public GetEffortEstimationQualityQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        IWorkItemStateClassificationService stateClassificationService,
        ILogger<GetEffortEstimationQualityQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _stateClassificationService = stateClassificationService;
        _logger = logger;
    }

    public async ValueTask<EffortEstimationQualityDto> Handle(
        GetEffortEstimationQualityQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetEffortEstimationQualityQuery for area={AreaPath}, maxIterations={MaxIterations}",
            query.AreaPath, query.MaxIterations);

        // Load work items using product-scoped approach
        IEnumerable<WorkItemDto> allWorkItems;
        var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
        var productsList = allProducts.ToList();

        if (productsList.Count > 0)
        {
            var rootIds = productsList
                .SelectMany(p => p.BacklogRootWorkItemIds)
                .ToArray();

            if (rootIds.Length > 0)
            {
                var workItemsQuery = new GetWorkItemsByRootIdsQuery(rootIds);
                allWorkItems = await _mediator.Send(workItemsQuery, cancellationToken);
            }
            else
            {
                allWorkItems = await _repository.GetAllAsync(cancellationToken);
            }
        }
        else
        {
            allWorkItems = await _repository.GetAllAsync(cancellationToken);
        }
        var workItemsList = allWorkItems.ToList();

        // Filter by area path if specified
        if (!string.IsNullOrEmpty(query.AreaPath))
        {
            workItemsList = workItemsList
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Get completed work items with effort estimates
        var completedWorkItems = new List<WorkItemDto>();
        foreach (var wi in workItemsList.Where(wi => wi.Effort.HasValue && wi.Effort.Value > 0))
        {
            if (await _stateClassificationService.IsDoneStateAsync(wi.Type, wi.State, cancellationToken))
            {
                completedWorkItems.Add(wi);
            }
        }
        completedWorkItems = completedWorkItems.OrderByDescending(wi => wi.RetrievedAt).ToList();

        _logger.LogDebug("Found {Count} completed work items with effort estimates", completedWorkItems.Count);

        // Group by iteration path and take most recent iterations
        var iterationGroups = completedWorkItems
            .GroupBy(wi => wi.IterationPath)
            .OrderByDescending(g => g.Max(wi => wi.RetrievedAt))
            .Take(query.MaxIterations)
            .ToList();

        // For quality analysis, we use a heuristic approach:
        // - Compare effort distribution across similar work item types
        // - Identify outliers and patterns
        // - Calculate consistency metrics

        var qualityByType = CalculateQualityByType(completedWorkItems);
        var trendOverTime = CalculateTrendOverTime(iterationGroups);
        var overallAccuracy = CalculateOverallAccuracy(completedWorkItems);

        var result = new EffortEstimationQualityDto(
            AverageEstimationAccuracy: overallAccuracy,
            TotalCompletedWorkItems: completedWorkItems.Count,
            WorkItemsWithEstimates: completedWorkItems.Count(wi => wi.Effort.HasValue && wi.Effort.Value > 0),
            QualityByType: qualityByType,
            TrendOverTime: trendOverTime
        );

        _logger.LogInformation("Effort estimation quality analysis complete: {Accuracy:P2} accuracy across {Count} items",
            overallAccuracy, completedWorkItems.Count);

        return result;
    }

    private IReadOnlyList<WorkItemTypeEstimationQuality> CalculateQualityByType(List<WorkItemDto> workItems)
    {
        var groupedByType = workItems
            .GroupBy(wi => wi.Type)
            .ToList();

        var qualityList = new List<WorkItemTypeEstimationQuality>();

        foreach (var group in groupedByType)
        {
            var efforts = group.Select(wi => wi.Effort!.Value).ToList();

            if (efforts.Count == 0)
                continue;

            var min = efforts.Min();
            var max = efforts.Max();
            var avg = (int)Math.Round(efforts.Average());

            // Calculate consistency as accuracy metric
            // Lower variance = higher accuracy/consistency
            var variance = CalculateVariance(efforts);
            var coefficientOfVariation = avg > 0 ? Math.Sqrt(variance) / avg : 0;

            // Convert to accuracy score (0-1, where 1 is perfect consistency)
            var accuracy = Math.Max(0, 1.0 - Math.Min(1.0, coefficientOfVariation));

            qualityList.Add(new WorkItemTypeEstimationQuality(
                WorkItemType: group.Key,
                Count: group.Count(),
                AverageAccuracy: accuracy,
                TypicalEffortMin: min,
                TypicalEffortMax: max,
                AverageEffort: avg
            ));
        }

        return qualityList
            .OrderByDescending(q => q.Count)
            .ToList();
    }

    private IReadOnlyList<EstimationTrend> CalculateTrendOverTime(
        List<IGrouping<string, WorkItemDto>> iterationGroups)
    {
        var trends = new List<EstimationTrend>();

        foreach (var group in iterationGroups)
        {
            var items = group.ToList();
            var efforts = items.Select(wi => wi.Effort!.Value).ToList();

            if (efforts.Count == 0)
                continue;

            var avg = efforts.Average();
            var variance = CalculateVariance(efforts);
            var coefficientOfVariation = avg > 0 ? Math.Sqrt(variance) / avg : 0;
            var accuracy = Math.Max(0, 1.0 - Math.Min(1.0, coefficientOfVariation));

            var startDate = items.Min(wi => wi.RetrievedAt);
            var endDate = items.Max(wi => wi.RetrievedAt);

            trends.Add(new EstimationTrend(
                Period: group.Key,
                StartDate: startDate,
                EndDate: endDate,
                AverageAccuracy: accuracy,
                EstimatedCount: efforts.Count
            ));
        }

        return trends
            .OrderBy(t => t.StartDate)
            .ToList();
    }

    private double CalculateOverallAccuracy(List<WorkItemDto> workItems)
    {
        if (workItems.Count == 0)
            return 0.0;

        // Group by type and calculate weighted average accuracy
        var typeGroups = workItems.GroupBy(wi => wi.Type).ToList();
        var totalItems = workItems.Count;
        double weightedAccuracySum = 0;

        foreach (var group in typeGroups)
        {
            var efforts = group.Select(wi => wi.Effort!.Value).ToList();
            var avg = efforts.Average();
            var variance = CalculateVariance(efforts);
            var coefficientOfVariation = avg > 0 ? Math.Sqrt(variance) / avg : 0;
            var accuracy = Math.Max(0, 1.0 - Math.Min(1.0, coefficientOfVariation));

            var weight = (double)group.Count() / totalItems;
            weightedAccuracySum += accuracy * weight;
        }

        return weightedAccuracySum;
    }

    private double CalculateVariance(List<int> values)
    {
        if (values.Count <= 1)
            return 0.0;

        var mean = values.Average();
        var sumOfSquaredDifferences = values.Sum(val => Math.Pow(val - mean, 2));
        return sumOfSquaredDifferences / values.Count;
    }
}
