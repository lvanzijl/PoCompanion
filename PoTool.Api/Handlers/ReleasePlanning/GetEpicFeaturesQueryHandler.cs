using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.ReleasePlanning;
using PoTool.Core.ReleasePlanning.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.ReleasePlanning;

/// <summary>
/// Handler for GetEpicFeaturesQuery.
/// Returns all Features for a specific Epic (for the Epic Split dialog).
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetEpicFeaturesQueryHandler : IQueryHandler<GetEpicFeaturesQuery, IReadOnlyList<EpicFeatureDto>>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<GetEpicFeaturesQueryHandler> _logger;

    public GetEpicFeaturesQueryHandler(
        IWorkItemRepository workItemRepository,
        IProductRepository productRepository,
        IMediator mediator,
        ILogger<GetEpicFeaturesQueryHandler> logger)
    {
        _workItemRepository = workItemRepository;
        _productRepository = productRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async ValueTask<IReadOnlyList<EpicFeatureDto>> Handle(
        GetEpicFeaturesQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetEpicFeaturesQuery for Epic {EpicId}", query.EpicId);

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
                allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
            }
        }
        else
        {
            allWorkItems = await _workItemRepository.GetAllAsync(cancellationToken);
        }
        var features = allWorkItems
            .Where(w => w.Type.Equals("Feature", StringComparison.OrdinalIgnoreCase)
                        && w.ParentTfsId == query.EpicId)
            .OrderBy(w => w.Title)
            .ToList();

        var result = features.Select(f => new EpicFeatureDto
        {
            FeatureId = f.TfsId,
            Title = f.Title,
            Effort = f.Effort,
            State = f.State
        }).ToList();

        return result;
    }
}
