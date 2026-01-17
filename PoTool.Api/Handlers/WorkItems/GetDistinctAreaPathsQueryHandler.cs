using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetDistinctAreaPathsQuery.
/// Returns all distinct area paths from work items.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetDistinctAreaPathsQueryHandler : IQueryHandler<GetDistinctAreaPathsQuery, IEnumerable<string>>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly ILogger<GetDistinctAreaPathsQueryHandler> _logger;

    public GetDistinctAreaPathsQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        ILogger<GetDistinctAreaPathsQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<string>> Handle(
        GetDistinctAreaPathsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetDistinctAreaPathsQuery");

        // Live-only mode: use injected provider directly
        var workItems = await _workItemReadProvider.GetAllAsync(cancellationToken);

        var distinctAreaPaths = workItems
            .Select(wi => wi.AreaPath)
            .Distinct()
            .OrderBy(ap => ap)
            .ToList();

        _logger.LogDebug("Found {Count} distinct area paths", distinctAreaPaths.Count);

        return distinctAreaPaths;
    }
}
