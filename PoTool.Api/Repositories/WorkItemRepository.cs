using Microsoft.EntityFrameworkCore;
using PoTool.Api.Helpers;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

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
        var sanitizedFilter = InputValidator.SanitizeFilter(filter);

        var q = _context.WorkItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(sanitizedFilter))
        {
            q = q.Where(w => w.Title.Contains(sanitizedFilter));
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

        // Validate all area paths
        var invalidPaths = areaPaths.Where(ap => !InputValidator.IsValidAreaPath(ap)).ToList();
        if (invalidPaths.Any())
        {
            throw new ArgumentException($"Invalid area path(s): {string.Join(", ", invalidPaths)}");
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
            .OrderBy(w => w.TfsId)
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

    public async Task UpsertManyAsync(IEnumerable<WorkItemDto> workItems, CancellationToken cancellationToken = default)
    {
        var workItemList = workItems.ToList();
        if (workItemList.Count == 0) return;

        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        if (isInMemory)
        {
            // InMemory: simpler approach
            foreach (var dto in workItemList)
            {
                var existing = await _context.WorkItems.OrderBy(w => w.TfsId).FirstOrDefaultAsync(w => w.TfsId == dto.TfsId, cancellationToken);
                if (existing != null)
                {
                    // Update existing
                    existing.ParentTfsId = dto.ParentTfsId;
                    existing.Type = dto.Type;
                    existing.Title = dto.Title;
                    existing.AreaPath = dto.AreaPath;
                    existing.IterationPath = dto.IterationPath;
                    existing.State = dto.State;
                    existing.RetrievedAt = dto.RetrievedAt;
                    existing.Effort = dto.Effort;
                    existing.StoryPoints = dto.StoryPoints;
                    existing.BusinessValue = dto.BusinessValue;
                    existing.Description = dto.Description;
                    existing.CreatedDate = dto.CreatedDate;
                    existing.ClosedDate = dto.ClosedDate;
                    existing.Severity = dto.Severity;
                    existing.Tags = dto.Tags;
                    existing.IsBlocked = dto.IsBlocked;
                    existing.Relations = dto.Relations != null ? System.Text.Json.JsonSerializer.Serialize(dto.Relations) : null;
                    var changedDate = dto.ChangedDate ?? dto.RetrievedAt;
                    existing.TfsChangedDate = changedDate;
                    existing.TfsChangedDateUtc = changedDate.UtcDateTime;
                    existing.BacklogPriority = dto.BacklogPriority;
                }
                else
                {
                    // Insert new
                    await _context.WorkItems.AddAsync(MapToEntity(dto), cancellationToken);
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // For SQLite: use upsert pattern
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Get existing IDs for this batch
                var tfsIds = workItemList.Select(w => w.TfsId).ToList();
                var existingIds = await _context.WorkItems
                    .Where(w => tfsIds.Contains(w.TfsId))
                    .Select(w => w.TfsId)
                    .ToListAsync(cancellationToken);

                var toUpdate = workItemList.Where(w => existingIds.Contains(w.TfsId)).ToList();
                var toInsert = workItemList.Where(w => !existingIds.Contains(w.TfsId)).ToList();

                // Update existing items
                foreach (var dto in toUpdate)
                {
                    var entity = await _context.WorkItems.OrderBy(w => w.TfsId).FirstAsync(w => w.TfsId == dto.TfsId, cancellationToken);
                    entity.ParentTfsId = dto.ParentTfsId;
                    entity.Type = dto.Type;
                    entity.Title = dto.Title;
                    entity.AreaPath = dto.AreaPath;
                    entity.IterationPath = dto.IterationPath;
                    entity.State = dto.State;
                    entity.RetrievedAt = dto.RetrievedAt;
                    entity.Effort = dto.Effort;
                    entity.StoryPoints = dto.StoryPoints;
                    entity.BusinessValue = dto.BusinessValue;
                    entity.Description = dto.Description;
                    entity.CreatedDate = dto.CreatedDate;
                    entity.ClosedDate = dto.ClosedDate;
                    entity.Severity = dto.Severity;
                    entity.Tags = dto.Tags;
                    entity.IsBlocked = dto.IsBlocked;
                    entity.Relations = dto.Relations != null ? System.Text.Json.JsonSerializer.Serialize(dto.Relations) : null;
                    var changedDate = dto.ChangedDate ?? dto.RetrievedAt;
                    entity.TfsChangedDate = changedDate;
                    entity.TfsChangedDateUtc = changedDate.UtcDateTime;
                    entity.BacklogPriority = dto.BacklogPriority;
                }

                // Insert new items
                if (toInsert.Count > 0)
                {
                    await _context.WorkItems.AddRangeAsync(toInsert.Select(MapToEntity), cancellationToken);
                }

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
        List<WorkItemRelation>? relations = null;
        if (!string.IsNullOrEmpty(entity.Relations))
        {
            try
            {
                relations = System.Text.Json.JsonSerializer.Deserialize<List<WorkItemRelation>>(entity.Relations);
            }
            catch (System.Text.Json.JsonException)
            {
                // Ignore deserialization errors - return null relations
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
                BacklogPriority: entity.BacklogPriority,
                StoryPoints: entity.StoryPoints
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
            RetrievedAt = dto.RetrievedAt,
            Effort = dto.Effort,
            StoryPoints = dto.StoryPoints,
            BusinessValue = dto.BusinessValue,
            Description = dto.Description,
            CreatedDate = dto.CreatedDate,
            ClosedDate = dto.ClosedDate,
            Severity = dto.Severity,
            Tags = dto.Tags,
            IsBlocked = dto.IsBlocked,
            Relations = dto.Relations != null ? System.Text.Json.JsonSerializer.Serialize(dto.Relations) : null,
            TfsChangedDate = dto.ChangedDate ?? dto.RetrievedAt,
            TfsChangedDateUtc = (dto.ChangedDate ?? dto.RetrievedAt).UtcDateTime,
            BacklogPriority = dto.BacklogPriority
        };
    }
}
