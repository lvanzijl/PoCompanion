using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class CdcTestDataSeederTests
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
    public async Task SeedAsync_CreatesExpectedDeterministicDatasetAndIsIdempotent()
    {
        await using var context = new PoToolDbContext(_options);
        var seeder = new CdcTestDataSeeder(context);

        var first = await seeder.SeedAsync(CancellationToken.None);
        var second = await seeder.SeedAsync(CancellationToken.None);

        Assert.AreEqual(first.ProductOwnerId, second.ProductOwnerId);
        Assert.AreEqual(first.ProductAId, second.ProductAId);
        Assert.AreEqual(first.ProductBId, second.ProductBId);
        Assert.AreEqual(first.ProductCId, second.ProductCId);
        Assert.AreEqual(3, await context.Products.CountAsync());
        Assert.AreEqual(1, await context.Profiles.CountAsync());
        Assert.AreEqual(1, await context.Teams.CountAsync());
        Assert.AreEqual(5, await context.Sprints.CountAsync());
        Assert.AreEqual(18, await context.PortfolioSnapshots.CountAsync());
        Assert.AreEqual(53, await context.PortfolioSnapshotItems.CountAsync());

        var emptyPortfolio = first.GetSnapshot($"{CdcTestDataSeeder.ProductBKey}:{CdcTestDataSeeder.EmptyPortfolioSource}");
        var sprint2Fallback = first.GetSnapshot($"{CdcTestDataSeeder.ProductBKey}:{CdcTestDataSeeder.Sprint2Source}");

        Assert.AreEqual(DateTimeOffset.UnixEpoch, emptyPortfolio.Timestamp);
        Assert.AreEqual(0, emptyPortfolio.ItemCount);
        Assert.AreEqual(new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero), sprint2Fallback.Timestamp);
        Assert.AreEqual(0, sprint2Fallback.ItemCount);
        Assert.AreEqual(emptyPortfolio.SnapshotId, second.GetSnapshot($"{CdcTestDataSeeder.ProductBKey}:{CdcTestDataSeeder.EmptyPortfolioSource}").SnapshotId);
        Assert.AreEqual(sprint2Fallback.SnapshotId, second.GetSnapshot($"{CdcTestDataSeeder.ProductBKey}:{CdcTestDataSeeder.Sprint2Source}").SnapshotId);
    }

    [TestMethod]
    public async Task SeededDataset_SupportsDeterministicSelectionGroupingAndSnapshotCountScenarios()
    {
        await using var context = new PoToolDbContext(_options);
        var dataset = await new CdcTestDataSeeder(context).SeedAsync(CancellationToken.None);
        var selectionService = CreateSelectionService(context);
        var stateService = CreateStateService(context);

        var latestProductA = await selectionService.GetLatestAsync(dataset.ProductAId, CancellationToken.None);
        var previousProductA = await selectionService.GetPreviousAsync(dataset.ProductAId, CancellationToken.None);
        var groupedSprint2 = await selectionService.GetPortfolioSnapshotBySourceAsync(
            [dataset.ProductAId, dataset.ProductBId, dataset.ProductCId],
            new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero),
            $"  {CdcTestDataSeeder.Sprint2Source}  ",
            CancellationToken.None);
        var productBHistory = await selectionService.GetPortfolioSnapshotsAsync(
            [dataset.ProductBId],
            count: 10,
            rangeStartUtc: null,
            rangeEndUtc: null,
            CancellationToken.None);

        var historyCountOne = await stateService.GetHistoryStateAsync(
            dataset.ProductOwnerId,
            new PortfolioReadQueryOptions(SnapshotCount: 1),
            CancellationToken.None);
        var historyCountTwo = await stateService.GetHistoryStateAsync(
            dataset.ProductOwnerId,
            new PortfolioReadQueryOptions(SnapshotCount: 2),
            CancellationToken.None);
        var historyAll = await stateService.GetHistoryStateAsync(
            dataset.ProductOwnerId,
            new PortfolioReadQueryOptions(SnapshotCount: 20),
            CancellationToken.None);

        Assert.IsNotNull(latestProductA);
        Assert.AreEqual(CdcTestDataSeeder.Sprint5Source, latestProductA.Source);
        Assert.IsNotNull(previousProductA);
        Assert.AreEqual(CdcTestDataSeeder.Sprint4BSource, previousProductA.Source);
        Assert.IsNotNull(groupedSprint2);
        Assert.AreEqual(CdcTestDataSeeder.Sprint2Source, groupedSprint2.Source);
        Assert.HasCount(7, groupedSprint2.Snapshot.Items);
        CollectionAssert.AreEqual(
            new[] { dataset.ProductAId, dataset.ProductCId },
            groupedSprint2.Snapshot.Items.Select(item => item.ProductId).Distinct().OrderBy(id => id).ToArray());
        Assert.HasCount(6, productBHistory);
        CollectionAssert.AreEqual(
            new[]
            {
                CdcTestDataSeeder.Sprint5Source,
                CdcTestDataSeeder.Sprint4BSource,
                CdcTestDataSeeder.Sprint4ASource,
                CdcTestDataSeeder.Sprint3Source,
                CdcTestDataSeeder.Sprint2Source,
                CdcTestDataSeeder.EmptyPortfolioSource
            },
            productBHistory.Select(snapshot => snapshot.Source).ToArray());

        Assert.IsNotNull(historyCountOne);
        Assert.HasCount(1, historyCountOne.Snapshots);
        Assert.AreEqual(CdcTestDataSeeder.Sprint5Source, historyCountOne.Snapshots[0].Source);
        Assert.IsNotNull(historyCountTwo);
        Assert.HasCount(2, historyCountTwo.Snapshots);
        CollectionAssert.AreEqual(
            new[] { CdcTestDataSeeder.Sprint5Source, CdcTestDataSeeder.Sprint4BSource },
            historyCountTwo.Snapshots.Select(snapshot => snapshot.Source).ToArray());
        Assert.IsNotNull(historyAll);
        Assert.HasCount(7, historyAll.Snapshots);
        CollectionAssert.AreEqual(
            new[]
            {
                CdcTestDataSeeder.Sprint5Source,
                CdcTestDataSeeder.Sprint4BSource,
                CdcTestDataSeeder.Sprint4ASource,
                CdcTestDataSeeder.Sprint3Source,
                CdcTestDataSeeder.Sprint2Source,
                CdcTestDataSeeder.Sprint1Source,
                CdcTestDataSeeder.EmptyPortfolioSource
            },
            historyAll.Snapshots.Select(snapshot => snapshot.Source).ToArray());
    }

    [TestMethod]
    public async Task SeededDataset_DrivesExpectedTrendDeltaAndSignalBehavior()
    {
        await using var context = new PoToolDbContext(_options);
        var dataset = await new CdcTestDataSeeder(context).SeedAsync(CancellationToken.None);
        var trendService = CreateTrendService(context);
        var signalService = CreateSignalService(context);

        var singleSnapshotTrend = await trendService.GetAsync(
            dataset.ProductOwnerId,
            new PortfolioReadQueryOptions(SnapshotCount: 1),
            CancellationToken.None);
        var singleSnapshotSignals = await signalService.GetAsync(
            dataset.ProductOwnerId,
            new PortfolioReadQueryOptions(SnapshotCount: 1),
            CancellationToken.None);
        var fullTrend = await trendService.GetAsync(
            dataset.ProductOwnerId,
            new PortfolioReadQueryOptions(SnapshotCount: 7),
            CancellationToken.None);
        var fullSignals = await signalService.GetAsync(
            dataset.ProductOwnerId,
            new PortfolioReadQueryOptions(SnapshotCount: 7),
            CancellationToken.None);

        Assert.IsTrue(singleSnapshotTrend.HasData);
        Assert.AreEqual(CdcTestDataSeeder.Sprint5Source, singleSnapshotTrend.Snapshots[0].SnapshotLabel);
        Assert.IsNull(singleSnapshotTrend.PortfolioProgressTrend.Delta);
        Assert.IsNull(singleSnapshotTrend.TotalWeightTrend.Delta);
        Assert.IsEmpty(singleSnapshotSignals);

        Assert.IsTrue(fullTrend.HasData);
        Assert.AreEqual(CdcTestDataSeeder.Sprint5Source, fullTrend.Snapshots[0].SnapshotLabel);
        Assert.AreEqual(CdcTestDataSeeder.Sprint4BSource, fullTrend.Snapshots[1].SnapshotLabel);
        Assert.IsNotNull(fullTrend.PortfolioProgressTrend.Delta);
        Assert.IsGreaterThan(0d, fullTrend.PortfolioProgressTrend.Delta.Value);
        Assert.AreEqual(-1d, fullTrend.TotalWeightTrend.Delta!.Value, 0.0001d);
        CollectionAssert.IsSubsetOf(
            new[]
            {
                PortfolioDecisionSignalType.ProgressImproving,
                PortfolioDecisionSignalType.WeightDecreasing,
                PortfolioDecisionSignalType.NewWorkPackage,
                PortfolioDecisionSignalType.RetiredWorkPackage,
                PortfolioDecisionSignalType.RepeatedNoChange
            },
            fullSignals.Select(signal => signal.Type).Distinct().ToArray());
    }

    [TestMethod]
    public async Task SeededDataset_PreservesExpectedAggregationForCompletedAndActiveProducts()
    {
        await using var context = new PoToolDbContext(_options);
        var dataset = await new CdcTestDataSeeder(context).SeedAsync(CancellationToken.None);
        var progressService = CreateProgressService(context);
        var selectionService = CreateSelectionService(context);
        var aggregationService = new ProductAggregationService();

        var portfolioProgress = await progressService.GetAsync(dataset.ProductOwnerId, options: null, CancellationToken.None);
        var productC = await selectionService.GetLatestAsync(dataset.ProductCId, CancellationToken.None);

        Assert.IsTrue(portfolioProgress.HasData);
        Assert.AreEqual(CdcTestDataSeeder.Sprint5Source, portfolioProgress.SnapshotLabel);
        Assert.AreEqual(42d, portfolioProgress.TotalWeight, 0.0001d);
        Assert.AreEqual(86.19047619d, portfolioProgress.PortfolioProgress!.Value, 0.0001d);
        Assert.IsNotNull(productC);

        var completedAggregation = aggregationService.Compute(new ProductAggregationRequest(
            productC.Snapshot.Items
                .Where(item => item.LifecycleState == WorkPackageLifecycleState.Active)
                .Select(item => new ProductAggregationEpicInput(
                    item.Progress * 100d,
                    EpicForecastConsumed: null,
                    EpicForecastRemaining: null,
                    item.TotalWeight,
                    IsExcluded: item.TotalWeight <= 0d))
                .ToList()));

        Assert.IsNotNull(completedAggregation.ProductProgress);
        Assert.AreEqual(100d, completedAggregation.ProductProgress.Value, 0.0001d);
        Assert.AreEqual(15d, completedAggregation.TotalWeight, 0.0001d);
    }

    private PortfolioSnapshotSelectionService CreateSelectionService(PoToolDbContext context)
        => new(
            context,
            new PortfolioSnapshotPersistenceMapper(),
            NullLogger<PortfolioSnapshotSelectionService>.Instance);

    private PortfolioReadModelStateService CreateStateService(PoToolDbContext context)
        => new(
            context,
            CreateSelectionService(context),
            new ProductAggregationService());

    private PortfolioTrendQueryService CreateTrendService(PoToolDbContext context)
        => new(
            CreateStateService(context),
            new PortfolioTrendAnalysisService(new ProductAggregationService(), new PortfolioReadModelMapper()));

    private PortfolioDecisionSignalQueryService CreateSignalService(PoToolDbContext context)
        => new(
            CreateStateService(context),
            new PortfolioTrendAnalysisService(new ProductAggregationService(), new PortfolioReadModelMapper()),
            new PortfolioDecisionSignalService(),
            new PortfolioSnapshotComparisonService(),
            new PortfolioReadModelMapper());

    private PortfolioProgressQueryService CreateProgressService(PoToolDbContext context)
        => new(
            CreateStateService(context),
            new PortfolioReadModelMapper());
}
