using System.Text.Json;
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
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<CachedWorkItemReadProvider> _logger;

    public CachedWorkItemReadProvider(
        PoToolDbContext dbContext,
        ILogger<CachedWorkItemReadProvider> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all work items from the local cache.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedWorkItemReadProvider: Fetching all work items from cache");

        var entities = await _dbContext.WorkItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
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

        return entities.Select(MapToDto);
    }

    /// <summary>
    /// Retrieves work items matching the specified area paths from the cache.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedWorkItemReadProvider: Fetching work items by area paths: {AreaPaths}", 
            string.Join(", ", areaPaths));

        if (areaPaths == null || areaPaths.Count == 0)
        {
            return Enumerable.Empty<WorkItemDto>();
        }

        // Build query for hierarchical area path matching
        var query = _dbContext.WorkItems.AsNoTracking();
        
        // Filter by area paths (any that start with the specified paths)
        var entities = await query
            .Where(wi => areaPaths.Any(ap => wi.AreaPath.StartsWith(ap)))
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
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

        return entity != null ? MapToDto(entity) : null;
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
            return Enumerable.Empty<WorkItemDto>();
        }

        // Load all work items from cache (cache is already filtered during sync)
        var allEntities = await _dbContext.WorkItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Build a set of TfsIds to include (roots + all descendants)
        var includedIds = new HashSet<int>(rootWorkItemIds);
        var entityLookup = allEntities.ToDictionary(e => e.TfsId);
        
        // Find all descendants iteratively
        bool changed;
        do
        {
            changed = false;
            foreach (var entity in allEntities)
            {
                if (entity.ParentTfsId.HasValue && 
                    includedIds.Contains(entity.ParentTfsId.Value) && 
                    !includedIds.Contains(entity.TfsId))
                {
                    includedIds.Add(entity.TfsId);
                    changed = true;
                }
            }
        } while (changed);

        // Filter and return
        var result = allEntities
            .Where(e => includedIds.Contains(e.TfsId))
            .Select(MapToDto)
            .ToList();

        _logger.LogDebug("CachedWorkItemReadProvider: Found {Count} work items for roots {RootIds}", 
            result.Count, string.Join(", ", rootWorkItemIds));

        return result;
    }

    private static WorkItemDto MapToDto(Persistence.Entities.WorkItemEntity entity)
    {
        List<WorkItemRelation>? relations = null;
        if (!string.IsNullOrEmpty(entity.Relations))
        {
            try
            {
                relations = System.Text.Json.JsonSerializer.Deserialize<List<WorkItemRelation>>(entity.Relations);
            }
            catch (System.Text.Json.JsonException)
            {
                // Ignore deserialization errors
            }
        }

        return new WorkItemDto(
            TfsId: entity.TfsId,
            Type: entity.Type,
            Title: entity.Title,
            ParentTfsId: entity.ParentTfsId,
            AreaPath: entity.AreaPath,
            IterationPath: entity.IterationPath,
            State: entity.State,
            RetrievedAt: entity.RetrievedAt,
            Effort: entity.Effort,
            Description: entity.Description,
            CreatedDate: entity.CreatedDate,
            ClosedDate: entity.ClosedDate,
            Severity: entity.Severity,
            Tags: entity.Tags,
            IsBlocked: entity.IsBlocked,
            Relations: relations,
            ChangedDate: entity.TfsChangedDate,
            BusinessValue: entity.BusinessValue,
            BacklogPriority: entity.BacklogPriority
        );
    }
}
