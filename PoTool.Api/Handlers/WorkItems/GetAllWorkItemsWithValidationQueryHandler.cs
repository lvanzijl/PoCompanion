using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllWorkItemsWithValidationQuery.
/// Retrieves all work items and attaches validation results.
/// Automatically filters by active profile's area paths if a profile is set.
/// Uses hierarchical loading from products when available, otherwise falls back to area path loading.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetAllWorkItemsWithValidationQueryHandler
    : IQueryHandler<GetAllWorkItemsWithValidationQuery, IEnumerable<WorkItemWithValidationDto>>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IWorkItemValidator _validator;
    private readonly ProfileFilterService _profileFilterService;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<GetAllWorkItemsWithValidationQueryHandler> _logger;

    public GetAllWorkItemsWithValidationQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        IWorkItemValidator validator,
        ProfileFilterService profileFilterService,
        IProductRepository productRepository,
        ILogger<GetAllWorkItemsWithValidationQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _validator = validator;
        _profileFilterService = profileFilterService;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemWithValidationDto>> Handle(
        GetAllWorkItemsWithValidationQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllWorkItemsWithValidationQuery with ProductIds: {ProductIds}", 
            query.ProductIds != null ? string.Join(", ", query.ProductIds) : "null (all products)");

        IEnumerable<WorkItemDto> workItems;

        // Determine which products to load from
        IEnumerable<ProductDto> productsToLoad;
        if (query.ProductIds != null && query.ProductIds.Length > 0)
        {
            // Load only the specified products
            var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
            var productIdSet = new HashSet<int>(query.ProductIds);
            productsToLoad = allProducts.Where(p => productIdSet.Contains(p.Id)).ToList();
            _logger.LogDebug("Loading work items for {Count} specified products: {ProductIds}", 
                productsToLoad.Count(), string.Join(", ", query.ProductIds));
        }
        else
        {
            // Load all products (existing behavior)
            productsToLoad = await _productRepository.GetAllProductsAsync(cancellationToken);
            _logger.LogDebug("Loading work items for all {Count} products", productsToLoad.Count());
        }

        var productsList = productsToLoad.ToList();

        if (productsList.Count > 0)
        {
            // Product-scoped hierarchical loading
            var rootIds = productsList
                .SelectMany(p => p.BacklogRootWorkItemIds)
                .Distinct()
                .ToArray();

            if (rootIds.Length == 0)
            {
                _logger.LogWarning("Products configured but none have backlog root work item IDs configured. Falling back to area path loading");
                // Fall through to area path loading
            }
            else
            {
                _logger.LogInformation("Loading work items hierarchically from {Count} product roots: {RootIds}",
                    rootIds.Length, string.Join(", ", rootIds));
                
                workItems = await _workItemReadProvider.GetByRootIdsAsync(rootIds, cancellationToken);
                _logger.LogDebug("Loaded {Count} work items via hierarchical loading", workItems.Count());
                
                // Skip area path loading
                goto ValidateWorkItems;
            }
        }

        // Fallback to area path-based loading when no products are configured or all have invalid root IDs
        {
            _logger.LogDebug("Falling back to area path loading");
            
            var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);

            if (profileAreaPaths != null && profileAreaPaths.Count > 0)
            {
                _logger.LogDebug("Filtering work items by active profile area paths for validation");
                workItems = await _workItemReadProvider.GetByAreaPathsAsync(profileAreaPaths, cancellationToken);
            }
            else
            {
                workItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
            }
        }

    ValidateWorkItems:
        var workItemsList = workItems.ToList();
        var validationResults = _validator.ValidateWorkItems(workItemsList);

        return workItemsList.Select(wi => new WorkItemWithValidationDto(
            wi.TfsId,
            wi.Type,
            wi.Title,
            wi.ParentTfsId,
            wi.AreaPath,
            wi.IterationPath,
            wi.State,
            wi.RetrievedAt,
            wi.Effort,
            wi.Description,
            validationResults.TryGetValue(wi.TfsId, out var issues)
                ? issues
                : new List<ValidationIssue>(),
            wi.CreatedDate,
            wi.ClosedDate,
            wi.Severity,
            wi.Tags,
            wi.IsBlocked,
            wi.BusinessValue,
            wi.BacklogPriority
        )).ToList();
    }
}
