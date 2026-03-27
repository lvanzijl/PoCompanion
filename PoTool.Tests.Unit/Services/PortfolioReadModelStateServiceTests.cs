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
    public async Task GetLatestStateAsync_DoesNotPersistSnapshotsWhenNoSnapshotsExist()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, _) = await SeedOwnerAndProductAsync(context);
        var service = CreateStateService(context);

        var state = await service.GetLatestStateAsync(profileId, CancellationToken.None);

        Assert.IsNull(state);
        Assert.AreEqual(0, await context.PortfolioSnapshots.CountAsync());
        Assert.AreEqual(0, await context.PortfolioSnapshotItems.CountAsync());
    }

    [TestMethod]
    public async Task GetLatestStateAsync_UsesPersistedSelectionWhenSnapshotsExist()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, productId) = await SeedOwnerAndProductAsync(context);
        var persistence = CreatePersistenceService(context);
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
                    new PortfolioSnapshotItem(productId, "PRJ-100", "WP-1", 0.7d, 10d, WorkPackageLifecycleState.Active)
                ]),
            CancellationToken.None);

        var service = CreateStateService(context);

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
    public async Task QueryServices_DoNotCreateSnapshotsDuringReadPaths()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, _) = await SeedOwnerAndProductAsync(context);
        var stateService = CreateStateService(context);
        var mapper = new PortfolioReadModelMapper();

        var progressService = new PortfolioProgressQueryService(stateService, mapper);
        var snapshotService = new PortfolioSnapshotQueryService(stateService, mapper);
        var comparisonService = new PortfolioComparisonQueryService(
            stateService,
            mapper,
            new PortfolioSnapshotComparisonService());
        var trendService = new PortfolioTrendQueryService(
            stateService,
            new PortfolioTrendAnalysisService(new ProductAggregationService(), mapper));
        var signalService = new PortfolioDecisionSignalQueryService(
            stateService,
            new PortfolioTrendAnalysisService(new ProductAggregationService(), mapper),
            new PortfolioDecisionSignalService(),
            new PortfolioSnapshotComparisonService(),
            mapper);

        var progress = await progressService.GetAsync(profileId, null, CancellationToken.None);
        var snapshot = await snapshotService.GetAsync(profileId, null, CancellationToken.None);
        var comparison = await comparisonService.GetAsync(profileId, null, CancellationToken.None);
        var trend = await trendService.GetAsync(profileId, null, CancellationToken.None);
        var signals = await signalService.GetAsync(profileId, null, CancellationToken.None);

        Assert.IsFalse(progress.HasData);
        Assert.IsFalse(snapshot.HasData);
        Assert.IsFalse(comparison.HasData);
        Assert.IsFalse(trend.HasData);
        Assert.IsEmpty(signals);
        Assert.AreEqual(0, await context.PortfolioSnapshots.CountAsync());
        Assert.AreEqual(0, await context.PortfolioSnapshotItems.CountAsync());
    }

    [TestMethod]
    public async Task ComparisonQueryService_UsesPersistedSelectedSnapshotsOnly()
    {
        await using var context = new PoToolDbContext(_options);
        var (profileId, productId) = await SeedOwnerAndProductAsync(context);
        var persistence = CreatePersistenceService(context);
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

        var comparisonService = new PortfolioComparisonQueryService(
            CreateStateService(context),
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

    private PortfolioReadModelStateService CreateStateService(PoToolDbContext context)
        => new(
            context,
            new PortfolioSnapshotSelectionService(
                context,
                new PortfolioSnapshotPersistenceMapper(),
                NullLogger<PortfolioSnapshotSelectionService>.Instance),
            new ProductAggregationService());

    private static PortfolioSnapshotPersistenceService CreatePersistenceService(PoToolDbContext context)
        => new(
            context,
            new PortfolioSnapshotPersistenceMapper(),
            NullLogger<PortfolioSnapshotPersistenceService>.Instance);

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
}
