using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Api.Services;

public sealed record PortfolioSnapshotGroupSelection(
    long SnapshotId,
    string Source,
    PortfolioSnapshot Snapshot,
    bool IncludesArchivedSnapshot);

public interface IPortfolioSnapshotSelectionService
{
    Task<PersistedPortfolioSnapshot?> GetLatestAsync(
        int productId,
        CancellationToken cancellationToken,
        bool includeArchived = false);

    Task<PersistedPortfolioSnapshot?> GetPreviousAsync(
        int productId,
        CancellationToken cancellationToken,
        bool includeArchived = false);

    Task<PersistedPortfolioSnapshot?> GetLatestBeforeAsync(
        int productId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken,
        bool includeArchived = false);

    Task<PortfolioSnapshotGroupSelection?> GetLatestPortfolioSnapshotAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken,
        bool includeArchived = false);

    Task<IReadOnlyList<PortfolioSnapshotGroupSelection>> GetPortfolioSnapshotsAsync(
        IReadOnlyCollection<int> productIds,
        int count,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        CancellationToken cancellationToken,
        bool includeArchived = false);

    Task<PortfolioSnapshotGroupSelection?> GetPreviousPortfolioSnapshotAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken,
        bool includeArchived = false);

    Task<PortfolioSnapshotGroupSelection?> GetPortfolioSnapshotByIdAsync(
        IReadOnlyCollection<int> productIds,
        long snapshotId,
        CancellationToken cancellationToken,
        bool includeArchived = false);

    Task<PortfolioSnapshotGroupSelection?> GetPortfolioSnapshotBySourceAsync(
        IReadOnlyCollection<int> productIds,
        DateTimeOffset timestamp,
        string source,
        CancellationToken cancellationToken,
        bool includeArchived = false);

    Task<bool> HasArchivedPortfolioSnapshotsAsync(
        IReadOnlyCollection<int> productIds,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        CancellationToken cancellationToken);
}

public sealed class PortfolioSnapshotSelectionService : IPortfolioSnapshotSelectionService
{
    private readonly PoToolDbContext _context;
    private readonly IPortfolioSnapshotPersistenceMapper _mapper;

    public PortfolioSnapshotSelectionService(
        PoToolDbContext context,
        IPortfolioSnapshotPersistenceMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PersistedPortfolioSnapshot?> GetLatestAsync(
        int productId,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var entity = await BuildProductQuery(productId, includeArchived)
            .OrderByDescending(snapshot => snapshot.TimestampUtc)
            .ThenByDescending(snapshot => snapshot.SnapshotId)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : _mapper.ToPersistedModel(entity);
    }

    public async Task<PersistedPortfolioSnapshot?> GetPreviousAsync(
        int productId,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var entity = await BuildProductQuery(productId, includeArchived)
            .OrderByDescending(snapshot => snapshot.TimestampUtc)
            .ThenByDescending(snapshot => snapshot.SnapshotId)
            .Skip(1)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : _mapper.ToPersistedModel(entity);
    }

    public async Task<PersistedPortfolioSnapshot?> GetLatestBeforeAsync(
        int productId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var timestampUtc = timestamp.UtcDateTime;
        var entity = await BuildProductQuery(productId, includeArchived)
            .Where(snapshot => snapshot.TimestampUtc < timestampUtc)
            .OrderByDescending(snapshot => snapshot.TimestampUtc)
            .ThenByDescending(snapshot => snapshot.SnapshotId)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : _mapper.ToPersistedModel(entity);
    }

    public async Task<PortfolioSnapshotGroupSelection?> GetLatestPortfolioSnapshotAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken,
        bool includeArchived = false)
        => (await GetPortfolioSnapshotsAsync(
                productIds,
                count: 1,
                rangeStartUtc: null,
                rangeEndUtc: null,
                cancellationToken,
                includeArchived))
            .FirstOrDefault();

    public async Task<IReadOnlyList<PortfolioSnapshotGroupSelection>> GetPortfolioSnapshotsAsync(
        IReadOnlyCollection<int> productIds,
        int count,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        if (count <= 0)
        {
            return [];
        }

        var groups = await GetOrderedPortfolioGroupsAsync(
            productIds,
            rangeStartUtc,
            rangeEndUtc,
            includeArchived,
            count,
            cancellationToken);

        var results = new List<PortfolioSnapshotGroupSelection>(groups.Count);
        foreach (var group in groups)
        {
            var selection = await GetPortfolioSnapshotBySourceAsync(
                productIds,
                new DateTimeOffset(group.TimestampUtc, TimeSpan.Zero),
                group.Source,
                cancellationToken,
                includeArchived);

            if (selection is not null)
            {
                results.Add(selection);
            }
        }

        return results;
    }

    public async Task<PortfolioSnapshotGroupSelection?> GetPreviousPortfolioSnapshotAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken,
        bool includeArchived = false)
        => (await GetPortfolioSnapshotsAsync(
                productIds,
                count: 2,
                rangeStartUtc: null,
                rangeEndUtc: null,
                cancellationToken,
                includeArchived))
            .Skip(1)
            .FirstOrDefault();

    public async Task<PortfolioSnapshotGroupSelection?> GetPortfolioSnapshotByIdAsync(
        IReadOnlyCollection<int> productIds,
        long snapshotId,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        if (productIds.Count == 0)
        {
            return null;
        }

        var header = await BuildPortfolioQuery(productIds, includeArchived)
            .Where(snapshot => snapshot.SnapshotId == snapshotId)
            .Select(snapshot => new PortfolioSnapshotGroupHeader(
                snapshot.TimestampUtc,
                snapshot.Source,
                snapshot.SnapshotId))
            .FirstOrDefaultAsync(cancellationToken);

        return header is null
            ? null
            : await GetPortfolioSnapshotBySourceAsync(
                productIds,
                new DateTimeOffset(header.TimestampUtc, TimeSpan.Zero),
                header.Source,
                cancellationToken,
                includeArchived);
    }

    public async Task<PortfolioSnapshotGroupSelection?> GetPortfolioSnapshotBySourceAsync(
        IReadOnlyCollection<int> productIds,
        DateTimeOffset timestamp,
        string source,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        if (productIds.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Portfolio snapshot source is required.", nameof(source));
        }

        var timestampUtc = timestamp.UtcDateTime;
        var entities = await BuildPortfolioSnapshotQuery(productIds, includeArchived)
            .Where(snapshot => snapshot.TimestampUtc == timestampUtc && snapshot.Source == source.Trim())
            .OrderBy(snapshot => snapshot.ProductId)
            .ThenByDescending(snapshot => snapshot.SnapshotId)
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return null;
        }

        var snapshots = entities
            .Select(_mapper.ToPersistedModel)
            .ToArray();

        var combinedSnapshot = new PortfolioSnapshot(
            snapshots[0].Snapshot.Timestamp,
            snapshots
                .SelectMany(snapshot => snapshot.Snapshot.Items)
                .OrderBy(item => item.ProductId)
                .ThenBy(item => item.ProjectNumber, StringComparer.Ordinal)
                .ThenBy(item => item.WorkPackage is null ? 0 : 1)
                .ThenBy(item => item.WorkPackage, StringComparer.Ordinal)
                .ToArray());

        return new PortfolioSnapshotGroupSelection(
            snapshots.Max(snapshot => snapshot.SnapshotId),
            snapshots[0].Source,
            combinedSnapshot,
            entities.Any(entity => entity.IsArchived));
    }

    public Task<bool> HasArchivedPortfolioSnapshotsAsync(
        IReadOnlyCollection<int> productIds,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        if (productIds.Count == 0)
        {
            return Task.FromResult(false);
        }

        return ApplyRange(BuildPortfolioQuery(productIds, includeArchived: true), rangeStartUtc, rangeEndUtc)
            .AnyAsync(snapshot => snapshot.IsArchived, cancellationToken);
    }

    private IQueryable<PortfolioSnapshotEntity> BuildProductQuery(int productId, bool includeArchived)
    {
        var query = _context.PortfolioSnapshots
            .AsNoTracking()
            .Include(snapshot => snapshot.Items)
            .Where(snapshot => snapshot.ProductId == productId);

        return includeArchived ? query : query.Where(snapshot => !snapshot.IsArchived);
    }

    private IQueryable<PortfolioSnapshotEntity> BuildPortfolioQuery(IReadOnlyCollection<int> productIds, bool includeArchived)
    {
        var query = _context.PortfolioSnapshots
            .AsNoTracking()
            .Where(snapshot => productIds.Contains(snapshot.ProductId));

        return includeArchived ? query : query.Where(snapshot => !snapshot.IsArchived);
    }

    private IQueryable<PortfolioSnapshotEntity> BuildPortfolioSnapshotQuery(IReadOnlyCollection<int> productIds, bool includeArchived)
    {
        return BuildPortfolioQuery(productIds, includeArchived)
            .Include(snapshot => snapshot.Items);
    }

    private async Task<IReadOnlyList<PortfolioSnapshotGroupHeader>> GetOrderedPortfolioGroupsAsync(
        IReadOnlyCollection<int> productIds,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchived,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        if (productIds.Count == 0 || count <= 0)
        {
            return [];
        }

        return await ApplyRange(BuildPortfolioQuery(productIds, includeArchived), rangeStartUtc, rangeEndUtc)
            .GroupBy(snapshot => new { snapshot.TimestampUtc, snapshot.Source })
            .Select(group => new
            {
                group.Key.TimestampUtc,
                group.Key.Source,
                MaxSnapshotId = group.Max(snapshot => snapshot.SnapshotId)
            })
            .OrderByDescending(group => group.TimestampUtc)
            .ThenByDescending(group => group.MaxSnapshotId)
            .Take(count)
            .Select(group => new PortfolioSnapshotGroupHeader(
                group.TimestampUtc,
                group.Source,
                group.MaxSnapshotId))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<PortfolioSnapshotEntity> ApplyRange(
        IQueryable<PortfolioSnapshotEntity> query,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc)
    {
        if (rangeStartUtc.HasValue)
        {
            var startUtc = rangeStartUtc.Value.UtcDateTime;
            query = query.Where(snapshot => snapshot.TimestampUtc >= startUtc);
        }

        if (rangeEndUtc.HasValue)
        {
            var endUtc = rangeEndUtc.Value.UtcDateTime;
            query = query.Where(snapshot => snapshot.TimestampUtc <= endUtc);
        }

        return query;
    }

    private sealed record PortfolioSnapshotGroupHeader(
        DateTime TimestampUtc,
        string Source,
        long MaxSnapshotId);
}
