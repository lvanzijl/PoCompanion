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
    private readonly IWorkItemQuery _workItemQuery;
    private readonly IWorkItemValidator _validator;
    private readonly ProfileFilterService _profileFilterService;
    private readonly ILogger<GetAllWorkItemsWithValidationQueryHandler> _logger;

    public GetAllWorkItemsWithValidationQueryHandler(
        IWorkItemQuery workItemQuery,
        IWorkItemValidator validator,
        ProfileFilterService profileFilterService,
        ILogger<GetAllWorkItemsWithValidationQueryHandler> logger)
    {
        _workItemQuery = workItemQuery;
        _validator = validator;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemWithValidationDto>> Handle(
        GetAllWorkItemsWithValidationQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllWorkItemsWithValidationQuery with ProductIds: {ProductIds}", 
            query.ProductIds != null ? string.Join(", ", query.ProductIds) : "null (all products)");

        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);
        var workItems = await _workItemQuery.GetWorkItemsForListingAsync(
            query.ProductIds,
            profileAreaPaths,
            cancellationToken);

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
            wi.BacklogPriority,
            wi.StoryPoints,
            wi.TimeCriticality,
            wi.ProjectNumber,
            wi.ProjectElement
        )).ToList();
    }
}
