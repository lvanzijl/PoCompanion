using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllWorkItemsWithValidationQuery.
/// Retrieves all work items and attaches validation results.
/// Automatically filters by active profile's area paths if a profile is set.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetAllWorkItemsWithValidationQueryHandler
    : IQueryHandler<GetAllWorkItemsWithValidationQuery, IEnumerable<WorkItemWithValidationDto>>
{
    private readonly WorkItemReadProviderFactory _providerFactory;
    private readonly IWorkItemValidator _validator;
    private readonly ProfileFilterService _profileFilterService;
    private readonly ILogger<GetAllWorkItemsWithValidationQueryHandler> _logger;

    public GetAllWorkItemsWithValidationQueryHandler(
        WorkItemReadProviderFactory providerFactory,
        IWorkItemValidator validator,
        ProfileFilterService profileFilterService,
        ILogger<GetAllWorkItemsWithValidationQueryHandler> logger)
    {
        _providerFactory = providerFactory;
        _validator = validator;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemWithValidationDto>> Handle(
        GetAllWorkItemsWithValidationQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllWorkItemsWithValidationQuery");

        var provider = _providerFactory.Create();
        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);

        IEnumerable<WorkItemDto> workItems;
        if (profileAreaPaths != null && profileAreaPaths.Count > 0)
        {
            _logger.LogDebug("Filtering work items by active profile area paths for validation");
            workItems = await provider.GetByAreaPathsAsync(profileAreaPaths, cancellationToken);
        }
        else
        {
            workItems = await provider.GetAllAsync(cancellationToken);
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
