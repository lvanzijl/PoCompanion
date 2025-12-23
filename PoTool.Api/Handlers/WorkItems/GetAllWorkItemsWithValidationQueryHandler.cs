using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllWorkItemsWithValidationQuery.
/// Retrieves all work items and attaches validation results.
/// Automatically filters by active profile's area paths if a profile is set.
/// </summary>
public sealed class GetAllWorkItemsWithValidationQueryHandler 
    : IQueryHandler<GetAllWorkItemsWithValidationQuery, IEnumerable<WorkItemWithValidationDto>>
{
    private readonly IWorkItemRepository _repository;
    private readonly IWorkItemValidator _validator;
    private readonly ProfileFilterService _profileFilterService;
    private readonly ILogger<GetAllWorkItemsWithValidationQueryHandler> _logger;

    public GetAllWorkItemsWithValidationQueryHandler(
        IWorkItemRepository repository,
        IWorkItemValidator validator,
        ProfileFilterService profileFilterService,
        ILogger<GetAllWorkItemsWithValidationQueryHandler> logger)
    {
        _repository = repository;
        _validator = validator;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemWithValidationDto>> Handle(
        GetAllWorkItemsWithValidationQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllWorkItemsWithValidationQuery");
        
        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);
        
        IEnumerable<WorkItemDto> workItems;
        if (profileAreaPaths != null && profileAreaPaths.Count > 0)
        {
            _logger.LogDebug("Filtering work items by active profile area paths for validation");
            workItems = await _repository.GetByAreaPathsAsync(profileAreaPaths, cancellationToken);
        }
        else
        {
            workItems = await _repository.GetAllAsync(cancellationToken);
        }
        
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
            wi.JsonPayload,
            wi.RetrievedAt,
            wi.Effort,
            validationResults.TryGetValue(wi.TfsId, out var issues) 
                ? issues 
                : new List<ValidationIssue>()
        )).ToList();
    }
}
