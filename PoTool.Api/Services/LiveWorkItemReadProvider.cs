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

    /// <summary>
    /// Retrieves all work items from the configured TFS area path.
    /// WARNING: This loads all work items from the configured area path without product filtering.
    /// Prefer using GetByRootIdsAsync() for product-scoped loading when products are configured.
    /// Only use this method for:
    /// - Discovery operations (area path selection, iteration selection)
    /// - Fallback when no products are configured
    /// - Legacy compatibility scenarios
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("LiveWorkItemReadProvider.{Method} called — may indicate cache bypass", nameof(GetAllAsync));
        _logger.LogDebug("LiveWorkItemReadProvider: Fetching all work items from TFS");

        // Get the configured area path from TFS settings
        var config = await _configService.GetConfigAsync(cancellationToken);
        if (config == null || string.IsNullOrWhiteSpace(config.Project))
        {
            _logger.LogWarning("LiveWorkItemReadProvider: No TFS configuration or project found, returning empty collection");
            return Enumerable.Empty<WorkItemDto>();
        }

        // Use DefaultAreaPath from config, which is derived from Project name (root area path)
        var areaPath = config.DefaultAreaPath ?? config.Project;
        
        // Fetch work items directly from TFS
        var workItems = await _tfsClient.GetWorkItemsAsync(areaPath, cancellationToken);
        return workItems;
    }

    public async Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("LiveWorkItemReadProvider.{Method} called — may indicate cache bypass", nameof(GetFilteredAsync));
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
        _logger.LogWarning("LiveWorkItemReadProvider.{Method} called — may indicate cache bypass", nameof(GetByAreaPathsAsync));
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
        _logger.LogWarning("LiveWorkItemReadProvider.{Method} called — may indicate cache bypass", nameof(GetByTfsIdAsync));
        _logger.LogDebug("LiveWorkItemReadProvider: Fetching work item by ID from TFS: {TfsId}", tfsId);

        // Use the direct TFS API to get a single work item
        return await _tfsClient.GetWorkItemByIdAsync(tfsId, cancellationToken);
    }

    public async Task<IEnumerable<WorkItemDto>> GetByRootIdsAsync(int[] rootWorkItemIds, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("LiveWorkItemReadProvider.{Method} called — may indicate cache bypass", nameof(GetByRootIdsAsync));
        _logger.LogDebug("LiveWorkItemReadProvider: Fetching work items by root IDs from TFS: {RootIds}", 
            string.Join(", ", rootWorkItemIds));

        if (rootWorkItemIds == null || rootWorkItemIds.Length == 0)
        {
            _logger.LogWarning("LiveWorkItemReadProvider: No root work item IDs provided, returning empty collection");
            return Enumerable.Empty<WorkItemDto>();
        }

        // Use the hierarchical loading method from TFS client
        // This fetches the complete tree starting from the specified roots
        var workItems = await _tfsClient.GetWorkItemsByRootIdsAsync(rootWorkItemIds, null, null, cancellationToken);
        return workItems;
    }
}
