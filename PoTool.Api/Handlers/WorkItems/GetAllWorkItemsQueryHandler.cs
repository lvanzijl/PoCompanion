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
    private readonly IWorkItemQuery _workItemQuery;
    private readonly ProfileFilterService _profileFilterService;
    private readonly ILogger<GetAllWorkItemsQueryHandler> _logger;

    public GetAllWorkItemsQueryHandler(
        IWorkItemQuery workItemQuery,
        ProfileFilterService profileFilterService,
        ILogger<GetAllWorkItemsQueryHandler> logger)
    {
        _workItemQuery = workItemQuery;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetAllWorkItemsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllWorkItemsQuery");

        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);
        return await _workItemQuery.GetWorkItemsForListingAsync(
            productIds: null,
            fallbackAreaPaths: profileAreaPaths,
            cancellationToken);
    }
}
