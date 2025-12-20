using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllWorkItemsWithValidationQuery.
/// Retrieves all work items and attaches validation results.
/// </summary>
public sealed class GetAllWorkItemsWithValidationQueryHandler 
    : IQueryHandler<GetAllWorkItemsWithValidationQuery, IEnumerable<WorkItemWithValidationDto>>
{
    private readonly IWorkItemRepository _repository;
    private readonly IWorkItemValidator _validator;
    private readonly ILogger<GetAllWorkItemsWithValidationQueryHandler> _logger;

    public GetAllWorkItemsWithValidationQueryHandler(
        IWorkItemRepository repository,
        IWorkItemValidator validator,
        ILogger<GetAllWorkItemsWithValidationQueryHandler> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemWithValidationDto>> Handle(
        GetAllWorkItemsWithValidationQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllWorkItemsWithValidationQuery");
        
        var workItems = (await _repository.GetAllAsync(cancellationToken)).ToList();
        var validationResults = _validator.ValidateWorkItems(workItems);

        return workItems.Select(wi => new WorkItemWithValidationDto(
            wi.TfsId,
            wi.Type,
            wi.Title,
            wi.ParentTfsId,
            wi.AreaPath,
            wi.IterationPath,
            wi.State,
            wi.JsonPayload,
            wi.RetrievedAt,
            wi.Effort,
            validationResults.TryGetValue(wi.TfsId, out var issues) 
                ? issues 
                : new List<ValidationIssue>()
        )).ToList();
    }
}
