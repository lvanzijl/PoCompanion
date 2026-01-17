using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Live work item read provider that queries TFS directly without cache.
/// Used when DataSourceMode is Live.
/// DTOs only - no persistence, no EF, no repositories.
/// </summary>
public sealed class LiveWorkItemReadProvider : IWorkItemReadProvider
{
    private readonly ITfsClient _tfsClient;
    private readonly TfsConfigurationService _configService;
    private readonly ILogger<LiveWorkItemReadProvider> _logger;

    public LiveWorkItemReadProvider(
        ITfsClient tfsClient,
        TfsConfigurationService configService,
        ILogger<LiveWorkItemReadProvider> logger)
    {
        _tfsClient = tfsClient;
        _configService = configService;
        _logger = logger;
    }

    public async Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LiveWorkItemReadProvider: Fetching all work items from TFS");

        // Get the configured area path from TFS settings
        var config = await _configService.GetConfigAsync(cancellationToken);
        if (config == null || string.IsNullOrWhiteSpace(config.DefaultAreaPath))
        {
            _logger.LogWarning("LiveWorkItemReadProvider: No TFS configuration or area path found, returning empty collection");
            return Enumerable.Empty<WorkItemDto>();
        }

        // Fetch work items directly from TFS
        var workItems = await _tfsClient.GetWorkItemsAsync(config.DefaultAreaPath, cancellationToken);
        return workItems;
    }

    public async Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LiveWorkItemReadProvider: Fetching filtered work items from TFS with filter: {Filter}", filter);

        // Get all work items first, then filter in-memory
        // This is acceptable for Live mode as we're not hitting cache
        var allWorkItems = await GetAllAsync(cancellationToken);
        
        if (string.IsNullOrWhiteSpace(filter))
        {
            return allWorkItems;
        }

        // Filter by title (case-insensitive)
        return allWorkItems.Where(wi => 
            wi.Title?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true);
    }

    public async Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LiveWorkItemReadProvider: Fetching work items by area paths from TFS: {AreaPaths}", 
            string.Join(", ", areaPaths));

        if (areaPaths == null || areaPaths.Count == 0)
        {
            return Enumerable.Empty<WorkItemDto>();
        }

        // For Live mode, fetch all work items and filter by area paths
        // In a future optimization, this could query TFS for each area path
        var allWorkItems = await GetAllAsync(cancellationToken);
        
        // Filter by area paths (hierarchical matching)
        return allWorkItems.Where(wi => 
            areaPaths.Any(areaPath => 
                wi.AreaPath?.StartsWith(areaPath, StringComparison.OrdinalIgnoreCase) == true));
    }

    public async Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LiveWorkItemReadProvider: Fetching work item by ID from TFS: {TfsId}", tfsId);

        // Use the direct TFS API to get a single work item
        return await _tfsClient.GetWorkItemByIdAsync(tfsId, cancellationToken);
    }
}
