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
        var entities = await _context.WorkItems
            .AsNoTracking()
            .Where(w => w.Title.Contains(filter))
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
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
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Remove all existing work items
            await _context.WorkItems.ExecuteDeleteAsync(cancellationToken);

            // Add new work items
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

    private static WorkItemDto MapToDto(WorkItemEntity entity)
    {
        return new WorkItemDto(
            TfsId: entity.TfsId,
            Type: entity.Type,
            Title: entity.Title,
            AreaPath: entity.AreaPath,
            IterationPath: entity.IterationPath,
            State: entity.State,
            JsonPayload: entity.JsonPayload,
            RetrievedAt: entity.RetrievedAt
        );
    }

    private static WorkItemEntity MapToEntity(WorkItemDto dto)
    {
        return new WorkItemEntity
        {
            TfsId = dto.TfsId,
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
