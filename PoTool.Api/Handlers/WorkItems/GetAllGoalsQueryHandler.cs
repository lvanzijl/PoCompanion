using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllGoalsQuery.
/// Retrieves all work items of type Goal, filtered by active profile if set.
/// </summary>
public sealed class GetAllGoalsQueryHandler : IQueryHandler<GetAllGoalsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemRepository _repository;
    private readonly ProfileFilterService _profileFilterService;
    private readonly ILogger<GetAllGoalsQueryHandler> _logger;

    public GetAllGoalsQueryHandler(
        IWorkItemRepository repository,
        ProfileFilterService profileFilterService,
        ILogger<GetAllGoalsQueryHandler> logger)
    {
        _repository = repository;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetAllGoalsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllGoalsQuery");
        
        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);
        
        IEnumerable<WorkItemDto> allWorkItems;
        if (profileAreaPaths != null && profileAreaPaths.Count > 0)
        {
            _logger.LogDebug("Filtering goals by active profile area paths");
            allWorkItems = await _repository.GetByAreaPathsAsync(profileAreaPaths, cancellationToken);
        }
        else
        {
            allWorkItems = await _repository.GetAllAsync(cancellationToken);
        }
        
        return allWorkItems.Where(wi => wi.Type == WorkItemType.Goal).ToList();
    }
}
