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
        Assert.IsEmpty(persisted.Items);
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

    [TestMethod]
    public async Task CaptureLatestAsync_EmptyOwner_UsesLatestSprintAsFallbackTimestamp()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, productId) = await SeedOwnerAndProductAsync(context);
        var teamId = await SeedTeamAsync(context);
        await SeedSprintAsync(
            context,
            teamId,
            "Sprint 1",
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc));
        await SeedSprintAsync(
            context,
            teamId,
            "Sprint 2A",
            new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc));
        var latestSprint = await SeedSprintAsync(
            context,
            teamId,
            "Sprint 2B",
            new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc));

        var orchestrator = CreateOrchestrator(context, new FakePortfolioSnapshotCaptureDataService());

        var first = await orchestrator.CaptureLatestAsync(profileId, CancellationToken.None);
        var second = await orchestrator.CaptureLatestAsync(profileId, CancellationToken.None);

        Assert.AreEqual(1, first.SourceCount);
        Assert.AreEqual(1, first.CreatedSnapshotCount);
        Assert.AreEqual(0, second.CreatedSnapshotCount);
        Assert.AreEqual(1, second.ExistingSnapshotCount);

        var persisted = await context.PortfolioSnapshots.Include(snapshot => snapshot.Items).SingleAsync();
        Assert.AreEqual(productId, persisted.ProductId);
        Assert.AreEqual("Sprint 2B", persisted.Source);
        Assert.AreEqual(latestSprint.EndDateUtc!.Value, DateTime.SpecifyKind(persisted.TimestampUtc, DateTimeKind.Utc));
        Assert.IsEmpty(persisted.Items);
    }

    [TestMethod]
    public async Task CaptureLatestAsync_EmptyOwner_NoSprints_UsesUnixEpochFallback()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, productId) = await SeedOwnerAndProductAsync(context);

        var orchestrator = CreateOrchestrator(context, new FakePortfolioSnapshotCaptureDataService());

        var first = await orchestrator.CaptureLatestAsync(profileId, CancellationToken.None);
        var second = await orchestrator.CaptureLatestAsync(profileId, CancellationToken.None);

        Assert.AreEqual(1, first.SourceCount);
        Assert.AreEqual(1, first.CreatedSnapshotCount);
        Assert.AreEqual(0, second.CreatedSnapshotCount);
        Assert.AreEqual(1, second.ExistingSnapshotCount);

        var persisted = await context.PortfolioSnapshots.Include(snapshot => snapshot.Items).SingleAsync();
        Assert.AreEqual(productId, persisted.ProductId);
        Assert.AreEqual("Empty portfolio", persisted.Source);
        Assert.AreEqual(DateTime.UnixEpoch, DateTime.SpecifyKind(persisted.TimestampUtc, DateTimeKind.Utc));
        Assert.IsEmpty(persisted.Items);
    }

    [TestMethod]
    public async Task SnapshotSelection_WithFallbackTimestamps_RemainsDeterministic()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, productId) = await SeedOwnerAndProductAsync(context);
        var teamId = await SeedTeamAsync(context);
        var fallbackEndUtc = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc);
        await SeedSprintAsync(
            context,
            teamId,
            "Sprint fallback",
            new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc),
            fallbackEndUtc);

        var orchestrator = CreateOrchestrator(context, new FakePortfolioSnapshotCaptureDataService());
        var persistenceService = CreatePersistenceService(context);
        var selectionService = CreateSelectionService(context);

        await orchestrator.CaptureLatestAsync(profileId, CancellationToken.None);
        await persistenceService.PersistAsync(
            productId,
            "Manual A",
            null,
            new PortfolioSnapshot(new DateTimeOffset(fallbackEndUtc, TimeSpan.Zero), []),
            CancellationToken.None);
        await persistenceService.PersistAsync(
            productId,
            "Manual B",
            null,
            new PortfolioSnapshot(new DateTimeOffset(fallbackEndUtc, TimeSpan.Zero), []),
            CancellationToken.None);

        var firstRead = await selectionService.GetPortfolioSnapshotsAsync(
            [productId],
            count: 3,
            rangeStartUtc: null,
            rangeEndUtc: null,
            CancellationToken.None);
        var secondRead = await selectionService.GetPortfolioSnapshotsAsync(
            [productId],
            count: 3,
            rangeStartUtc: null,
            rangeEndUtc: null,
            CancellationToken.None);

        var expectedOrder = new[] { "Manual B", "Manual A", "Sprint fallback" };
        CollectionAssert.AreEqual(expectedOrder, firstRead.Select(snapshot => snapshot.Source).ToArray());
        CollectionAssert.AreEqual(expectedOrder, secondRead.Select(snapshot => snapshot.Source).ToArray());
        CollectionAssert.AreEqual(
            firstRead.Select(snapshot => snapshot.SnapshotId).ToArray(),
            secondRead.Select(snapshot => snapshot.SnapshotId).ToArray());
    }

    private PortfolioSnapshotCaptureOrchestrator CreateOrchestrator(
        PoToolDbContext context,
        IPortfolioSnapshotCaptureDataService captureService)
        => new(
            context,
            captureService,
            new PortfolioSnapshotFactory(),
            CreatePersistenceService(context),
            CreateSelectionService(context));

    private static PortfolioSnapshotPersistenceService CreatePersistenceService(PoToolDbContext context)
        => new(
            context,
            new PortfolioSnapshotPersistenceMapper(),
            NullLogger<PortfolioSnapshotPersistenceService>.Instance);

    private static PortfolioSnapshotSelectionService CreateSelectionService(PoToolDbContext context)
        => new(
            context,
            new PortfolioSnapshotPersistenceMapper(),
            NullLogger<PortfolioSnapshotSelectionService>.Instance);

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

    private static async Task<int> SeedTeamAsync(PoToolDbContext context)
    {
        var team = new TeamEntity
        {
            Name = "Team 1",
            TeamAreaPath = "Area/Team 1"
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();
        return team.Id;
    }

    private static async Task<SprintEntity> SeedSprintAsync(
        PoToolDbContext context,
        int teamId,
        string name,
        DateTime startDateUtc,
        DateTime endDateUtc)
    {
        var sprint = new SprintEntity
        {
            TeamId = teamId,
            Path = $"\\Project\\{name}",
            Name = name,
            StartUtc = new DateTimeOffset(startDateUtc, TimeSpan.Zero),
            StartDateUtc = startDateUtc,
            EndUtc = new DateTimeOffset(endDateUtc, TimeSpan.Zero),
            EndDateUtc = endDateUtc,
            LastSyncedUtc = new DateTimeOffset(endDateUtc, TimeSpan.Zero),
            LastSyncedDateUtc = endDateUtc
        };

        context.Sprints.Add(sprint);
        await context.SaveChangesAsync();
        return sprint;
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
