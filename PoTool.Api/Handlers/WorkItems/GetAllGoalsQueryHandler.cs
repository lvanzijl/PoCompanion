using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllGoalsQuery.
/// Retrieves all work items of type Goal, filtered by active profile if set.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetAllGoalsQueryHandler : IQueryHandler<GetAllGoalsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly ProfileFilterService _profileFilterService;
    private readonly ILogger<GetAllGoalsQueryHandler> _logger;

    public GetAllGoalsQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        ProfileFilterService profileFilterService,
        ILogger<GetAllGoalsQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetAllGoalsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllGoalsQuery");

        // Live-only mode: use injected provider directly
        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);

        IEnumerable<WorkItemDto> allWorkItems;
        if (profileAreaPaths != null && profileAreaPaths.Count > 0)
        {
            _logger.LogDebug("Filtering goals by active profile area paths");
            allWorkItems = await _workItemReadProvider.GetByAreaPathsAsync(profileAreaPaths, cancellationToken);
        }
        else
        {
            allWorkItems = await _workItemReadProvider.GetAllAsync(cancellationToken);
        }

        return allWorkItems.Where(wi => wi.Type == WorkItemType.Goal).ToList();
    }
}
