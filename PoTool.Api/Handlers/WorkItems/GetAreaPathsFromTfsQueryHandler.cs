using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;
using PoTool.Api.Services;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAreaPathsFromTfsQuery.
/// Fetches area paths directly from TFS, bypassing the cache.
/// Used specifically for the Add Profile flow where cache is not yet populated.
/// </summary>
public sealed class GetAreaPathsFromTfsQueryHandler : IQueryHandler<GetAreaPathsFromTfsQuery, IEnumerable<string>>
{
    private readonly ITfsClient _tfsClient;
    private readonly TfsConfigurationService _configService;
    private readonly ILogger<GetAreaPathsFromTfsQueryHandler> _logger;

    public GetAreaPathsFromTfsQueryHandler(
        ITfsClient tfsClient,
        TfsConfigurationService configService,
        ILogger<GetAreaPathsFromTfsQueryHandler> logger)
    {
        _tfsClient = tfsClient;
        _configService = configService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<string>> Handle(
        GetAreaPathsFromTfsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching area paths directly from TFS (cache bypass for Add Profile flow)");

        // Get the configured default area path from TFS config
        var config = await _configService.GetConfigEntityAsync(cancellationToken);
        
        if (config == null || string.IsNullOrWhiteSpace(config.DefaultAreaPath))
        {
            _logger.LogWarning("No TFS configuration or default area path found");
            return Enumerable.Empty<string>();
        }

        try
        {
            // Fetch work items from TFS using the default area path
            // Cache is intentionally bypassed here because it's not yet populated in the Add Profile flow
            var workItems = await _tfsClient.GetWorkItemsAsync(config.DefaultAreaPath, cancellationToken);
            
            // Extract distinct area paths from the fetched work items
            var distinctAreaPaths = workItems
                .Select(wi => wi.AreaPath)
                .Distinct()
                .OrderBy(ap => ap)
                .ToList();

            _logger.LogDebug("Found {Count} distinct area paths from TFS", distinctAreaPaths.Count);
            
            return distinctAreaPaths;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching area paths from TFS");
            // Return empty list on error to prevent breaking the UI
            return Enumerable.Empty<string>();
        }
    }
}
