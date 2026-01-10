using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllWorkItemsQuery.
/// Automatically filters by active profile's area paths if a profile is set.
/// </summary>
public sealed class GetAllWorkItemsQueryHandler : IQueryHandler<GetAllWorkItemsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemRepository _repository;
    private readonly ProfileFilterService _profileFilterService;
    private readonly ILogger<GetAllWorkItemsQueryHandler> _logger;

    public GetAllWorkItemsQueryHandler(
        IWorkItemRepository repository,
        ProfileFilterService profileFilterService,
        ILogger<GetAllWorkItemsQueryHandler> logger)
    {
        _repository = repository;
        _profileFilterService = profileFilterService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetAllWorkItemsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllWorkItemsQuery");

        var profileAreaPaths = await _profileFilterService.GetActiveProfileAreaPathsAsync(cancellationToken);

        if (profileAreaPaths != null && profileAreaPaths.Count > 0)
        {
            _logger.LogDebug("Filtering work items by active profile area paths: {AreaPaths}",
                string.Join(", ", profileAreaPaths));
            return await _repository.GetByAreaPathsAsync(profileAreaPaths, cancellationToken);
        }

        return await _repository.GetAllAsync(cancellationToken);
    }
}
