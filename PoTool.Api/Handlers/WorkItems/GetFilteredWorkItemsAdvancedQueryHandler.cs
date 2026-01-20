using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetFilteredWorkItemsAdvancedQuery.
/// Applies multiple filter dimensions to work items for advanced search.
/// Uses product-scoped hierarchical loading when products are configured.
/// </summary>
public sealed class GetFilteredWorkItemsAdvancedQueryHandler
    : IQueryHandler<GetFilteredWorkItemsAdvancedQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemRepository _repository;
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<GetFilteredWorkItemsAdvancedQueryHandler> _logger;

    public GetFilteredWorkItemsAdvancedQueryHandler(
        IWorkItemRepository repository,
        IWorkItemReadProvider workItemReadProvider,
        IProductRepository productRepository,
        ILogger<GetFilteredWorkItemsAdvancedQueryHandler> logger)
    {
        _repository = repository;
        _workItemReadProvider = workItemReadProvider;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetFilteredWorkItemsAdvancedQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetFilteredWorkItemsAdvancedQuery with filters");

        // Load work items using product-scoped approach
        IEnumerable<WorkItemDto> allWorkItems;
        var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
        var productsList = allProducts.ToList();

        if (productsList.Count > 0)
        {
            var rootIds = productsList
                .Where(p => p.BacklogRootWorkItemId > 0)
                .Select(p => p.BacklogRootWorkItemId)
                .ToArray();

            if (rootIds.Length > 0)
            {
                _logger.LogDebug("Loading work items from {Count} product roots for filtering", rootIds.Length);
                allWorkItems = await _workItemReadProvider.GetByRootIdsAsync(rootIds, cancellationToken);
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

        IEnumerable<WorkItemDto> filtered = allWorkItems;

        // Apply Type filter
        if (!string.IsNullOrWhiteSpace(query.TypeFilter))
        {
            filtered = filtered.Where(wi =>
                wi.Type.Equals(query.TypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Apply State filter
        if (!string.IsNullOrWhiteSpace(query.StateFilter))
        {
            filtered = filtered.Where(wi =>
                wi.State.Equals(query.StateFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Apply Iteration Path filter
        if (!string.IsNullOrWhiteSpace(query.IterationPathFilter))
        {
            filtered = filtered.Where(wi =>
                wi.IterationPath != null &&
                wi.IterationPath.Contains(query.IterationPathFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Apply Area Path filter
        if (!string.IsNullOrWhiteSpace(query.AreaPathFilter))
        {
            filtered = filtered.Where(wi =>
                wi.AreaPath != null &&
                wi.AreaPath.Contains(query.AreaPathFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Apply Effort filters
        if (query.MinEffort.HasValue)
        {
            filtered = filtered.Where(wi =>
                wi.Effort.HasValue && wi.Effort.Value >= query.MinEffort.Value);
        }

        if (query.MaxEffort.HasValue)
        {
            filtered = filtered.Where(wi =>
                wi.Effort.HasValue && wi.Effort.Value <= query.MaxEffort.Value);
        }

        // Apply Validation Issues filter
        if (query.HasValidationIssues.HasValue && query.HasValidationIssues.Value)
        {
            // Items without effort or with parent progress issues
            filtered = filtered.Where(wi =>
                !wi.Effort.HasValue ||
                (wi.ParentTfsId.HasValue && !wi.Effort.HasValue) ||
                (wi.State == "In Progress" && !wi.Effort.HasValue));
        }

        // Apply Blocked filter
        if (query.IsBlocked.HasValue && query.IsBlocked.Value)
        {
            filtered = filtered.Where(wi =>
                wi.State.Equals("Blocked", StringComparison.OrdinalIgnoreCase) ||
                (wi.JsonPayload != null && wi.JsonPayload.Contains("\"Blocked\":\"Yes\"", StringComparison.OrdinalIgnoreCase)));
        }

        // Apply Title search
        if (!string.IsNullOrWhiteSpace(query.TitleSearch))
        {
            filtered = filtered.Where(wi =>
                wi.Title.Contains(query.TitleSearch, StringComparison.OrdinalIgnoreCase));
        }

        var result = filtered.ToList();
        _logger.LogDebug("Advanced filter returned {Count} work items", result.Count);

        return result;
    }
}
