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
    private readonly ILogger<GetWorkItemByIdWithValidationQueryHandler> _logger;

    public GetWorkItemByIdWithValidationQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        IWorkItemValidator validator,
        ILogger<GetWorkItemByIdWithValidationQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _validator = validator;
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

        // Note: productIds parameter is included for future use but not currently enforced
        // The cache already filters work items based on products during load operations
        // so work items not belonging to active products won't be in the cache

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
            workItem.RetrievedAt,
            workItem.Effort,
            workItem.Description,
            issues,
            workItem.CreatedDate,
            workItem.ClosedDate,
            workItem.Severity,
            workItem.Tags,
            workItem.IsBlocked
        );
    }
}
