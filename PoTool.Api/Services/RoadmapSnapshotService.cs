using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.UiRoadmap;
using PoTool.Shared.Planning;

namespace PoTool.Api.Services;

/// <summary>
/// Service for managing roadmap snapshots in persistent storage.
/// Read-only with respect to TFS — snapshots are stored in the application database.
/// </summary>
public class RoadmapSnapshotService
{
    private readonly PoToolDbContext _db;
    private readonly ILogger<RoadmapSnapshotService> _logger;

    public RoadmapSnapshotService(
        PoToolDbContext db,
        ILogger<RoadmapSnapshotService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new roadmap snapshot from the provided product/epic data.
    /// </summary>
    public async Task<RoadmapSnapshotDto> CreateSnapshotAsync(
        CreateRoadmapSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = new RoadmapSnapshotEntity
        {
            CreatedAtUtc = DateTime.UtcNow,
            Description = request.Description?.Trim(),
            Items = request.Products.SelectMany(p => p.Epics.Select(e => new RoadmapSnapshotItemEntity
            {
                ProductName = p.ProductName,
                EpicTfsId = e.EpicTfsId,
                EpicTitle = e.EpicTitle,
                EpicOrder = e.EpicOrder
            })).ToList()
        };

        _db.RoadmapSnapshots.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created roadmap snapshot {SnapshotId} with {ItemCount} items",
            entity.Id, entity.Items.Count);

        return MapToSummaryDto(entity);
    }

    /// <summary>
    /// Lists all snapshots, ordered newest first.
    /// </summary>
    public async Task<List<RoadmapSnapshotDto>> ListSnapshotsAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _db.RoadmapSnapshots
            .Include(s => s.Items)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return snapshots.Select(MapToSummaryDto).ToList();
    }

    /// <summary>
    /// Gets a snapshot detail including all items, grouped by product.
    /// </summary>
    public async Task<RoadmapSnapshotDetailDto?> GetSnapshotDetailAsync(
        int snapshotId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.RoadmapSnapshots
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken);

        if (entity == null) return null;

        return MapToDetailDto(entity);
    }

    /// <summary>
    /// Deletes a snapshot and all its items.
    /// </summary>
    public async Task<bool> DeleteSnapshotAsync(
        int snapshotId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.RoadmapSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken);

        if (entity == null) return false;

        _db.RoadmapSnapshots.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted roadmap snapshot {SnapshotId}", snapshotId);
        return true;
    }

    private static RoadmapSnapshotDto MapToSummaryDto(RoadmapSnapshotEntity entity)
    {
        return new RoadmapSnapshotDto(
            entity.Id,
            new DateTimeOffset(entity.CreatedAtUtc, TimeSpan.Zero),
            entity.Description,
            entity.Items.Select(i => i.ProductName).Distinct().Count(),
            entity.Items.Count
        );
    }

    private static RoadmapSnapshotDetailDto MapToDetailDto(RoadmapSnapshotEntity entity)
    {
        var products = entity.Items
            .GroupBy(i => i.ProductName)
            .Select(g => new RoadmapSnapshotProductDto(
                g.Key,
                g.OrderBy(i => i.EpicOrder)
                 .Select(i => new RoadmapSnapshotEpicDto(i.EpicTfsId, i.EpicTitle, i.EpicOrder))
                 .ToList()
            ))
            .ToList();

        return new RoadmapSnapshotDetailDto(
            entity.Id,
            new DateTimeOffset(entity.CreatedAtUtc, TimeSpan.Zero),
            entity.Description,
            products
        );
    }
}
