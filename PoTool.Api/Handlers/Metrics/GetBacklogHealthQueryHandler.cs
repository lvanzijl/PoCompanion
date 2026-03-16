using Mediator;
using PoTool.Api.Services;
using PoTool.Core.BacklogQuality;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Metrics;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetBacklogHealthQuery.
/// Calculates backlog health metrics for a specific iteration.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetBacklogHealthQueryHandler
    : IQueryHandler<GetBacklogHealthQuery, BacklogHealthDto?>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly IBacklogQualityAnalysisService _backlogQualityAnalysisService;
    private readonly ILogger<GetBacklogHealthQueryHandler> _logger;

    public GetBacklogHealthQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        IProductRepository productRepository,
        IMediator mediator,
        IBacklogQualityAnalysisService backlogQualityAnalysisService,
        ILogger<GetBacklogHealthQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _productRepository = productRepository;
        _mediator = mediator;
        _backlogQualityAnalysisService = backlogQualityAnalysisService;
        _logger = logger;
    }

    public async ValueTask<BacklogHealthDto?> Handle(
        GetBacklogHealthQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetBacklogHealthQuery for iteration: {IterationPath}", query.IterationPath);

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
                allWorkItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
            }
        }
        else
        {
            allWorkItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
        }

        var iterationWorkItems = allWorkItems
            .Where(wi => wi.IterationPath.Equals(query.IterationPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!iterationWorkItems.Any())
        {
            _logger.LogDebug("No work items found for iteration: {IterationPath}", query.IterationPath);
            return null;
        }

        var analysis = await _backlogQualityAnalysisService.AnalyzeAsync(iterationWorkItems, cancellationToken);
        var (startDate, endDate) = ExtractSprintDates(iterationWorkItems);

        return BacklogHealthDtoFactory.Create(
            query.IterationPath,
            iterationWorkItems,
            analysis,
            startDate,
            endDate);
    }

    private static (DateTimeOffset?, DateTimeOffset?) ExtractSprintDates(List<WorkItemDto> workItems)
    {
        // For now, return null - in future, could parse from JsonPayload
        // or maintain separate sprint metadata
        return (null, null);
    }
}
