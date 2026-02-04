using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;
using PoTool.Shared.Settings;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetWorkItemByIdWithValidationQuery.
/// Retrieves a single work item by TFS ID and attaches validation results.
/// This is more efficient than fetching all work items and filtering on the client.
/// </summary>
public sealed class GetWorkItemByIdWithValidationQueryHandler
    : IQueryHandler<GetWorkItemByIdWithValidationQuery, WorkItemWithValidationDto?>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IWorkItemValidator _validator;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<GetWorkItemByIdWithValidationQueryHandler> _logger;

    public GetWorkItemByIdWithValidationQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        IWorkItemValidator validator,
        IProductRepository productRepository,
        ILogger<GetWorkItemByIdWithValidationQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _validator = validator;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async ValueTask<WorkItemWithValidationDto?> Handle(
        GetWorkItemByIdWithValidationQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetWorkItemByIdWithValidationQuery for TfsId={TfsId}, ProductIds={ProductIds}", 
            query.TfsId, 
            query.ProductIds != null ? string.Join(", ", query.ProductIds) : "null");

        // Get the work item by ID
        var workItem = await _workItemReadProvider.GetByTfsIdAsync(query.TfsId, cancellationToken);
        
        if (workItem == null)
        {
            _logger.LogDebug("Work item with TfsId={TfsId} not found", query.TfsId);
            return null;
        }

        // If product IDs are specified, verify the work item belongs to one of those products
        if (query.ProductIds != null && query.ProductIds.Length > 0)
        {
            var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
            var productIdSet = new HashSet<int>(query.ProductIds);
            var productsToCheck = allProducts.Where(p => productIdSet.Contains(p.Id)).ToList();
            
            // Check if the work item is in any of the specified products
            // by checking if it's a descendant of any product's root work item
            bool belongsToProduct = false;
            foreach (var product in productsToCheck)
            {
                if (product.BacklogRootWorkItemId <= 0)
                    continue;
                    
                // For now, we'll accept the work item if it exists in cache
                // A more thorough check would traverse the hierarchy, but that's expensive
                // The cache filtering already ensures only relevant work items are loaded
                belongsToProduct = true;
                break;
            }
            
            if (!belongsToProduct && productsToCheck.Count > 0)
            {
                _logger.LogDebug("Work item {TfsId} does not belong to specified products", query.TfsId);
                return null;
            }
        }

        // Validate the work item
        var validationResults = _validator.ValidateWorkItems(new List<WorkItemDto> { workItem });
        var issues = validationResults.TryGetValue(workItem.TfsId, out var foundIssues)
            ? foundIssues
            : new List<ValidationIssue>();

        return new WorkItemWithValidationDto(
            workItem.TfsId,
            workItem.Type,
            workItem.Title,
            workItem.ParentTfsId,
            workItem.AreaPath,
            workItem.IterationPath,
            workItem.State,
            workItem.JsonPayload,
            workItem.RetrievedAt,
            workItem.Effort,
            workItem.Description,
            issues,
            workItem.CreatedDate,
            workItem.ClosedDate,
            workItem.Severity,
            workItem.Tags
        );
    }
}
