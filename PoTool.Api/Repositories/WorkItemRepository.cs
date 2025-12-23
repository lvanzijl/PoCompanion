using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for work item persistence.
/// </summary>
public class WorkItemRepository : IWorkItemRepository
{
    private readonly PoToolDbContext _context;

    public WorkItemRepository(PoToolDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.WorkItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    public async Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
    {
        var q = _context.WorkItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            q = q.Where(w => w.Title.Contains(filter));
        }

        var entities = await q.ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    public async Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default)
    {
        if (areaPaths == null || areaPaths.Count == 0)
        {
            return await GetAllAsync(cancellationToken);
        }

        var entities = await _context.WorkItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Filter using hierarchical area path matching
        var filtered = entities.Where(e => 
            areaPaths.Any(profilePath => 
                e.AreaPath.Equals(profilePath, StringComparison.OrdinalIgnoreCase) ||
                e.AreaPath.StartsWith(profilePath + "\\", StringComparison.OrdinalIgnoreCase)));

        return filtered.Select(MapToDto);
    }

    public async Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TfsId == tfsId, cancellationToken);

        return entity != null ? MapToDto(entity) : null;
    }

    public async Task ReplaceAllAsync(IEnumerable<WorkItemDto> workItems, CancellationToken cancellationToken = default)
    {
        // Check if we're using InMemory database (for testing)
        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        
        if (isInMemory)
        {
            // InMemory doesn't support transactions or ExecuteDelete
            var existingItems = await _context.WorkItems.ToListAsync(cancellationToken);
            _context.WorkItems.RemoveRange(existingItems);
            
            var entities = workItems.Select(MapToEntity);
            await _context.WorkItems.AddRangeAsync(entities, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Use transaction for real databases
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            
            try
            {
                await _context.WorkItems.ExecuteDeleteAsync(cancellationToken);
                var entities = workItems.Select(MapToEntity);
                await _context.WorkItems.AddRangeAsync(entities, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    private static WorkItemDto MapToDto(WorkItemEntity entity)
    {
        return new WorkItemDto(
            TfsId: entity.TfsId,
            Type: entity.Type,
            Title: entity.Title,
            ParentTfsId: entity.ParentTfsId,
            AreaPath: entity.AreaPath,
            IterationPath: entity.IterationPath,
            State: entity.State,
            JsonPayload: entity.JsonPayload,
            RetrievedAt: entity.RetrievedAt,
            Effort: entity.Effort
        );
    }

    private static WorkItemEntity MapToEntity(WorkItemDto dto)
    {
        return new WorkItemEntity
        {
            TfsId = dto.TfsId,
            ParentTfsId = dto.ParentTfsId,
            Type = dto.Type,
            Title = dto.Title,
            AreaPath = dto.AreaPath,
            IterationPath = dto.IterationPath,
            State = dto.State,
            JsonPayload = dto.JsonPayload,
            RetrievedAt = dto.RetrievedAt
        };
    }
}
