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
    private readonly WorkItemReadProviderFactory _providerFactory;
    private readonly ILogger<GetDistinctAreaPathsQueryHandler> _logger;

    public GetDistinctAreaPathsQueryHandler(
        WorkItemReadProviderFactory providerFactory,
        ILogger<GetDistinctAreaPathsQueryHandler> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<string>> Handle(
        GetDistinctAreaPathsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetDistinctAreaPathsQuery");

        var provider = _providerFactory.Create();
        var workItems = await provider.GetAllAsync(cancellationToken);

        var distinctAreaPaths = workItems
            .Select(wi => wi.AreaPath)
            .Distinct()
            .OrderBy(ap => ap)
            .ToList();

        _logger.LogDebug("Found {Count} distinct area paths", distinctAreaPaths.Count);

        return distinctAreaPaths;
    }
}
