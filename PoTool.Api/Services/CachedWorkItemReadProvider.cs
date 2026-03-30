using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Cached work item read provider that reads from the local database.
/// Used when DataSourceMode is Cache (after sync).
/// </summary>
public sealed class CachedWorkItemReadProvider : IWorkItemReadProvider
{
    private readonly IWorkItemQuery _workItemQuery;
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<CachedWorkItemReadProvider> _logger;

    public CachedWorkItemReadProvider(
        IWorkItemQuery workItemQuery,
        PoToolDbContext dbContext,
        ILogger<CachedWorkItemReadProvider> logger)
    {
        _workItemQuery = workItemQuery;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all work items from the local cache.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedWorkItemReadProvider: Fetching all work items from cache");
        return await _workItemQuery.GetAllAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves work items matching the specified filter from the cache.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedWorkItemReadProvider: Fetching filtered work items with filter: {Filter}", filter);

        if (string.IsNullOrWhiteSpace(filter))
        {
            return await GetAllAsync(cancellationToken);
        }

        var entities = await _dbContext.WorkItems
            .AsNoTracking()
            .Where(wi => EF.Functions.Like(wi.Title, $"%{filter}%"))
            .ToListAsync(cancellationToken);

        return entities.Select(WorkItemQueryMapping.MapToDto);
    }

    /// <summary>
    /// Retrieves work items matching the specified area paths from the cache.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedWorkItemReadProvider: Fetching work items by area paths: {AreaPaths}", 
            string.Join(", ", areaPaths));
        return areaPaths == null
            ? Array.Empty<WorkItemDto>()
            : await _workItemQuery.GetByAreaPathsAsync(areaPaths, cancellationToken);
    }

    /// <summary>
    /// Retrieves a work item by its TFS ID from the cache.
    /// </summary>
    public async Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedWorkItemReadProvider: Fetching work item by ID: {TfsId}", tfsId);

        var entity = await _dbContext.WorkItems
            .AsNoTracking()
            .OrderBy(wi => wi.TfsId)
            .FirstOrDefaultAsync(wi => wi.TfsId == tfsId, cancellationToken);

        return entity != null ? WorkItemQueryMapping.MapToDto(entity) : null;
    }

    /// <summary>
    /// Retrieves work items starting from specified root work item IDs and their entire hierarchy.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetByRootIdsAsync(int[] rootWorkItemIds, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedWorkItemReadProvider: Fetching work items by root IDs: {RootIds}", 
            string.Join(", ", rootWorkItemIds));
        if (rootWorkItemIds == null || rootWorkItemIds.Length == 0)
        {
            _logger.LogWarning("CachedWorkItemReadProvider: No root work item IDs provided, returning empty collection");
            return Array.Empty<WorkItemDto>();
        }

        var result = await _workItemQuery.GetByRootIdsAsync(rootWorkItemIds, cancellationToken);
        _logger.LogDebug("CachedWorkItemReadProvider: Found {Count} work items for roots {RootIds}",
            result.Count, string.Join(", ", rootWorkItemIds));
        return result;
    }
}
