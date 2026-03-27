using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Tests.Unit.Services;

public sealed class CdcTestDataSeeder
{
    public const string ProductAKey = "ProductA";
    public const string ProductBKey = "ProductB";
    public const string ProductCKey = "ProductC";

    public const string ProductAName = "Product A - Active evolving product";
    public const string ProductBName = "Product B - Empty to active";
    public const string ProductCName = "Product C - Completed product";

    public const string EmptyPortfolioSource = "Empty portfolio";
    public const string Sprint1Source = "Sprint 1";
    public const string Sprint2Source = "Sprint 2";
    public const string Sprint3Source = "Sprint 3";
    public const string Sprint4ASource = "Sprint 4A";
    public const string Sprint4BSource = "Sprint 4B";
    public const string Sprint5Source = "Sprint 5";

    private static readonly DateTimeOffset Sprint1Timestamp = new(2026, 3, 7, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sprint2Timestamp = new(2026, 3, 14, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sprint3Timestamp = new(2026, 3, 21, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sprint4Timestamp = new(2026, 3, 28, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sprint5Timestamp = new(2026, 4, 4, 0, 0, 0, TimeSpan.Zero);

    private readonly PoToolDbContext _context;
    private readonly PortfolioSnapshotPersistenceService _persistenceService;

    public CdcTestDataSeeder(PoToolDbContext context)
    {
        _context = context;
        _persistenceService = new PortfolioSnapshotPersistenceService(
            context,
            new PortfolioSnapshotPersistenceMapper(),
            NullLogger<PortfolioSnapshotPersistenceService>.Instance);
    }

    public async Task<CdcTestDataset> SeedAsync(CancellationToken cancellationToken = default)
    {
        var owner = await GetOrCreateProfileAsync(cancellationToken);
        var team = await GetOrCreateTeamAsync(cancellationToken);
        await EnsureSprintAsync(team.Id, Sprint1Source, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), Sprint1Timestamp.UtcDateTime, cancellationToken);
        await EnsureSprintAsync(team.Id, Sprint2Source, new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc), Sprint2Timestamp.UtcDateTime, cancellationToken);
        await EnsureSprintAsync(team.Id, Sprint3Source, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), Sprint3Timestamp.UtcDateTime, cancellationToken);
        await EnsureSprintAsync(team.Id, Sprint4ASource, new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc), Sprint4Timestamp.UtcDateTime, cancellationToken);
        await EnsureSprintAsync(team.Id, Sprint5Source, new DateTime(2026, 3, 29, 0, 0, 0, DateTimeKind.Utc), Sprint5Timestamp.UtcDateTime, cancellationToken);

        var productA = await GetOrCreateProductAsync(owner.Id, ProductAName, order: 1, cancellationToken);
        var productB = await GetOrCreateProductAsync(owner.Id, ProductBName, order: 2, cancellationToken);
        var productC = await GetOrCreateProductAsync(owner.Id, ProductCName, order: 3, cancellationToken);

        await SeedProductAAsync(productA.Id, cancellationToken);
        await SeedProductBAsync(productB.Id, cancellationToken);
        await SeedProductCAsync(productC.Id, cancellationToken);

        return await BuildDatasetAsync(
            owner.Id,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ProductAKey] = productA.Id,
                [ProductBKey] = productB.Id,
                [ProductCKey] = productC.Id
            },
            cancellationToken);
    }

    private async Task SeedProductAAsync(int productId, CancellationToken cancellationToken)
    {
        await PersistSnapshotAsync(
            productId,
            Sprint1Source,
            Sprint1Timestamp,
            cancellationToken,
            Item(productId, "A-EP-100", "A-FEAT-1", 0.10d, 8d),
            Item(productId, "A-EP-200", "A-FEAT-3", 0.00d, 3d),
            Item(productId, "A-EP-300", "A-FEAT-0", 0.00d, 0d));
        await PersistSnapshotAsync(
            productId,
            Sprint2Source,
            Sprint2Timestamp,
            cancellationToken,
            Item(productId, "A-EP-100", "A-FEAT-1", 0.35d, 8d),
            Item(productId, "A-EP-100", "A-FEAT-2", 0.00d, 5d),
            Item(productId, "A-EP-200", "A-FEAT-3", 0.20d, 3d),
            Item(productId, "A-EP-300", "A-FEAT-0", 0.00d, 0d));
        await PersistSnapshotAsync(
            productId,
            Sprint3Source,
            Sprint3Timestamp,
            cancellationToken,
            Item(productId, "A-EP-100", "A-FEAT-1", 0.65d, 8d),
            Item(productId, "A-EP-100", "A-FEAT-2", 0.30d, 5d),
            Item(productId, "A-EP-200", "A-FEAT-3", 0.25d, 3d),
            Item(productId, "A-EP-300", "A-FEAT-0", 0.00d, 0d));
        await PersistSnapshotAsync(
            productId,
            Sprint4ASource,
            Sprint4Timestamp,
            cancellationToken,
            Item(productId, "A-EP-100", "A-FEAT-1", 0.80d, 8d),
            Item(productId, "A-EP-100", "A-FEAT-2", 0.50d, 5d),
            Item(productId, "A-EP-200", "A-FEAT-3", 0.25d, 3d, WorkPackageLifecycleState.Retired),
            Item(productId, "A-EP-300", "A-FEAT-0", 0.00d, 0d));
        await PersistSnapshotAsync(
            productId,
            Sprint4BSource,
            Sprint4Timestamp,
            cancellationToken,
            Item(productId, "A-EP-100", "A-FEAT-1", 0.82d, 8d),
            Item(productId, "A-EP-100", "A-FEAT-2", 0.55d, 5d),
            Item(productId, "A-EP-200", "A-FEAT-3", 0.40d, 4d),
            Item(productId, "A-EP-300", "A-FEAT-0", 0.00d, 0d));
        await PersistSnapshotAsync(
            productId,
            Sprint5Source,
            Sprint5Timestamp,
            cancellationToken,
            Item(productId, "A-EP-100", "A-FEAT-1", 1.00d, 8d),
            Item(productId, "A-EP-100", "A-FEAT-2", 0.70d, 5d),
            Item(productId, "A-EP-200", "A-FEAT-3", 0.40d, 4d, WorkPackageLifecycleState.Retired),
            Item(productId, "A-EP-300", "A-FEAT-0", 0.00d, 0d));
    }

    private async Task SeedProductBAsync(int productId, CancellationToken cancellationToken)
    {
        await PersistSnapshotAsync(productId, EmptyPortfolioSource, DateTimeOffset.UnixEpoch, cancellationToken);
        await PersistSnapshotAsync(productId, Sprint2Source, Sprint2Timestamp, cancellationToken);
        await PersistSnapshotAsync(
            productId,
            Sprint3Source,
            Sprint3Timestamp,
            cancellationToken,
            Item(productId, "B-EP-100", "B-FEAT-1", 0.00d, 5d),
            Item(productId, "B-EP-200", null, 0.00d, 4d));
        await PersistSnapshotAsync(
            productId,
            Sprint4ASource,
            Sprint4Timestamp,
            cancellationToken,
            Item(productId, "B-EP-100", "B-FEAT-1", 0.25d, 5d),
            Item(productId, "B-EP-100", "B-FEAT-2", 0.00d, 2d),
            Item(productId, "B-EP-200", null, 0.40d, 4d));
        await PersistSnapshotAsync(
            productId,
            Sprint4BSource,
            Sprint4Timestamp,
            cancellationToken,
            Item(productId, "B-EP-100", "B-FEAT-1", 0.50d, 5d),
            Item(productId, "B-EP-100", "B-FEAT-2", 0.20d, 2d),
            Item(productId, "B-EP-200", null, 0.70d, 4d));
        await PersistSnapshotAsync(
            productId,
            Sprint5Source,
            Sprint5Timestamp,
            cancellationToken,
            Item(productId, "B-EP-100", "B-FEAT-1", 0.90d, 5d),
            Item(productId, "B-EP-100", "B-FEAT-2", 0.60d, 2d),
            Item(productId, "B-EP-200", null, 1.00d, 4d),
            Item(productId, "B-EP-300", "B-FEAT-3", 0.00d, 3d));
    }

    private async Task SeedProductCAsync(int productId, CancellationToken cancellationToken)
    {
        await PersistSnapshotAsync(
            productId,
            Sprint1Source,
            Sprint1Timestamp,
            cancellationToken,
            Item(productId, "C-EP-100", "C-FEAT-1", 0.40d, 6d),
            Item(productId, "C-EP-100", "C-FEAT-2", 0.00d, 4d),
            Item(productId, "C-EP-200", "C-FEAT-3", 0.30d, 5d));
        await PersistSnapshotAsync(
            productId,
            Sprint2Source,
            Sprint2Timestamp,
            cancellationToken,
            Item(productId, "C-EP-100", "C-FEAT-1", 0.60d, 6d),
            Item(productId, "C-EP-100", "C-FEAT-2", 0.20d, 4d),
            Item(productId, "C-EP-200", "C-FEAT-3", 0.45d, 5d));
        await PersistSnapshotAsync(
            productId,
            Sprint3Source,
            Sprint3Timestamp,
            cancellationToken,
            Item(productId, "C-EP-100", "C-FEAT-1", 0.80d, 6d),
            Item(productId, "C-EP-100", "C-FEAT-2", 0.50d, 4d),
            Item(productId, "C-EP-200", "C-FEAT-3", 0.70d, 5d));
        await PersistSnapshotAsync(
            productId,
            Sprint4ASource,
            Sprint4Timestamp,
            cancellationToken,
            Item(productId, "C-EP-100", "C-FEAT-1", 1.00d, 6d),
            Item(productId, "C-EP-100", "C-FEAT-2", 0.80d, 4d),
            Item(productId, "C-EP-200", "C-FEAT-3", 0.90d, 5d));
        await PersistSnapshotAsync(
            productId,
            Sprint4BSource,
            Sprint4Timestamp,
            cancellationToken,
            Item(productId, "C-EP-100", "C-FEAT-1", 1.00d, 6d),
            Item(productId, "C-EP-100", "C-FEAT-2", 1.00d, 4d),
            Item(productId, "C-EP-200", "C-FEAT-3", 0.95d, 5d));
        await PersistSnapshotAsync(
            productId,
            Sprint5Source,
            Sprint5Timestamp,
            cancellationToken,
            Item(productId, "C-EP-100", "C-FEAT-1", 1.00d, 6d),
            Item(productId, "C-EP-100", "C-FEAT-2", 1.00d, 4d),
            Item(productId, "C-EP-200", "C-FEAT-3", 1.00d, 5d));
    }

    private async Task PersistSnapshotAsync(
        int productId,
        string source,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken,
        params PortfolioSnapshotItem[] items)
    {
        await _persistenceService.PersistAsync(
            productId,
            source,
            createdBy: "cdc-test-seeder",
            new PortfolioSnapshot(timestamp, items),
            cancellationToken);
    }

    private async Task<ProfileEntity> GetOrCreateProfileAsync(CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles
            .SingleOrDefaultAsync(entity => entity.Name == "CDC Test Owner", cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new ProfileEntity
        {
            Name = "CDC Test Owner"
        };

        _context.Profiles.Add(profile);
        await _context.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private async Task<ProductEntity> GetOrCreateProductAsync(
        int ownerId,
        string name,
        int order,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .SingleOrDefaultAsync(entity => entity.ProductOwnerId == ownerId && entity.Name == name, cancellationToken);
        if (product is not null)
        {
            if (product.Order != order)
            {
                product.Order = order;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return product;
        }

        product = new ProductEntity
        {
            ProductOwnerId = ownerId,
            Name = name,
            Order = order
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);
        return product;
    }

    private async Task<TeamEntity> GetOrCreateTeamAsync(CancellationToken cancellationToken)
    {
        var team = await _context.Teams
            .SingleOrDefaultAsync(entity => entity.TeamAreaPath == "Area/CDC", cancellationToken);
        if (team is not null)
        {
            return team;
        }

        team = new TeamEntity
        {
            Name = "CDC Team",
            TeamAreaPath = "Area/CDC",
            ProjectName = "CDC Project"
        };

        _context.Teams.Add(team);
        await _context.SaveChangesAsync(cancellationToken);
        return team;
    }

    private async Task EnsureSprintAsync(
        int teamId,
        string name,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var path = $"\\CDC\\{name}";
        var existing = await _context.Sprints
            .SingleOrDefaultAsync(entity => entity.TeamId == teamId && entity.Path == path, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        _context.Sprints.Add(new SprintEntity
        {
            TeamId = teamId,
            Path = path,
            Name = name,
            StartUtc = new DateTimeOffset(startUtc, TimeSpan.Zero),
            StartDateUtc = startUtc,
            EndUtc = new DateTimeOffset(endUtc, TimeSpan.Zero),
            EndDateUtc = endUtc,
            LastSyncedUtc = new DateTimeOffset(endUtc, TimeSpan.Zero),
            LastSyncedDateUtc = endUtc
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<CdcTestDataset> BuildDatasetAsync(
        int ownerId,
        IReadOnlyDictionary<string, int> productIdsByKey,
        CancellationToken cancellationToken)
    {
        var productKeysById = productIdsByKey.ToDictionary(pair => pair.Value, pair => pair.Key);
        var snapshots = await _context.PortfolioSnapshots
            .AsNoTracking()
            .Where(snapshot => productKeysById.Keys.Contains(snapshot.ProductId))
            .OrderBy(snapshot => snapshot.ProductId)
            .ThenBy(snapshot => snapshot.TimestampUtc)
            .ThenBy(snapshot => snapshot.SnapshotId)
            .Select(snapshot => new
            {
                snapshot.SnapshotId,
                snapshot.ProductId,
                snapshot.Source,
                snapshot.TimestampUtc,
                ItemCount = snapshot.Items.Count
            })
            .ToListAsync(cancellationToken);

        var snapshotAliases = snapshots.ToDictionary(
            snapshot => $"{productKeysById[snapshot.ProductId]}:{snapshot.Source}",
            snapshot => new CdcSeededSnapshot(
                snapshot.SnapshotId,
                snapshot.ProductId,
                snapshot.Source,
                new DateTimeOffset(DateTime.SpecifyKind(snapshot.TimestampUtc, DateTimeKind.Utc)),
                snapshot.ItemCount),
            StringComparer.Ordinal);

        return new CdcTestDataset(
            ownerId,
            productIdsByKey[ProductAKey],
            productIdsByKey[ProductBKey],
            productIdsByKey[ProductCKey],
            snapshotAliases);
    }

    private static PortfolioSnapshotItem Item(
        int productId,
        string projectNumber,
        string? workPackage,
        double progress,
        double totalWeight,
        WorkPackageLifecycleState lifecycleState = WorkPackageLifecycleState.Active)
        => new(productId, projectNumber, workPackage, progress, totalWeight, lifecycleState);
}

public sealed record CdcTestDataset(
    int ProductOwnerId,
    int ProductAId,
    int ProductBId,
    int ProductCId,
    IReadOnlyDictionary<string, CdcSeededSnapshot> SnapshotsByAlias)
{
    public CdcSeededSnapshot GetSnapshot(string alias) => SnapshotsByAlias[alias];
}

public sealed record CdcSeededSnapshot(
    long SnapshotId,
    int ProductId,
    string Source,
    DateTimeOffset Timestamp,
    int ItemCount);
