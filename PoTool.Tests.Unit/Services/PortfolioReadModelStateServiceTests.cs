using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioReadModelStateServiceTests
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
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _connection.DisposeAsync();
    }

    [TestMethod]
    public async Task GetLatestStateAsync_PersistsCurrentAndPreviousSnapshotsThenSelectsThem()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, productId) = await SeedOwnerAndProductAsync(context);
        var captureService = new FakePortfolioSnapshotCaptureDataService();
        captureService.Sources.AddRange(
        [
            new PortfolioSnapshotCaptureSource(1, "Sprint 1", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc)),
            new PortfolioSnapshotCaptureSource(2, "Sprint 2", new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc))
        ]);
        captureService.InputsBySource["Sprint 1"] = new Dictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>
        {
            [productId] =
            [
                new PortfolioSnapshotFactoryEpicInput(productId, "PRJ-100", "WP-1", 0.4d, 10d)
            ]
        };
        captureService.InputsBySource["Sprint 2"] = new Dictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>
        {
            [productId] =
            [
                new PortfolioSnapshotFactoryEpicInput(productId, "PRJ-100", "WP-1", 0.7d, 10d)
            ]
        };

        var service = CreateStateService(context, captureService);

        var state = await service.GetLatestStateAsync(profileId, CancellationToken.None);

        Assert.IsNotNull(state);
        Assert.AreEqual("Sprint 2", state.CurrentSnapshotLabel);
        Assert.AreEqual("Sprint 1", state.PreviousSnapshotLabel);
        Assert.AreEqual(new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero), state.CurrentSnapshot.Timestamp);
        Assert.IsNotNull(state.PreviousSnapshot);
        Assert.AreEqual(new DateTimeOffset(2026, 3, 7, 0, 0, 0, TimeSpan.Zero), state.PreviousSnapshot.Timestamp);
        Assert.AreEqual(2, await context.PortfolioSnapshots.CountAsync());
    }

    [TestMethod]
    public async Task GetLatestStateAsync_UsesPersistedSelectionWhenTransientCaptureSourcesAreUnavailable()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, productId) = await SeedOwnerAndProductAsync(context);
        var captureService = new FakePortfolioSnapshotCaptureDataService();
        captureService.Sources.Add(new PortfolioSnapshotCaptureSource(1, "Sprint 1", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc)));
        captureService.InputsBySource["Sprint 1"] = new Dictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>
        {
            [productId] =
            [
                new PortfolioSnapshotFactoryEpicInput(productId, "PRJ-100", "WP-1", 0.4d, 10d)
            ]
        };

        var service = CreateStateService(context, captureService);
        var first = await service.GetLatestStateAsync(profileId, CancellationToken.None);
        Assert.IsNotNull(first);

        captureService.Sources.Clear();
        captureService.InputsBySource.Clear();

        var second = await service.GetLatestStateAsync(profileId, CancellationToken.None);

        Assert.IsNotNull(second);
        Assert.AreEqual(first.CurrentSnapshotLabel, second.CurrentSnapshotLabel);
        Assert.AreEqual(first.CurrentSnapshot.Timestamp, second.CurrentSnapshot.Timestamp);
        Assert.AreEqual(first.CurrentSnapshot.Items[0].Progress, second.CurrentSnapshot.Items[0].Progress, 0.001d);
    }

    [TestMethod]
    public async Task GetLatestStateAsync_MissingProjectNumberFailurePreventsPersistence()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, _) = await SeedOwnerAndProductAsync(context);
        var captureService = new FakePortfolioSnapshotCaptureDataService
        {
            BuildException = new InvalidOperationException("Required ProjectNumber is missing.")
        };
        captureService.Sources.Add(new PortfolioSnapshotCaptureSource(1, "Sprint 1", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc)));

        var service = CreateStateService(context, captureService);

        try
        {
            await service.GetLatestStateAsync(profileId, CancellationToken.None);
            Assert.Fail("Missing required project numbers should prevent snapshot persistence.");
        }
        catch (InvalidOperationException)
        {
        }
        Assert.AreEqual(0, await context.PortfolioSnapshots.CountAsync());
        Assert.AreEqual(0, await context.PortfolioSnapshotItems.CountAsync());
    }

    [TestMethod]
    public async Task ComparisonQueryService_UsesPersistedSelectedSnapshotsOnly()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, productId) = await SeedOwnerAndProductAsync(context);
        var mapper = new PortfolioSnapshotPersistenceMapper();
        var persistence = new PortfolioSnapshotPersistenceService(context, mapper);
        await persistence.PersistAsync(
            productId,
            "Sprint 1",
            null,
            new PortfolioSnapshot(
                new DateTimeOffset(2026, 3, 7, 0, 0, 0, TimeSpan.Zero),
                [
                    new PortfolioSnapshotItem(productId, "PRJ-100", "WP-1", 0.4d, 10d, WorkPackageLifecycleState.Active)
                ]),
            CancellationToken.None);
        await persistence.PersistAsync(
            productId,
            "Sprint 2",
            null,
            new PortfolioSnapshot(
                new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero),
                [
                    new PortfolioSnapshotItem(productId, "PRJ-100", "WP-1", 0.7d, 12d, WorkPackageLifecycleState.Active)
                ]),
            CancellationToken.None);

        var stateService = CreateStateService(context, new FakePortfolioSnapshotCaptureDataService());
        var comparisonService = new PortfolioComparisonQueryService(
            stateService,
            new PortfolioReadModelMapper(),
            new PortfolioSnapshotComparisonService());

        var result = await comparisonService.GetAsync(profileId, options: null, CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.AreEqual("Sprint 1", result.PreviousSnapshotLabel);
        Assert.AreEqual("Sprint 2", result.CurrentSnapshotLabel);
        Assert.HasCount(1, result.Items);
        Assert.AreEqual(0.3d, result.Items[0].ProgressDelta!.Value, 0.001d);
        Assert.AreEqual(2d, result.Items[0].WeightDelta!.Value, 0.001d);
    }

    private PortfolioReadModelStateService CreateStateService(
        PoToolDbContext context,
        IPortfolioSnapshotCaptureDataService captureService)
        => new(
            context,
            captureService,
            new PortfolioSnapshotFactory(),
            new PortfolioSnapshotPersistenceService(context, new PortfolioSnapshotPersistenceMapper()),
            new PortfolioSnapshotSelectionService(context, new PortfolioSnapshotPersistenceMapper()),
            new ProductAggregationService());

    private static async Task<(int ProfileId, int ProductId)> SeedOwnerAndProductAsync(PoToolDbContext context)
    {
        var profile = new ProfileEntity
        {
            Name = "PO 1"
        };
        context.Profiles.Add(profile);
        await context.SaveChangesAsync();

        var product = new ProductEntity
        {
            ProductOwnerId = profile.Id,
            Name = "Product 1"
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return (profile.Id, product.Id);
    }

    private sealed class FakePortfolioSnapshotCaptureDataService : IPortfolioSnapshotCaptureDataService
    {
        public List<PortfolioSnapshotCaptureSource> Sources { get; } = [];

        public Dictionary<string, IReadOnlyDictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>> InputsBySource { get; } = new(StringComparer.Ordinal);

        public Exception? BuildException { get; init; }

        public Task<IReadOnlyList<PortfolioSnapshotCaptureSource>> GetLatestSourcesAsync(
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PortfolioSnapshotCaptureSource>>(Sources.ToArray());

        public Task<IReadOnlyDictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>> BuildSnapshotInputsByProductAsync(
            int productOwnerId,
            PortfolioSnapshotCaptureSource source,
            CancellationToken cancellationToken)
        {
            if (BuildException is not null)
            {
                throw BuildException;
            }

            return Task.FromResult(InputsBySource.TryGetValue(source.Source, out var inputs)
                ? inputs
                : new Dictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>() as IReadOnlyDictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>);
        }
    }
}
