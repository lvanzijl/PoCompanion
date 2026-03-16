using Mediator;
using PoTool.Api.Adapters;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.EffortPlanning;
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
    private readonly IEffortEstimationQualityService _effortEstimationQualityService;
    private readonly ILogger<GetEffortEstimationQualityQueryHandler> _logger;

    public GetEffortEstimationQualityQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        IMediator mediator,
        IWorkItemStateClassificationService stateClassificationService,
        IEffortEstimationQualityService effortEstimationQualityService,
        ILogger<GetEffortEstimationQualityQueryHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _mediator = mediator;
        _stateClassificationService = stateClassificationService;
        _effortEstimationQualityService = effortEstimationQualityService;
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

        var quality = _effortEstimationQualityService.Analyze(
            completedWorkItems.Select(static wi => wi.ToEffortPlanningWorkItem()).ToList(),
            query.MaxIterations);

        var result = new EffortEstimationQualityDto(
            AverageEstimationAccuracy: quality.AverageEstimationAccuracy,
            TotalCompletedWorkItems: quality.TotalCompletedWorkItems,
            WorkItemsWithEstimates: quality.WorkItemsWithEstimates,
            QualityByType: quality.QualityByType
                .Select(static entry => new WorkItemTypeEstimationQuality(
                    entry.WorkItemType,
                    entry.Count,
                    entry.AverageAccuracy,
                    entry.TypicalEffortMin,
                    entry.TypicalEffortMax,
                    entry.AverageEffort))
                .ToList(),
            TrendOverTime: quality.TrendOverTime
                .Select(static trend => new EstimationTrend(
                    trend.Period,
                    trend.StartDate,
                    trend.EndDate,
                    trend.AverageAccuracy,
                    trend.EstimatedCount))
                .ToList()
        );

        _logger.LogInformation("Effort estimation quality analysis complete: {Accuracy:P2} accuracy across {Count} items",
            quality.AverageEstimationAccuracy, completedWorkItems.Count);

        return result;
    }
}
