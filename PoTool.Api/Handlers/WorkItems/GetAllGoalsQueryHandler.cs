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
    private readonly ILogger<GetAllGoalsQueryHandler> _logger;

    public GetAllGoalsQueryHandler(
        IWorkItemQuery workItemQuery,
        ProfileFilterService profileFilterService,
        ILogger<GetAllGoalsQueryHandler> logger)
    {
        _workItemQuery = workItemQuery;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetAllGoalsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllGoalsQuery");
        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);
        return await _workItemQuery.GetGoalsForListingAsync(profileAreaPaths, cancellationToken);
    }
}
