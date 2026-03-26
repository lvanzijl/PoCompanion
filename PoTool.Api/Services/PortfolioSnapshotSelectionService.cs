using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Api.Services;

public sealed record PortfolioSnapshotGroupSelection(
    long SnapshotId,
    string Source,
    PortfolioSnapshot Snapshot);

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

    Task<PortfolioSnapshotGroupSelection?> GetPreviousPortfolioSnapshotAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken,
        bool includeArchived = false);

    Task<PortfolioSnapshotGroupSelection?> GetPortfolioSnapshotBySourceAsync(
        IReadOnlyCollection<int> productIds,
        DateTimeOffset timestamp,
        string source,
        CancellationToken cancellationToken,
        bool includeArchived = false);
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
    {
        var group = (await GetOrderedPortfolioGroupsAsync(productIds, includeArchived, cancellationToken))
            .FirstOrDefault();

        return group is null
            ? null
            : await GetPortfolioSnapshotBySourceAsync(productIds, new DateTimeOffset(group.TimestampUtc, TimeSpan.Zero), group.Source, cancellationToken, includeArchived);
    }

    public async Task<PortfolioSnapshotGroupSelection?> GetPreviousPortfolioSnapshotAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var group = (await GetOrderedPortfolioGroupsAsync(productIds, includeArchived, cancellationToken))
            .Skip(1)
            .FirstOrDefault();

        return group is null
            ? null
            : await GetPortfolioSnapshotBySourceAsync(productIds, new DateTimeOffset(group.TimestampUtc, TimeSpan.Zero), group.Source, cancellationToken, includeArchived);
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
            combinedSnapshot);
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
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        if (productIds.Count == 0)
        {
            return [];
        }

        var headers = await BuildPortfolioQuery(productIds, includeArchived)
            .Select(snapshot => new PortfolioSnapshotGroupHeader(
                snapshot.TimestampUtc,
                snapshot.Source,
                snapshot.SnapshotId))
            .ToListAsync(cancellationToken);

        return headers
            .GroupBy(header => new { header.TimestampUtc, header.Source })
            .Select(group => new PortfolioSnapshotGroupHeader(
                group.Key.TimestampUtc,
                group.Key.Source,
                group.Max(header => header.MaxSnapshotId)))
            .OrderByDescending(group => group.TimestampUtc)
            .ThenByDescending(group => group.MaxSnapshotId)
            .ToList();
    }

    private sealed record PortfolioSnapshotGroupHeader(
        DateTime TimestampUtc,
        string Source,
        long MaxSnapshotId);
}
