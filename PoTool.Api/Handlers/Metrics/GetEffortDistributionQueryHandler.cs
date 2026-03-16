using Mediator;
using PoTool.Api.Adapters;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.EffortPlanning;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEffortDistributionQuery.
/// Calculates effort distribution across area paths and iterations for heat map visualization.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetEffortDistributionQueryHandler
    : IQueryHandler<GetEffortDistributionQuery, EffortDistributionDto>
{
    private readonly IWorkItemRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly IEffortDistributionService _effortDistributionService;
    private readonly ILogger<GetEffortDistributionQueryHandler> _logger;

    public GetEffortDistributionQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        IEffortDistributionService effortDistributionService,
        ILogger<GetEffortDistributionQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _effortDistributionService = effortDistributionService;
        _logger = logger;
    }

    public async ValueTask<EffortDistributionDto> Handle(
        GetEffortDistributionQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetEffortDistributionQuery with AreaPathFilter: {AreaPathFilter}, MaxIterations: {MaxIterations}",
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

        // Filter to items with effort only
        var workItemsWithEffort = allWorkItems
            .Where(wi => wi.Effort.HasValue && wi.Effort.Value > 0)
            .ToList();

        var distribution = _effortDistributionService.Analyze(
            workItemsWithEffort.Select(static wi => wi.ToEffortPlanningWorkItem()).ToList(),
            query.MaxIterations,
            query.DefaultCapacityPerIteration);

        return new EffortDistributionDto(
            EffortByArea: distribution.EffortByArea
                .Select(static area => new EffortByAreaPath(
                    area.AreaPath,
                    area.TotalEffort,
                    area.WorkItemCount,
                    area.AverageEffortPerItem))
                .ToList(),
            EffortByIteration: distribution.EffortByIteration
                .Select(static iteration => new EffortByIteration(
                    iteration.IterationPath,
                    iteration.SprintName,
                    iteration.TotalEffort,
                    iteration.WorkItemCount,
                    iteration.Capacity,
                    iteration.UtilizationPercentage))
                .ToList(),
            HeatMapData: distribution.HeatMapData
                .Select(static cell => new EffortHeatMapCell(
                    cell.AreaPath,
                    cell.IterationPath,
                    cell.Effort,
                    cell.WorkItemCount,
                    cell.Status))
                .ToList(),
            TotalEffort: distribution.TotalEffort,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }
}
