using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllGoalsQuery.
/// Retrieves all work items of type Goal from configured products.
/// Uses product-scoped hierarchical loading when products are configured.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetAllGoalsQueryHandler : IQueryHandler<GetAllGoalsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemQuery _workItemQuery;
    private readonly ProfileFilterService _profileFilterService;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<GetAllGoalsQueryHandler> _logger;

    public GetAllGoalsQueryHandler(
        IWorkItemQuery workItemQuery,
        ProfileFilterService profileFilterService,
        IProductRepository productRepository,
        ILogger<GetAllGoalsQueryHandler> logger)
    {
        _workItemQuery = workItemQuery;
        _profileFilterService = profileFilterService;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetAllGoalsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllGoalsQuery");

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
                _logger.LogDebug("Loading goals from {Count} product roots", rootIds.Length);
                allWorkItems = await _workItemQuery.GetByRootIdsAsync(rootIds, cancellationToken);
            }
            else
            {
                _logger.LogDebug("No valid product roots, falling back to area path loading");
                var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);
                if (profileAreaPaths != null && profileAreaPaths.Count > 0)
                {
                    allWorkItems = await _workItemQuery.GetByAreaPathsAsync(profileAreaPaths, cancellationToken);
                }
                else
                {
                    allWorkItems = await _workItemQuery.GetAllAsync(cancellationToken);
                }
            }
        }
        else
        {
            _logger.LogDebug("No products configured, falling back to area path loading");
            var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);
            if (profileAreaPaths != null && profileAreaPaths.Count > 0)
            {
                allWorkItems = await _workItemQuery.GetByAreaPathsAsync(profileAreaPaths, cancellationToken);
            }
            else
            {
                allWorkItems = await _workItemQuery.GetAllAsync(cancellationToken);
            }
        }

        return allWorkItems.Where(wi => wi.Type == WorkItemType.Goal).ToList();
    }
}
