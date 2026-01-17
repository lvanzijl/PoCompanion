using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetFilteredWorkItemsQuery.
/// Automatically filters by active profile's area paths if a profile is set.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetFilteredWorkItemsQueryHandler : IQueryHandler<GetFilteredWorkItemsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly ProfileFilterService _profileFilterService;
    private readonly ILogger<GetFilteredWorkItemsQueryHandler> _logger;

    public GetFilteredWorkItemsQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        ProfileFilterService profileFilterService,
        ILogger<GetFilteredWorkItemsQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetFilteredWorkItemsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetFilteredWorkItemsQuery with filter={Filter}", query.Filter);

        // Live-only mode: use injected provider directly
        var allFiltered = await _workItemReadProvider.GetFilteredAsync(query.Filter, cancellationToken);

        // Apply profile-based area path filtering
        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);

        if (profileAreaPaths != null && profileAreaPaths.Count > 0)
        {
            _logger.LogDebug("Applying active profile area path filter: {AreaPaths}",
                string.Join(", ", profileAreaPaths));

            return allFiltered.Where(wi =>
                _profileFilterService.MatchesAreaPathFilter(wi.AreaPath, profileAreaPaths));
        }

        return allFiltered;
    }
}
