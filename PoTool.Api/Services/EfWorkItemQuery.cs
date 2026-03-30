using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// EF-backed cache-only analytical Work Item query store.
/// </summary>
public sealed class EfWorkItemQuery : IWorkItemQuery
{
    private readonly PoToolDbContext _context;

    public EfWorkItemQuery(PoToolDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WorkItemDto>> GetGoalHierarchyAsync(
        IReadOnlyList<int> goalIds,
        CancellationToken cancellationToken)
    {
        var entities = await LoadHierarchyByRootIdsAsync(goalIds, cancellationToken);
        return MapToDtos(entities);
    }

    public async Task<IReadOnlyList<WorkItemDto>> GetWorkItemsForListingAsync(
        IReadOnlyList<int>? productIds,
        IReadOnlyList<string>? fallbackAreaPaths,
        CancellationToken cancellationToken)
    {
        var entities = await LoadListingScopeEntitiesAsync(productIds, fallbackAreaPaths, cancellationToken);
        return MapToDtos(entities);
    }

    public async Task<IReadOnlyList<WorkItemDto>> GetGoalsForListingAsync(
        IReadOnlyList<string>? fallbackAreaPaths,
        CancellationToken cancellationToken)
    {
        var entities = await LoadListingScopeEntitiesAsync(productIds: null, fallbackAreaPaths, cancellationToken);
        return entities
            .Where(entity => entity.Type == Core.WorkItems.WorkItemType.Goal)
            .Select(WorkItemQueryMapping.MapToDto)
            .ToList();
    }

    public async Task<DependencyGraphQuerySource> GetDependencyGraphSourceAsync(
        string? areaPathFilter,
        IReadOnlyList<int>? workItemIds,
        IReadOnlyList<string>? workItemTypes,
        CancellationToken cancellationToken)
    {
        var scopedEntities = await LoadAllProductScopedOrAllEntitiesAsync(cancellationToken);
        var scopedWorkItems = MapToDtos(scopedEntities);
        var relevantWorkItems = ApplyDependencyGraphFilters(scopedWorkItems, areaPathFilter, workItemIds, workItemTypes);

        return new DependencyGraphQuerySource(scopedWorkItems, relevantWorkItems);
    }

    public async Task<ValidationImpactQuerySource> GetValidationImpactSourceAsync(
        string? areaPathFilter,
        string? iterationPathFilter,
        CancellationToken cancellationToken)
    {
        var scopedEntities = await LoadAllProductScopedOrAllEntitiesAsync(cancellationToken);
        var filteredWorkItems = ApplyValidationImpactFilters(
            MapToDtos(scopedEntities),
            areaPathFilter,
            iterationPathFilter);

        return new ValidationImpactQuerySource(
            filteredWorkItems,
            BuildChildrenLookup(filteredWorkItems));
    }

    public async Task<ProductBacklogAnalyticsSource?> GetProductBacklogAnalyticsSourceAsync(
        int productId,
        CancellationToken cancellationToken)
    {
        var productExists = await _context.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == productId, cancellationToken);

        if (!productExists)
        {
            return null;
        }

        var rootWorkItemIds = await LoadConfiguredRootIdsAsync([productId], cancellationToken);
        if (rootWorkItemIds.Count == 0)
        {
            return new ProductBacklogAnalyticsSource(productId, Array.Empty<WorkItemDto>());
        }

        var entities = await LoadHierarchyByRootIdsAsync(rootWorkItemIds, cancellationToken);
        return new ProductBacklogAnalyticsSource(productId, MapToDtos(entities));
    }

    private async Task<IReadOnlyList<Persistence.Entities.WorkItemEntity>> LoadListingScopeEntitiesAsync(
        IReadOnlyList<int>? productIds,
        IReadOnlyList<string>? fallbackAreaPaths,
        CancellationToken cancellationToken)
    {
        var rootIds = await LoadConfiguredRootIdsAsync(productIds, cancellationToken);
        if (rootIds.Count > 0)
        {
            return await LoadHierarchyByRootIdsAsync(rootIds, cancellationToken);
        }

        var normalizedAreaPaths = NormalizeAreaPaths(fallbackAreaPaths);
        if (normalizedAreaPaths.Length > 0)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .Where(workItem => normalizedAreaPaths.Any(areaPath => workItem.AreaPath.StartsWith(areaPath)))
                .ToListAsync(cancellationToken);
        }

        return await LoadAllEntitiesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Persistence.Entities.WorkItemEntity>> LoadAllProductScopedOrAllEntitiesAsync(
        CancellationToken cancellationToken)
    {
        var rootIds = await LoadConfiguredRootIdsAsync(productIds: null, cancellationToken);
        return rootIds.Count > 0
            ? await LoadHierarchyByRootIdsAsync(rootIds, cancellationToken)
            : await LoadAllEntitiesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<int>> LoadConfiguredRootIdsAsync(
        IReadOnlyList<int>? productIds,
        CancellationToken cancellationToken)
    {
        var rootsQuery = _context.ProductBacklogRoots
            .AsNoTracking();

        if (productIds is { Count: > 0 })
        {
            var normalizedProductIds = productIds
                .Distinct()
                .ToArray();
            rootsQuery = rootsQuery.Where(root => normalizedProductIds.Contains(root.ProductId));
        }

        return await rootsQuery
            .Select(root => root.WorkItemTfsId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Persistence.Entities.WorkItemEntity>> LoadHierarchyByRootIdsAsync(
        IReadOnlyList<int> rootWorkItemIds,
        CancellationToken cancellationToken)
    {
        var normalizedRootIds = rootWorkItemIds
            .Distinct()
            .ToArray();

        if (normalizedRootIds.Length == 0)
        {
            return Array.Empty<Persistence.Entities.WorkItemEntity>();
        }

        var allEntities = await LoadAllEntitiesAsync(cancellationToken);
        var includedIds = WorkItemHierarchySelection.ExpandToDescendantIds(allEntities, normalizedRootIds);

        return allEntities
            .Where(entity => includedIds.Contains(entity.TfsId))
            .ToList();
    }

    private Task<List<Persistence.Entities.WorkItemEntity>> LoadAllEntitiesAsync(CancellationToken cancellationToken)
    {
        return _context.WorkItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyList<WorkItemDto> MapToDtos(IEnumerable<Persistence.Entities.WorkItemEntity> entities)
        => entities.Select(WorkItemQueryMapping.MapToDto).ToList();

    private static IReadOnlyList<WorkItemDto> ApplyDependencyGraphFilters(
        IReadOnlyList<WorkItemDto> scopedWorkItems,
        string? areaPathFilter,
        IReadOnlyList<int>? workItemIds,
        IReadOnlyList<string>? workItemTypes)
    {
        IEnumerable<WorkItemDto> filteredItems = scopedWorkItems;

        if (!string.IsNullOrWhiteSpace(areaPathFilter))
        {
            filteredItems = filteredItems.Where(workItem =>
                workItem.AreaPath.Contains(areaPathFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (workItemIds is { Count: > 0 })
        {
            var workItemIdSet = workItemIds.ToHashSet();
            filteredItems = filteredItems.Where(workItem => workItemIdSet.Contains(workItem.TfsId));
        }

        if (workItemTypes is { Count: > 0 })
        {
            filteredItems = filteredItems.Where(workItem =>
                workItemTypes.Contains(workItem.Type, StringComparer.OrdinalIgnoreCase));
        }

        return filteredItems.ToList();
    }

    private static IReadOnlyList<WorkItemDto> ApplyValidationImpactFilters(
        IReadOnlyList<WorkItemDto> scopedWorkItems,
        string? areaPathFilter,
        string? iterationPathFilter)
    {
        IEnumerable<WorkItemDto> filteredItems = scopedWorkItems;

        if (!string.IsNullOrWhiteSpace(areaPathFilter))
        {
            filteredItems = filteredItems.Where(workItem =>
                workItem.AreaPath.StartsWith(areaPathFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(iterationPathFilter))
        {
            filteredItems = filteredItems.Where(workItem =>
                workItem.IterationPath.Contains(iterationPathFilter, StringComparison.OrdinalIgnoreCase));
        }

        return filteredItems.ToList();
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<int>> BuildChildrenLookup(
        IReadOnlyList<WorkItemDto> workItems)
    {
        return workItems
            .Where(workItem => workItem.ParentTfsId.HasValue)
            .GroupBy(workItem => workItem.ParentTfsId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(workItem => workItem.TfsId).ToList());
    }

    private static string[] NormalizeAreaPaths(IReadOnlyList<string>? areaPaths)
    {
        if (areaPaths is not { Count: > 0 })
        {
            return [];
        }

        return areaPaths
            .Where(areaPath => !string.IsNullOrWhiteSpace(areaPath))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
