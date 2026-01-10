using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAreaPathsFromTfsQuery.
/// Fetches area paths directly from TFS using the Classification Nodes API.
/// Used specifically for the Add Profile flow where cache is not yet populated.
/// </summary>
public sealed class GetAreaPathsFromTfsQueryHandler : IQueryHandler<GetAreaPathsFromTfsQuery, IEnumerable<string>>
{
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<GetAreaPathsFromTfsQueryHandler> _logger;

    public GetAreaPathsFromTfsQueryHandler(
        ITfsClient tfsClient,
        ILogger<GetAreaPathsFromTfsQueryHandler> logger)
    {
        _tfsClient = tfsClient;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<string>> Handle(
        GetAreaPathsFromTfsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching area paths directly from TFS Classification Nodes API");

        try
        {
            // Fetch area paths directly from TFS Classification Nodes API
            // This bypasses the work item cache and provides the current area path structure
            var areaPaths = await _tfsClient.GetAreaPathsAsync(depth: null, cancellationToken);

            var areaPathsList = areaPaths.ToList();
            _logger.LogDebug("Retrieved {Count} area paths from TFS Classification Nodes API", areaPathsList.Count);

            return areaPathsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching area paths from TFS Classification Nodes API");
            // Return empty list on error to prevent breaking the UI
            return Enumerable.Empty<string>();
        }
    }
}
