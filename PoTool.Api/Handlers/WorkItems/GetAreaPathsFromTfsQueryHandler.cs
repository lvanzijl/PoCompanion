using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAreaPathsFromTfsQuery.
/// Retrieves all area paths directly from TFS/Azure DevOps server.
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
        _logger.LogDebug("Handling GetAreaPathsFromTfsQuery");
        
        var areaPaths = await _tfsClient.GetAreaPathsAsync(cancellationToken);
        
        _logger.LogDebug("Retrieved {Count} area paths from TFS", areaPaths.Count());
        
        return areaPaths;
    }
}
