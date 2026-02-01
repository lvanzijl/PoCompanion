using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Shared.BugTriage;

namespace PoTool.Api.Services;

/// <summary>
/// Service for managing the triage tag catalog.
/// Tags can be added, enabled/disabled, reordered, and removed.
/// </summary>
public class TriageTagService
{
    private readonly PoToolDbContext _db;
    private readonly ILogger<TriageTagService> _logger;

    public TriageTagService(
        PoToolDbContext db,
        ILogger<TriageTagService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets all triage tags, ordered by display order.
    /// </summary>
    public async Task<List<TriageTagDto>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _db.TriageTags
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets only enabled triage tags, ordered by display order.
    /// </summary>
    public async Task<List<TriageTagDto>> GetEnabledTagsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _db.TriageTags
            .Where(t => t.IsEnabled)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Creates a new triage tag.
    /// Tag name must be unique (case-insensitive).
    /// </summary>
    public async Task<TriageTagOperationResponse> CreateTagAsync(
        CreateTriageTagRequest request,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return new TriageTagOperationResponse(false, "Tag name cannot be empty");
        }

        // Check for duplicate (case-insensitive)
        var exists = await _db.TriageTags
            .AnyAsync(t => t.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            return new TriageTagOperationResponse(false, $"Tag '{trimmedName}' already exists");
        }

        // Get next display order
        var maxOrder = await _db.TriageTags.AnyAsync(cancellationToken)
            ? await _db.TriageTags.MaxAsync(t => t.DisplayOrder, cancellationToken)
            : 0;

        var entity = new TriageTagEntity
        {
            Name = trimmedName,
            IsEnabled = true,
            DisplayOrder = maxOrder + 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.TriageTags.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created triage tag: {TagName}", trimmedName);

        return new TriageTagOperationResponse(true, "Tag created successfully", MapToDto(entity));
    }

    /// <summary>
    /// Updates a triage tag.
    /// </summary>
    public async Task<TriageTagOperationResponse> UpdateTagAsync(
        UpdateTriageTagRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.TriageTags.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
        {
            return new TriageTagOperationResponse(false, "Tag not found");
        }

        if (request.Name != null)
        {
            var trimmedName = request.Name.Trim();

            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                return new TriageTagOperationResponse(false, "Tag name cannot be empty");
            }

            // Check for duplicate (case-insensitive, excluding current tag)
            var exists = await _db.TriageTags
                .AnyAsync(t => t.Id != request.Id && t.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

            if (exists)
            {
                return new TriageTagOperationResponse(false, $"Tag '{trimmedName}' already exists");
            }

            entity.Name = trimmedName;
        }

        if (request.IsEnabled.HasValue)
        {
            entity.IsEnabled = request.IsEnabled.Value;
        }

        if (request.DisplayOrder.HasValue)
        {
            entity.DisplayOrder = request.DisplayOrder.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated triage tag: {TagId}", request.Id);

        return new TriageTagOperationResponse(true, "Tag updated successfully", MapToDto(entity));
    }

    /// <summary>
    /// Deletes a triage tag.
    /// This removes it from the catalog but does not delete it from any bugs in TFS.
    /// </summary>
    public async Task<TriageTagOperationResponse> DeleteTagAsync(
        int tagId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.TriageTags.FindAsync(new object[] { tagId }, cancellationToken);

        if (entity == null)
        {
            return new TriageTagOperationResponse(false, "Tag not found");
        }

        _db.TriageTags.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted triage tag: {TagId} ({TagName})", tagId, entity.Name);

        return new TriageTagOperationResponse(true, "Tag removed from catalog");
    }

    /// <summary>
    /// Reorders tags by updating display orders.
    /// </summary>
    public async Task<TriageTagOperationResponse> ReorderTagsAsync(
        List<int> tagIds,
        CancellationToken cancellationToken = default)
    {
        var entities = await _db.TriageTags
            .Where(t => tagIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        if (entities.Count != tagIds.Count)
        {
            return new TriageTagOperationResponse(false, "Some tags not found");
        }

        for (int i = 0; i < tagIds.Count; i++)
        {
            var entity = entities.First(e => e.Id == tagIds[i]);
            entity.DisplayOrder = i + 1;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Reordered {Count} triage tags", tagIds.Count);

        return new TriageTagOperationResponse(true, "Tags reordered successfully");
    }

    private static TriageTagDto MapToDto(TriageTagEntity entity)
    {
        return new TriageTagDto(
            entity.Id,
            entity.Name,
            entity.IsEnabled,
            entity.DisplayOrder,
            entity.CreatedAt
        );
    }
}
