using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioSnapshotCaptureOrchestratorTests
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
    public async Task CaptureLatestAsync_PersistsEmptySnapshotForProductWithoutInputs()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, productId) = await SeedOwnerAndProductAsync(context);
        var captureService = new FakePortfolioSnapshotCaptureDataService();
        captureService.Sources.Add(new PortfolioSnapshotCaptureSource(
            1,
            "Sprint 1",
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc)));

        var orchestrator = CreateOrchestrator(context, captureService);

        var result = await orchestrator.CaptureLatestAsync(profileId, CancellationToken.None);

        Assert.AreEqual(1, result.CreatedSnapshotCount);
        var persisted = await context.PortfolioSnapshots.Include(snapshot => snapshot.Items).SingleAsync();
        Assert.AreEqual(productId, persisted.ProductId);
        Assert.AreEqual("Sprint 1", persisted.Source);
        Assert.AreEqual(0, persisted.Items.Count);
    }

    [TestMethod]
    public async Task CaptureLatestAsync_IsIdempotentForRepeatedRetries()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, productId) = await SeedOwnerAndProductAsync(context);
        var captureService = new FakePortfolioSnapshotCaptureDataService();
        captureService.Sources.Add(new PortfolioSnapshotCaptureSource(
            1,
            "Sprint 1",
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc)));
        captureService.InputsBySource["Sprint 1"] = new Dictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>
        {
            [productId] =
            [
                new PortfolioSnapshotFactoryEpicInput(productId, "PRJ-100", "WP-1", 0.4d, 10d)
            ]
        };

        var orchestrator = CreateOrchestrator(context, captureService);

        var first = await orchestrator.CaptureLatestAsync(profileId, CancellationToken.None);
        var second = await orchestrator.CaptureLatestAsync(profileId, CancellationToken.None);

        Assert.AreEqual(1, first.CreatedSnapshotCount);
        Assert.AreEqual(0, first.ExistingSnapshotCount);
        Assert.AreEqual(0, second.CreatedSnapshotCount);
        Assert.AreEqual(1, second.ExistingSnapshotCount);
        Assert.AreEqual(1, await context.PortfolioSnapshots.CountAsync());
    }

    private PortfolioSnapshotCaptureOrchestrator CreateOrchestrator(
        PoToolDbContext context,
        IPortfolioSnapshotCaptureDataService captureService)
        => new(
            context,
            captureService,
            new PortfolioSnapshotFactory(),
            new PortfolioSnapshotPersistenceService(
                context,
                new PortfolioSnapshotPersistenceMapper(),
                NullLogger<PortfolioSnapshotPersistenceService>.Instance),
            new PortfolioSnapshotSelectionService(
                context,
                new PortfolioSnapshotPersistenceMapper(),
                NullLogger<PortfolioSnapshotSelectionService>.Instance));

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

        public Task<IReadOnlyList<PortfolioSnapshotCaptureSource>> GetLatestSourcesAsync(
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PortfolioSnapshotCaptureSource>>(Sources.ToArray());

        public Task<IReadOnlyDictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>> BuildSnapshotInputsByProductAsync(
            int productOwnerId,
            PortfolioSnapshotCaptureSource source,
            CancellationToken cancellationToken)
            => Task.FromResult(InputsBySource.TryGetValue(source.Source, out var inputs)
                ? inputs
                : new Dictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>() as IReadOnlyDictionary<int, IReadOnlyList<PortfolioSnapshotFactoryEpicInput>>);
    }
}
