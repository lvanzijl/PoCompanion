using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllWorkItemsQuery.
/// Automatically filters by active profile's area paths if a profile is set.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetAllWorkItemsQueryHandler : IQueryHandler<GetAllWorkItemsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly ProfileFilterService _profileFilterService;
    private readonly ILogger<GetAllWorkItemsQueryHandler> _logger;

    public GetAllWorkItemsQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        ProfileFilterService profileFilterService,
        ILogger<GetAllWorkItemsQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetAllWorkItemsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllWorkItemsQuery");

        // Live-only mode: use injected provider directly
        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);

        if (profileAreaPaths != null && profileAreaPaths.Count > 0)
        {
            _logger.LogDebug("Filtering work items by active profile area paths: {AreaPaths}",
                string.Join(", ", profileAreaPaths));
            return await _workItemReadProvider.GetByAreaPathsAsync(profileAreaPaths, cancellationToken);
        }

        return await _workItemReadProvider.GetAllAsync(cancellationToken);
    }
}
