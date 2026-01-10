using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetDistinctAreaPathsQuery.
/// Returns all distinct area paths from cached work items.
/// </summary>
public sealed class GetDistinctAreaPathsQueryHandler : IQueryHandler<GetDistinctAreaPathsQuery, IEnumerable<string>>
{
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<GetDistinctAreaPathsQueryHandler> _logger;

    public GetDistinctAreaPathsQueryHandler(
        IWorkItemRepository repository,
        ILogger<GetDistinctAreaPathsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<string>> Handle(
        GetDistinctAreaPathsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetDistinctAreaPathsQuery");

        var workItems = await _repository.GetAllAsync(cancellationToken);

        var distinctAreaPaths = workItems
            .Select(wi => wi.AreaPath)
            .Distinct()
            .OrderBy(ap => ap)
            .ToList();

        _logger.LogDebug("Found {Count} distinct area paths", distinctAreaPaths.Count);

        return distinctAreaPaths;
    }
}
