using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllWorkItemsQuery.
/// Uses product-scoped hierarchical loading when products are configured.
/// Falls back to area path filtering if no products exist.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetAllWorkItemsQueryHandler : IQueryHandler<GetAllWorkItemsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly ProfileFilterService _profileFilterService;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<GetAllWorkItemsQueryHandler> _logger;

    public GetAllWorkItemsQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        ProfileFilterService profileFilterService,
        IProductRepository productRepository,
        ILogger<GetAllWorkItemsQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _profileFilterService = profileFilterService;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetAllWorkItemsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllWorkItemsQuery");

        // Check if there are products configured - if so, use hierarchical loading
        var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
        var productsList = allProducts.ToList();

        if (productsList.Count > 0)
        {
            // Product-scoped hierarchical loading
            var rootIds = productsList
                .SelectMany(p => p.BacklogRootWorkItemIds)
                .ToArray();

            if (rootIds.Length > 0)
            {
                _logger.LogInformation("Loading work items hierarchically from {Count} product roots: {RootIds}",
                    rootIds.Length, string.Join(", ", rootIds));
                return await _workItemReadProvider.GetByRootIdsAsync(rootIds, cancellationToken);
            }
        }

        // Fallback to area path-based loading when no products are configured
        _logger.LogDebug("No products configured, falling back to area path loading");
        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);

        if (profileAreaPaths != null && profileAreaPaths.Count > 0)
        {
            _logger.LogDebug("Filtering work items by active profile area paths: {AreaPaths}",
                string.Join(", ", profileAreaPaths));
            return await _workItemReadProvider.GetByAreaPathsAsync(profileAreaPaths, cancellationToken);
        }

        return await _workItemReadProvider.GetAllAsync(cancellationToken);
    }
}
