using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
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

    public async Task<IReadOnlyList<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entities = await _context.WorkItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities
            .Select(WorkItemQueryMapping.MapToDto)
            .ToList();
    }

    public async Task<IReadOnlyList<WorkItemDto>> GetByAreaPathsAsync(
        IReadOnlyList<string> areaPaths,
        CancellationToken cancellationToken)
    {
        if (areaPaths.Count == 0)
        {
            return Array.Empty<WorkItemDto>();
        }

        var normalizedAreaPaths = areaPaths
            .Where(areaPath => !string.IsNullOrWhiteSpace(areaPath))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedAreaPaths.Length == 0)
        {
            return Array.Empty<WorkItemDto>();
        }

        var entities = await _context.WorkItems
            .AsNoTracking()
            .Where(workItem => normalizedAreaPaths.Any(areaPath => workItem.AreaPath.StartsWith(areaPath)))
            .ToListAsync(cancellationToken);

        return entities
            .Select(WorkItemQueryMapping.MapToDto)
            .ToList();
    }

    public async Task<IReadOnlyList<WorkItemDto>> GetByRootIdsAsync(
        IReadOnlyList<int> rootWorkItemIds,
        CancellationToken cancellationToken)
    {
        if (rootWorkItemIds.Count == 0)
        {
            return Array.Empty<WorkItemDto>();
        }

        var normalizedRootIds = rootWorkItemIds
            .Distinct()
            .ToArray();

        var allEntities = await _context.WorkItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var includedIds = new HashSet<int>(normalizedRootIds);

        bool changed;
        do
        {
            changed = false;
            foreach (var entity in allEntities)
            {
                if (entity.ParentTfsId.HasValue
                    && includedIds.Contains(entity.ParentTfsId.Value)
                    && includedIds.Add(entity.TfsId))
                {
                    changed = true;
                }
            }
        } while (changed);

        return allEntities
            .Where(entity => includedIds.Contains(entity.TfsId))
            .Select(WorkItemQueryMapping.MapToDto)
            .ToList();
    }
}
