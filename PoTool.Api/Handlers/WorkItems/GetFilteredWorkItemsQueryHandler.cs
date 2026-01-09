using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetFilteredWorkItemsQuery.
/// Automatically filters by active profile's area paths if a profile is set.
/// </summary>
public sealed class GetFilteredWorkItemsQueryHandler : IQueryHandler<GetFilteredWorkItemsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemRepository _repository;
    private readonly ProfileFilterService _profileFilterService;
    private readonly ILogger<GetFilteredWorkItemsQueryHandler> _logger;

    public GetFilteredWorkItemsQueryHandler(
        IWorkItemRepository repository,
        ProfileFilterService profileFilterService,
        ILogger<GetFilteredWorkItemsQueryHandler> logger)
    {
        _repository = repository;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetFilteredWorkItemsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetFilteredWorkItemsQuery with filter={Filter}", query.Filter);
        
        var allFiltered = await _repository.GetFilteredAsync(query.Filter, cancellationToken);
        
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
