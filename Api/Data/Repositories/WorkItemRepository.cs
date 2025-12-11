using Api.Data.Entities;
using Core.Contracts;
using Core.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.Repositories;

/// <summary>
/// Repository implementation for work item persistence.
/// </summary>
public sealed class WorkItemRepository : IWorkItemRepository
{
    private readonly ApplicationDbContext _context;

    public WorkItemRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyCollection<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.WorkItems
            .OrderBy(w => w.AreaPath)
            .ThenBy(w => w.TfsId)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    public async Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.WorkItems
            .FirstOrDefaultAsync(w => w.TfsId == tfsId, cancellationToken);

        return entity is not null ? MapToDto(entity) : null;
    }

    public async Task<IReadOnlyCollection<WorkItemDto>> GetByAreaPathAsync(
        string areaPath,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.WorkItems
            .Where(w => w.AreaPath.StartsWith(areaPath))
            .OrderBy(w => w.AreaPath)
            .ThenBy(w => w.TfsId)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    public async Task ReplaceAllAsync(
        IEnumerable<WorkItemDto> workItems,
        CancellationToken cancellationToken = default)
    {
        // Atomic replacement: delete all, then insert new
        await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Clear existing work items
            _context.WorkItems.RemoveRange(_context.WorkItems);
            await _context.SaveChangesAsync(cancellationToken);

            // Insert new work items
            var entities = workItems.Select(MapToEntity).ToList();
            await _context.WorkItems.AddRangeAsync(entities, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await _context.Database.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _context.Database.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<DateTimeOffset?> GetLastUpdateTimestampAsync(CancellationToken cancellationToken = default)
    {
        return await _context.WorkItems
            .MaxAsync(w => (DateTimeOffset?)w.RetrievedAt, cancellationToken);
    }

    private static WorkItemDto MapToDto(WorkItemEntity entity)
    {
        return new WorkItemDto
        {
            TfsId = entity.TfsId,
            Type = entity.Type,
            Title = entity.Title,
            AreaPath = entity.AreaPath,
            IterationPath = entity.IterationPath,
            State = entity.State,
            JsonPayload = entity.JsonPayload,
            RetrievedAt = entity.RetrievedAt
        };
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
