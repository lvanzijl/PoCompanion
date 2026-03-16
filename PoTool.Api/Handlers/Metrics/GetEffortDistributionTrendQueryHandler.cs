using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEffortDistributionTrendQuery.
/// Analyzes how effort distribution changes over time and forecasts future trends.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetEffortDistributionTrendQueryHandler
    : IQueryHandler<GetEffortDistributionTrendQuery, EffortDistributionTrendDto>
{
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly IEffortTrendForecastService _effortTrendForecastService;
    private readonly ILogger<GetEffortDistributionTrendQueryHandler> _logger;

    public GetEffortDistributionTrendQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        IEffortTrendForecastService effortTrendForecastService,
        ILogger<GetEffortDistributionTrendQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _effortTrendForecastService = effortTrendForecastService;
        _logger = logger;
    }

    public async ValueTask<EffortDistributionTrendDto> Handle(
        GetEffortDistributionTrendQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetEffortDistributionTrendQuery with AreaPathFilter: {AreaPathFilter}, MaxIterations: {MaxIterations}",
            query.AreaPathFilter ?? "All",
            query.MaxIterations);

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

        // Filter by area path if specified
        if (!string.IsNullOrWhiteSpace(query.AreaPathFilter))
        {
            allWorkItems = allWorkItems
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPathFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var analysis = _effortTrendForecastService.Analyze(
            allWorkItems
                .Select(workItem => new EffortDistributionWorkItem(
                    workItem.AreaPath,
                    workItem.IterationPath,
                    workItem.Effort ?? 0))
                .ToList(),
            query.MaxIterations,
            query.DefaultCapacityPerIteration);

        return new EffortDistributionTrendDto(
            TrendBySprint: analysis.TrendBySprint
                .Select(trend => new SprintTrendData(
                    trend.IterationPath,
                    trend.SprintName,
                    trend.TotalEffort,
                    trend.WorkItemCount,
                    trend.UtilizationPercentage,
                    trend.ChangeFromPrevious,
                    MapDirection(trend.Direction)))
                .ToList(),
            TrendByAreaPath: analysis.TrendByAreaPath
                .Select(trend => new AreaPathTrendData(
                    trend.AreaPath,
                    trend.EffortBySprint,
                    trend.AverageEffort,
                    trend.StandardDeviation,
                    MapDirection(trend.Direction),
                    trend.TrendSlope))
                .ToList(),
            OverallTrend: MapDirection(analysis.OverallTrend),
            TrendSlope: analysis.TrendSlope,
            Forecasts: analysis.Forecasts
                .Select(forecast => new DistributionForecast(
                    forecast.SprintName,
                    forecast.ForecastedEffort,
                    forecast.LowEstimate,
                    forecast.HighEstimate,
                    forecast.ConfidenceLevel))
                .ToList(),
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private static EffortTrendDirection MapDirection(EffortForecastDirection direction)
    {
        return direction switch
        {
            EffortForecastDirection.Increasing => EffortTrendDirection.Increasing,
            EffortForecastDirection.Decreasing => EffortTrendDirection.Decreasing,
            EffortForecastDirection.Volatile => EffortTrendDirection.Volatile,
            _ => EffortTrendDirection.Stable
        };
    }
}
