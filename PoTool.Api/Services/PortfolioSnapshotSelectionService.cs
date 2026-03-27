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

    Task<PersistedPortfolioSnapshot?> GetLatestAtOrBeforeAsync(
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
    private readonly ILogger<PortfolioSnapshotSelectionService> _logger;

    public PortfolioSnapshotSelectionService(
        PoToolDbContext context,
        IPortfolioSnapshotPersistenceMapper mapper,
        ILogger<PortfolioSnapshotSelectionService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PersistedPortfolioSnapshot?> GetLatestAsync(
        int productId,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var header = (await GetCanonicalProductHeadersAsync(productId, includeArchived, cancellationToken))
            .FirstOrDefault();

        return header is null
            ? null
            : await LoadPersistedSnapshotAsync(header.SnapshotId, cancellationToken);
    }

    public async Task<PersistedPortfolioSnapshot?> GetPreviousAsync(
        int productId,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var header = (await GetCanonicalProductHeadersAsync(productId, includeArchived, cancellationToken))
            .Skip(1)
            .FirstOrDefault();

        return header is null
            ? null
            : await LoadPersistedSnapshotAsync(header.SnapshotId, cancellationToken);
    }

    public async Task<PersistedPortfolioSnapshot?> GetLatestAtOrBeforeAsync(
        int productId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var timestampUtc = timestamp.UtcDateTime;
        var header = (await GetCanonicalProductHeadersAsync(productId, includeArchived, cancellationToken))
            .FirstOrDefault(snapshot => snapshot.TimestampUtc <= timestampUtc);

        return header is null
            ? null
            : await LoadPersistedSnapshotAsync(header.SnapshotId, cancellationToken);
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

        var canonicalEntities = entities
            .GroupBy(entity => entity.ProductId)
            .Select(group =>
            {
                var orderedEntities = group
                    .OrderByDescending(entity => entity.SnapshotId)
                    .ToArray();
                if (orderedEntities.Length > 1)
                {
                    _logger.LogWarning(
                        "Duplicate logical portfolio snapshots detected for ProductId {ProductId}, TimestampUtc {TimestampUtc}, Source {Source}. Using SnapshotId {SnapshotId} as canonical.",
                        group.Key,
                        timestampUtc,
                        source.Trim(),
                        orderedEntities[0].SnapshotId);
                }

                return orderedEntities[0];
            })
            .OrderBy(entity => entity.ProductId)
            .ToArray();

        var snapshots = canonicalEntities
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
            canonicalEntities.Any(entity => entity.IsArchived));
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

    private async Task<IReadOnlyList<ProductSnapshotHeader>> GetCanonicalProductHeadersAsync(
        int productId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var headers = (await BuildProductQuery(productId, includeArchived)
            .GroupBy(snapshot => new { snapshot.ProductId, snapshot.TimestampUtc, snapshot.Source })
            .Select(group => new
            {
                group.Key.TimestampUtc,
                group.Key.Source,
                SnapshotId = group.Max(snapshot => snapshot.SnapshotId),
                DuplicateCount = group.Count()
            })
            .OrderByDescending(snapshot => snapshot.TimestampUtc)
            .ThenByDescending(snapshot => snapshot.SnapshotId)
            .ToListAsync(cancellationToken))
            .Select(snapshot => new ProductSnapshotHeader(
                snapshot.TimestampUtc,
                snapshot.Source,
                snapshot.SnapshotId,
                snapshot.DuplicateCount))
            .ToList();

        foreach (var duplicate in headers.Where(header => header.DuplicateCount > 1))
        {
            _logger.LogWarning(
                "Duplicate logical portfolio snapshots detected for ProductId {ProductId}, TimestampUtc {TimestampUtc}, Source {Source}. Using SnapshotId {SnapshotId} as canonical.",
                productId,
                duplicate.TimestampUtc,
                duplicate.Source,
                duplicate.SnapshotId);
        }

        return headers;
    }

    private async Task<PersistedPortfolioSnapshot?> LoadPersistedSnapshotAsync(
        long snapshotId,
        CancellationToken cancellationToken)
    {
        var entity = await _context.PortfolioSnapshots
            .AsNoTracking()
            .Include(snapshot => snapshot.Items)
            .SingleOrDefaultAsync(snapshot => snapshot.SnapshotId == snapshotId, cancellationToken);

        return entity is null ? null : _mapper.ToPersistedModel(entity);
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

    private sealed record ProductSnapshotHeader(
        DateTime TimestampUtc,
        string Source,
        long SnapshotId,
        int DuplicateCount);
}
