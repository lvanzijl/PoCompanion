using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Api.Services;

public sealed record PersistedPortfolioSnapshot(
    long SnapshotId,
    int ProductId,
    string Source,
    string? CreatedBy,
    bool IsArchived,
    PortfolioSnapshot Snapshot);

public interface IPortfolioSnapshotPersistenceMapper
{
    PortfolioSnapshotEntity ToEntity(
        int productId,
        string source,
        string? createdBy,
        bool isArchived,
        PortfolioSnapshot snapshot);

    PersistedPortfolioSnapshot ToPersistedModel(PortfolioSnapshotEntity entity);
}

public sealed class PortfolioSnapshotPersistenceMapper : IPortfolioSnapshotPersistenceMapper
{
    public PortfolioSnapshotEntity ToEntity(
        int productId,
        string source,
        string? createdBy,
        bool isArchived,
        PortfolioSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Portfolio snapshot source is required.", nameof(source));
        }

        if (snapshot.Items.Any(item => item.ProductId != productId))
        {
            throw new InvalidOperationException("Persisted portfolio snapshot items must all match the snapshot header ProductId.");
        }

        return new PortfolioSnapshotEntity
        {
            ProductId = productId,
            TimestampUtc = snapshot.Timestamp.UtcDateTime,
            Source = source.Trim(),
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim(),
            IsArchived = isArchived,
            Items = snapshot.Items
                .Select(item => new PortfolioSnapshotItemEntity
                {
                    ProjectNumber = item.ProjectNumber,
                    WorkPackage = item.WorkPackage,
                    Progress = item.Progress,
                    TotalWeight = item.TotalWeight,
                    LifecycleState = item.LifecycleState
                })
                .ToList()
        };
    }

    public PersistedPortfolioSnapshot ToPersistedModel(PortfolioSnapshotEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        try
        {
            if (string.IsNullOrWhiteSpace(entity.Source))
            {
                throw new InvalidOperationException($"Persisted portfolio snapshot {entity.SnapshotId} has no source.");
            }

            var timestampUtc = DateTime.SpecifyKind(entity.TimestampUtc, DateTimeKind.Utc);
            var items = entity.Items
                .OrderBy(_ => entity.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.Ordinal)
                .ThenBy(item => item.WorkPackage is null ? 0 : 1)
                .ThenBy(item => item.WorkPackage, StringComparer.Ordinal)
                .Select(item => new PortfolioSnapshotItem(
                    entity.ProductId,
                    item.ProjectNumber,
                    item.WorkPackage,
                    item.Progress,
                    item.TotalWeight,
                    item.LifecycleState))
                .ToArray();

            var snapshot = new PortfolioSnapshot(new DateTimeOffset(timestampUtc, TimeSpan.Zero), items);
            return new PersistedPortfolioSnapshot(
                entity.SnapshotId,
                entity.ProductId,
                entity.Source,
                entity.CreatedBy,
                entity.IsArchived,
                snapshot);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Persisted portfolio snapshot {entity.SnapshotId} failed integrity validation and cannot be used.",
                exception);
        }
    }
}

public interface IPortfolioSnapshotPersistenceService
{
    Task<PersistedPortfolioSnapshot?> GetBySourceAsync(
        int productId,
        string source,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken,
        bool includeArchived = true);

    Task<PersistedPortfolioSnapshot> PersistAsync(
        int productId,
        string source,
        string? createdBy,
        PortfolioSnapshot snapshot,
        CancellationToken cancellationToken);
}

public sealed class PortfolioSnapshotPersistenceService : IPortfolioSnapshotPersistenceService
{
    private readonly PoToolDbContext _context;
    private readonly IPortfolioSnapshotPersistenceMapper _mapper;

    public PortfolioSnapshotPersistenceService(
        PoToolDbContext context,
        IPortfolioSnapshotPersistenceMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PersistedPortfolioSnapshot?> GetBySourceAsync(
        int productId,
        string source,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken,
        bool includeArchived = true)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Portfolio snapshot source is required.", nameof(source));
        }

        var timestampUtc = timestamp.UtcDateTime;
        var query = _context.PortfolioSnapshots
            .AsNoTracking()
            .Include(entity => entity.Items)
            .Where(entity =>
                entity.ProductId == productId
                && entity.TimestampUtc == timestampUtc
                && entity.Source == source.Trim());

        if (!includeArchived)
        {
            query = query.Where(entity => !entity.IsArchived);
        }

        var entity = await query
            .OrderByDescending(snapshot => snapshot.SnapshotId)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : _mapper.ToPersistedModel(entity);
    }

    public async Task<PersistedPortfolioSnapshot> PersistAsync(
        int productId,
        string source,
        string? createdBy,
        PortfolioSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var entity = _mapper.ToEntity(productId, source, createdBy, isArchived: false, snapshot);
        entity.SnapshotId = (await _context.PortfolioSnapshots
            .AsNoTracking()
            .MaxAsync(existing => (long?)existing.SnapshotId, cancellationToken) ?? 0L) + 1L;

        _context.PortfolioSnapshots.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        var persisted = await GetBySourceAsync(
            productId,
            source,
            snapshot.Timestamp,
            cancellationToken,
            includeArchived: true);

        return persisted ?? throw new InvalidOperationException("Persisted portfolio snapshot could not be reloaded after save.");
    }
}
