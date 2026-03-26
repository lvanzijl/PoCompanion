using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioSnapshotPersistenceServiceTests
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<PoToolDbContext> _options = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var context = new PoToolDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        await SeedProductAsync(context, 1);
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _connection.DisposeAsync();
    }

    [TestMethod]
    public async Task PersistAsync_PersistsSnapshotHeaderAndAllItems()
    {
        await using var context = new PoToolDbContext(_options);
        var mapper = new PortfolioSnapshotPersistenceMapper();
        var service = new PortfolioSnapshotPersistenceService(context, mapper);
        var timestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var persisted = await service.PersistAsync(
            1,
            "Sprint 1",
            createdBy: "tester",
            new PortfolioSnapshot(
                timestamp,
                [
                    new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10d, WorkPackageLifecycleState.Active),
                    new PortfolioSnapshotItem(1, "PRJ-100", "WP-2", 0.6d, 15d, WorkPackageLifecycleState.Retired)
                ]),
            CancellationToken.None);

        Assert.IsGreaterThan(0L, persisted.SnapshotId);
        Assert.AreEqual("Sprint 1", persisted.Source);
        Assert.AreEqual("tester", persisted.CreatedBy);

        var header = await context.PortfolioSnapshots.Include(snapshot => snapshot.Items).SingleAsync();
        Assert.AreEqual(1, header.ProductId);
        Assert.AreEqual(timestamp.UtcDateTime, header.TimestampUtc);
        Assert.AreEqual("Sprint 1", header.Source);
        Assert.AreEqual("tester", header.CreatedBy);
        Assert.IsFalse(header.IsArchived);
        Assert.HasCount(2, header.Items);
        CollectionAssert.AreEqual(
            new[] { "PRJ-100|WP-1|Active", "PRJ-100|WP-2|Retired" },
            header.Items
                .OrderBy(item => item.ProjectNumber, StringComparer.Ordinal)
                .ThenBy(item => item.WorkPackage, StringComparer.Ordinal)
                .Select(item => $"{item.ProjectNumber}|{item.WorkPackage}|{item.LifecycleState}")
                .ToArray());
    }

    [TestMethod]
    public async Task GetLatestAsync_ExcludesArchivedSnapshotsByDefault()
    {
        await using var context = new PoToolDbContext(_options);
        var mapper = new PortfolioSnapshotPersistenceMapper();
        var selectionService = new PortfolioSnapshotSelectionService(context, mapper);

        context.PortfolioSnapshots.Add(mapper.ToEntity(
            1,
            "Sprint 1",
            createdBy: null,
            isArchived: false,
            CreateSnapshot(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), 1, 0.3d)));
        context.PortfolioSnapshots.Add(mapper.ToEntity(
            1,
            "Sprint 2",
            createdBy: null,
            isArchived: true,
            CreateSnapshot(new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero), 1, 0.6d)));
        await context.SaveChangesAsync();

        var latestDefault = await selectionService.GetLatestAsync(1, CancellationToken.None);
        var latestIncludingArchived = await selectionService.GetLatestAsync(1, CancellationToken.None, includeArchived: true);

        Assert.IsNotNull(latestDefault);
        Assert.AreEqual("Sprint 1", latestDefault.Source);
        Assert.IsNotNull(latestIncludingArchived);
        Assert.AreEqual("Sprint 2", latestIncludingArchived.Source);
    }

    [TestMethod]
    public async Task GetLatestAsync_UsesTimestampThenSnapshotIdDescendingTieBreak()
    {
        await using var context = new PoToolDbContext(_options);
        var mapper = new PortfolioSnapshotPersistenceMapper();
        var persistenceService = new PortfolioSnapshotPersistenceService(context, mapper);
        var selectionService = new PortfolioSnapshotSelectionService(context, mapper);
        var timestamp = new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero);

        await persistenceService.PersistAsync(1, "Sprint 2A", null, CreateSnapshot(timestamp, 1, 0.4d), CancellationToken.None);
        var second = await persistenceService.PersistAsync(1, "Sprint 2B", null, CreateSnapshot(timestamp, 1, 0.7d), CancellationToken.None);

        var latest = await selectionService.GetLatestAsync(1, CancellationToken.None);

        Assert.IsNotNull(latest);
        Assert.AreEqual(second.SnapshotId, latest.SnapshotId);
        Assert.AreEqual("Sprint 2B", latest.Source);
    }

    [TestMethod]
    public async Task GetPreviousAsync_ReturnsSecondSnapshotInDeterministicOrder()
    {
        await using var context = new PoToolDbContext(_options);
        var mapper = new PortfolioSnapshotPersistenceMapper();
        var persistenceService = new PortfolioSnapshotPersistenceService(context, mapper);
        var selectionService = new PortfolioSnapshotSelectionService(context, mapper);
        var timestamp = new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero);

        await persistenceService.PersistAsync(1, "Sprint 1", null, CreateSnapshot(timestamp.AddDays(-7), 1, 0.2d), CancellationToken.None);
        var expectedPrevious = await persistenceService.PersistAsync(1, "Sprint 2A", null, CreateSnapshot(timestamp, 1, 0.4d), CancellationToken.None);
        await persistenceService.PersistAsync(1, "Sprint 2B", null, CreateSnapshot(timestamp, 1, 0.6d), CancellationToken.None);

        var previous = await selectionService.GetPreviousAsync(1, CancellationToken.None);

        Assert.IsNotNull(previous);
        Assert.AreEqual(expectedPrevious.Source, previous.Source);
        Assert.AreEqual(expectedPrevious.SnapshotId, previous.SnapshotId);
    }

    [TestMethod]
    public async Task GetLatestBeforeAsync_ReturnsLatestStrictlyBeforeTimestamp()
    {
        await using var context = new PoToolDbContext(_options);
        var mapper = new PortfolioSnapshotPersistenceMapper();
        var persistenceService = new PortfolioSnapshotPersistenceService(context, mapper);
        var selectionService = new PortfolioSnapshotSelectionService(context, mapper);

        await persistenceService.PersistAsync(1, "Sprint 1", null, CreateSnapshot(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), 1, 0.2d), CancellationToken.None);
        var expected = await persistenceService.PersistAsync(1, "Sprint 2", null, CreateSnapshot(new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero), 1, 0.4d), CancellationToken.None);
        await persistenceService.PersistAsync(1, "Sprint 3", null, CreateSnapshot(new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero), 1, 0.8d), CancellationToken.None);

        var latestBefore = await selectionService.GetLatestBeforeAsync(
            1,
            new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.IsNotNull(latestBefore);
        Assert.AreEqual(expected.Source, latestBefore.Source);
        Assert.AreEqual(expected.SnapshotId, latestBefore.SnapshotId);
    }

    [TestMethod]
    public async Task GetLatestAsync_SurfacesCorruptedPersistedRowsInsteadOfSkippingThem()
    {
        await using var context = new PoToolDbContext(_options);
        var selectionService = new PortfolioSnapshotSelectionService(context, new PortfolioSnapshotPersistenceMapper());

        context.PortfolioSnapshots.Add(new PortfolioSnapshotEntity
        {
            ProductId = 1,
            TimestampUtc = new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc),
            Source = "Corrupt",
            Items =
            [
                new PortfolioSnapshotItemEntity
                {
                    ProjectNumber = string.Empty,
                    WorkPackage = "WP-1",
                    Progress = 0.4d,
                    TotalWeight = 10d,
                    LifecycleState = WorkPackageLifecycleState.Active
                }
            ]
        });
        await context.SaveChangesAsync();

        try
        {
            await selectionService.GetLatestAsync(1, CancellationToken.None);
            Assert.Fail("Corrupted persisted rows should surface an integrity exception.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [TestMethod]
    public async Task GetPortfolioSnapshotsAsync_ReturnsLatestNInTimestampThenSnapshotIdOrder()
    {
        await using var context = new PoToolDbContext(_options);
        var mapper = new PortfolioSnapshotPersistenceMapper();
        var persistenceService = new PortfolioSnapshotPersistenceService(context, mapper);
        var selectionService = new PortfolioSnapshotSelectionService(context, mapper);
        var timestamp = new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero);

        await persistenceService.PersistAsync(1, "Sprint 1", null, CreateSnapshot(timestamp.AddDays(-7), 1, 0.2d), CancellationToken.None);
        await persistenceService.PersistAsync(1, "Sprint 2A", null, CreateSnapshot(timestamp, 1, 0.4d), CancellationToken.None);
        await persistenceService.PersistAsync(1, "Sprint 2B", null, CreateSnapshot(timestamp, 1, 0.6d), CancellationToken.None);
        await persistenceService.PersistAsync(1, "Sprint 3", null, CreateSnapshot(timestamp.AddDays(7), 1, 0.8d), CancellationToken.None);

        var snapshots = await selectionService.GetPortfolioSnapshotsAsync(
            [1],
            count: 3,
            rangeStartUtc: null,
            rangeEndUtc: null,
            CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "Sprint 3", "Sprint 2B", "Sprint 2A" },
            snapshots.Select(snapshot => snapshot.Source).ToArray());
    }

    [TestMethod]
    public async Task GetPortfolioSnapshotsAsync_ExcludesArchivedSnapshotsByDefault()
    {
        await using var context = new PoToolDbContext(_options);
        var mapper = new PortfolioSnapshotPersistenceMapper();
        var selectionService = new PortfolioSnapshotSelectionService(context, mapper);

        context.PortfolioSnapshots.Add(mapper.ToEntity(
            1,
            "Sprint 1",
            createdBy: null,
            isArchived: false,
            CreateSnapshot(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), 1, 0.3d)));
        context.PortfolioSnapshots.Add(mapper.ToEntity(
            1,
            "Sprint 2",
            createdBy: null,
            isArchived: true,
            CreateSnapshot(new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero), 1, 0.6d)));
        await context.SaveChangesAsync();

        var snapshots = await selectionService.GetPortfolioSnapshotsAsync(
            [1],
            count: 5,
            rangeStartUtc: null,
            rangeEndUtc: null,
            CancellationToken.None);

        Assert.HasCount(1, snapshots);
        Assert.AreEqual("Sprint 1", snapshots[0].Source);
        Assert.IsTrue(await selectionService.HasArchivedPortfolioSnapshotsAsync([1], null, null, CancellationToken.None));
    }

    private static PortfolioSnapshot CreateSnapshot(DateTimeOffset timestamp, int productId, double progress)
        => new(
            timestamp,
            [
                new PortfolioSnapshotItem(productId, "PRJ-100", "WP-1", progress, 10d, WorkPackageLifecycleState.Active)
            ]);

    private static async Task SeedProductAsync(PoToolDbContext context, int productId)
    {
        var profile = new ProfileEntity
        {
            Name = "PO 1"
        };
        context.Profiles.Add(profile);
        await context.SaveChangesAsync();

        context.Products.Add(new ProductEntity
        {
            Id = productId,
            ProductOwnerId = profile.Id,
            Name = "Product 1"
        });
        await context.SaveChangesAsync();
    }
}
