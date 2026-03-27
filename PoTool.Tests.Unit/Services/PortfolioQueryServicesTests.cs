using PoTool.Api.Services;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioQueryServicesTests
{
    [TestMethod]
    public async Task ProgressQueryService_AppliesFilteringAfterOverallProgressIsComputed()
    {
        var stateService = new StubPortfolioReadModelStateService(CreateState());
        var service = new PortfolioProgressQueryService(stateService, new PortfolioReadModelMapper());

        var result = await service.GetAsync(
            42,
            new PortfolioReadQueryOptions(
                LifecycleState: PortfolioLifecycleState.Retired,
                SortBy: PortfolioReadSortBy.Progress,
                GroupBy: PortfolioReadGroupBy.Project),
            CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.IsTrue(result.PortfolioProgress.HasValue);
        Assert.AreEqual(62.5d, result.PortfolioProgress.Value, 0.001d, "Filtering must not change the computed portfolio progress.");
        Assert.AreEqual(2, result.TotalItemCount);
        Assert.AreEqual(1, result.FilteredItemCount);
        Assert.AreEqual("PRJ-200", result.Items[0].ProjectNumber);
        Assert.AreEqual(PortfolioLifecycleState.Retired, result.Items[0].LifecycleState);
    }

    [TestMethod]
    public async Task SnapshotQueryService_SortsFilteredRowsByRequestedField()
    {
        var stateService = new StubPortfolioReadModelStateService(CreateState());
        var service = new PortfolioSnapshotQueryService(stateService, new PortfolioReadModelMapper());

        var result = await service.GetAsync(
            42,
            new PortfolioReadQueryOptions(
                SortBy: PortfolioReadSortBy.Weight,
                SortDirection: PortfolioReadSortDirection.Desc),
            CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.HasCount(2, result.Items);
        CollectionAssert.AreEqual(new[] { 10d, 6d }, result.Items.Select(item => item.Weight).ToArray());
    }

    [TestMethod]
    public async Task SnapshotQueryService_ReturnsValidEmptyPayloadForPersistedEmptySnapshot()
    {
        var emptySnapshot = new PortfolioSnapshot(
            new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero),
            []);
        var stateService = new StubPortfolioReadModelStateService(
            new PortfolioReadModelState(
                emptySnapshot,
                "Sprint 3",
                null,
                null,
                null,
                0d,
                new Dictionary<int, string> { [1] = "Product A" }));
        var service = new PortfolioSnapshotQueryService(stateService, new PortfolioReadModelMapper());

        var result = await service.GetAsync(42, null, CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.AreEqual("Sprint 3", result.SnapshotLabel);
        Assert.AreEqual(0, result.TotalItemCount);
        Assert.AreEqual(0, result.FilteredItemCount);
        Assert.IsEmpty(result.Items);
    }

    [TestMethod]
    public async Task ComparisonQueryService_UsesDomainComparisonThenFiltersOutput()
    {
        var previous = new PortfolioSnapshot(
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            [
                new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10d, WorkPackageLifecycleState.Active),
                new PortfolioSnapshotItem(2, "PRJ-200", "WP-9", 0.6d, 5d, WorkPackageLifecycleState.Active)
            ]);
        var current = new PortfolioSnapshot(
            previous.Timestamp.AddDays(14),
            [
                new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.7d, 12d, WorkPackageLifecycleState.Active),
                new PortfolioSnapshotItem(2, "PRJ-200", "WP-9", 0.6d, 5d, WorkPackageLifecycleState.Retired)
            ]);

        var stateService = new StubPortfolioReadModelStateService(
            new PortfolioReadModelState(
                current,
                "Sprint 2",
                previous,
                "Sprint 1",
                70d,
                17d,
                new Dictionary<int, string>
                {
                    [1] = "Product A",
                    [2] = "Product B"
                }),
            comparisonState: new PortfolioReadModelComparisonState(
                new PortfolioSnapshotGroupSelection(2, "Sprint 2", current, false),
                new PortfolioSnapshotGroupSelection(1, "Sprint 1", previous, false),
                new Dictionary<int, string>
                {
                    [1] = "Product A",
                    [2] = "Product B"
                },
                ArchivedSnapshotsExcludedNotice: false));
        var service = new PortfolioComparisonQueryService(
            stateService,
            new PortfolioReadModelMapper(),
            new PortfolioSnapshotComparisonService());

        var result = await service.GetAsync(
            42,
            new PortfolioReadQueryOptions(
                LifecycleState: PortfolioLifecycleState.Retired,
                SortBy: PortfolioReadSortBy.Delta),
            CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.AreEqual(2, result.TotalItemCount);
        Assert.AreEqual(1, result.FilteredItemCount);
        Assert.AreEqual("PRJ-200", result.Items[0].ProjectNumber);
        Assert.AreEqual(PortfolioLifecycleState.Retired, result.Items[0].CurrentLifecycleState);
    }

    [TestMethod]
    public async Task ComparisonQueryService_UsesExplicitEarlierSnapshotSelection()
    {
        var previous = new PortfolioSnapshot(
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            [
                new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.2d, 8d, WorkPackageLifecycleState.Active)
            ]);
        var current = new PortfolioSnapshot(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            [
                new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.5d, 10d, WorkPackageLifecycleState.Active)
            ]);

        var stateService = new StubPortfolioReadModelStateService(
            CreateState(),
            comparisonState: new PortfolioReadModelComparisonState(
                new PortfolioSnapshotGroupSelection(30, "Sprint 3", current, false),
                new PortfolioSnapshotGroupSelection(10, "Sprint 1", previous, false),
                new Dictionary<int, string> { [1] = "Product A" },
                ArchivedSnapshotsExcludedNotice: false));
        var service = new PortfolioComparisonQueryService(
            stateService,
            new PortfolioReadModelMapper(),
            new PortfolioSnapshotComparisonService());

        var result = await service.GetAsync(
            42,
            new PortfolioReadQueryOptions(CompareToSnapshotId: 10),
            CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.AreEqual("Sprint 1", result.PreviousSnapshotLabel);
        Assert.AreEqual("Sprint 3", result.CurrentSnapshotLabel);
        Assert.AreEqual(0.3d, result.Items[0].ProgressDelta!.Value, 0.001d);
    }

    [TestMethod]
    public async Task TrendQueryService_ReportsIncreasingPortfolioProgressAcrossPersistedSnapshots()
    {
        var snapshots = new[]
        {
            new PortfolioSnapshotGroupSelection(3, "Sprint 3", CreateSnapshot(0.8d, 18d, new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)), false),
            new PortfolioSnapshotGroupSelection(2, "Sprint 2", CreateSnapshot(0.6d, 16d, new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero)), false),
            new PortfolioSnapshotGroupSelection(1, "Sprint 1", CreateSnapshot(0.4d, 14d, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)), false)
        };
        var stateService = new StubPortfolioReadModelStateService(
            CreateState(),
            historyState: new PortfolioReadModelHistoryState(
                snapshots,
                new Dictionary<int, string> { [1] = "Product A" },
                ArchivedSnapshotsExcludedNotice: false));
        var service = new PortfolioTrendQueryService(
            stateService,
            new PortfolioTrendAnalysisService(new ProductAggregationService(), new PortfolioReadModelMapper()));

        var result = await service.GetAsync(42, new PortfolioReadQueryOptions(SnapshotCount: 3), CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.AreEqual(PortfolioTrendDirection.Increasing, result.PortfolioProgressTrend.Direction);
        Assert.AreEqual(0.2d, result.PortfolioProgressTrend.Delta!.Value, 0.001d);
        Assert.HasCount(3, result.Snapshots);
    }

    [TestMethod]
    public async Task TrendQueryService_KeepsDeterministicOrderingIndependentOfInputOrder()
    {
        var unorderedSnapshots = new[]
        {
            new PortfolioSnapshotGroupSelection(2, "Sprint 2", CreateSnapshot(0.6d, 16d, new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero)), false),
            new PortfolioSnapshotGroupSelection(3, "Sprint 3", CreateSnapshot(0.6d, 16d, new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)), false),
            new PortfolioSnapshotGroupSelection(1, "Sprint 1", CreateSnapshot(0.6d, 16d, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)), false)
        };
        var stateService = new StubPortfolioReadModelStateService(
            CreateState(),
            historyState: new PortfolioReadModelHistoryState(
                unorderedSnapshots,
                new Dictionary<int, string> { [1] = "Product A" },
                ArchivedSnapshotsExcludedNotice: false));
        var service = new PortfolioTrendQueryService(
            stateService,
            new PortfolioTrendAnalysisService(new ProductAggregationService(), new PortfolioReadModelMapper()));

        var result = await service.GetAsync(42, new PortfolioReadQueryOptions(SnapshotCount: 3), CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "Sprint 3", "Sprint 2", "Sprint 1" },
            result.Snapshots.Select(snapshot => snapshot.SnapshotLabel).ToArray());
        Assert.AreEqual(PortfolioTrendDirection.Stable, result.PortfolioProgressTrend.Direction);
    }

    [TestMethod]
    public async Task TrendQueryService_ReportsDecreasingProgressAndWeight()
    {
        var snapshots = new[]
        {
            new PortfolioSnapshotGroupSelection(3, "Sprint 3", CreateSnapshot(0.2d, 10d, new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)), false),
            new PortfolioSnapshotGroupSelection(2, "Sprint 2", CreateSnapshot(0.4d, 12d, new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero)), false),
            new PortfolioSnapshotGroupSelection(1, "Sprint 1", CreateSnapshot(0.6d, 14d, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)), false)
        };
        var stateService = new StubPortfolioReadModelStateService(
            CreateState(),
            historyState: new PortfolioReadModelHistoryState(
                snapshots,
                new Dictionary<int, string> { [1] = "Product A" },
                ArchivedSnapshotsExcludedNotice: false));
        var service = new PortfolioTrendQueryService(
            stateService,
            new PortfolioTrendAnalysisService(new ProductAggregationService(), new PortfolioReadModelMapper()));

        var result = await service.GetAsync(42, new PortfolioReadQueryOptions(SnapshotCount: 3), CancellationToken.None);

        Assert.AreEqual(PortfolioTrendDirection.Decreasing, result.PortfolioProgressTrend.Direction);
        Assert.AreEqual(PortfolioTrendDirection.Decreasing, result.TotalWeightTrend.Direction);
    }

    [TestMethod]
    public async Task TrendQueryService_WithSingleSnapshot_ReturnsNoDeltaOrDirection()
    {
        var snapshots = new[]
        {
            new PortfolioSnapshotGroupSelection(3, "Sprint 3", CreateSnapshot(0.8d, 18d, new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)), false)
        };
        var stateService = new StubPortfolioReadModelStateService(
            CreateState(),
            historyState: new PortfolioReadModelHistoryState(
                snapshots,
                new Dictionary<int, string> { [1] = "Product A" },
                ArchivedSnapshotsExcludedNotice: false));
        var service = new PortfolioTrendQueryService(
            stateService,
            new PortfolioTrendAnalysisService(new ProductAggregationService(), new PortfolioReadModelMapper()));

        var result = await service.GetAsync(42, new PortfolioReadQueryOptions(SnapshotCount: 1), CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.HasCount(1, result.Snapshots);
        Assert.HasCount(1, result.PortfolioProgressTrend.Points);
        Assert.IsNull(result.PortfolioProgressTrend.PreviousValue);
        Assert.IsNull(result.PortfolioProgressTrend.Delta);
        Assert.IsNull(result.PortfolioProgressTrend.Direction);
        Assert.AreEqual(1, result.SnapshotCount);
    }

    [TestMethod]
    public async Task DecisionSignalQueryService_ReportsNewRetiredNoChangeAndArchivedSignals()
    {
        var history = new[]
        {
            new PortfolioSnapshotGroupSelection(3, "Sprint 3", new PortfolioSnapshot(
                new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero),
                [
                    new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.2d, 5d, WorkPackageLifecycleState.Active),
                    new PortfolioSnapshotItem(1, "PRJ-100", "WP-2", 0.4d, 3d, WorkPackageLifecycleState.Retired),
                    new PortfolioSnapshotItem(1, "PRJ-100", "WP-3", 0.6d, 2d, WorkPackageLifecycleState.Active)
                ]), false),
            new PortfolioSnapshotGroupSelection(2, "Sprint 2", new PortfolioSnapshot(
                new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero),
                [
                    new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.2d, 5d, WorkPackageLifecycleState.Active),
                    new PortfolioSnapshotItem(1, "PRJ-100", "WP-2", 0.4d, 3d, WorkPackageLifecycleState.Active)
                ]), false),
            new PortfolioSnapshotGroupSelection(1, "Sprint 1", new PortfolioSnapshot(
                new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                [
                    new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.2d, 5d, WorkPackageLifecycleState.Active),
                    new PortfolioSnapshotItem(1, "PRJ-100", "WP-2", 0.4d, 3d, WorkPackageLifecycleState.Active)
                ]), false)
        };

        var current = history[0];
        var comparison = history[1];
        var stateService = new StubPortfolioReadModelStateService(
            CreateState(),
            historyState: new PortfolioReadModelHistoryState(
                history,
                new Dictionary<int, string> { [1] = "Product A" },
                ArchivedSnapshotsExcludedNotice: true),
            comparisonState: new PortfolioReadModelComparisonState(
                current,
                comparison,
                new Dictionary<int, string> { [1] = "Product A" },
                ArchivedSnapshotsExcludedNotice: true));
        var service = new PortfolioDecisionSignalQueryService(
            stateService,
            new PortfolioTrendAnalysisService(new ProductAggregationService(), new PortfolioReadModelMapper()),
            new PortfolioDecisionSignalService(),
            new PortfolioSnapshotComparisonService(),
            new PortfolioReadModelMapper());

        var signals = await service.GetAsync(42, new PortfolioReadQueryOptions(SnapshotCount: 3), CancellationToken.None);

        CollectionAssert.IsSubsetOf(
            new[]
            {
                PortfolioDecisionSignalType.NewWorkPackage,
                PortfolioDecisionSignalType.RetiredWorkPackage,
                PortfolioDecisionSignalType.RepeatedNoChange,
                PortfolioDecisionSignalType.ArchivedSnapshotExcludedNotice
            },
            signals.Select(signal => signal.Type).Distinct().ToArray());
    }

    [TestMethod]
    public async Task DecisionSignalQueryService_WithSingleSnapshot_ReturnsEmptySignals()
    {
        var current = new PortfolioSnapshotGroupSelection(
            3,
            "Sprint 3",
            CreateSnapshot(0.8d, 18d, new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)),
            false);
        var stateService = new StubPortfolioReadModelStateService(
            CreateState(),
            historyState: new PortfolioReadModelHistoryState(
                [current],
                new Dictionary<int, string> { [1] = "Product A" },
                ArchivedSnapshotsExcludedNotice: false),
            comparisonState: new PortfolioReadModelComparisonState(
                current,
                ComparisonSnapshot: null,
                new Dictionary<int, string> { [1] = "Product A" },
                ArchivedSnapshotsExcludedNotice: false));
        var service = new PortfolioDecisionSignalQueryService(
            stateService,
            new PortfolioTrendAnalysisService(new ProductAggregationService(), new PortfolioReadModelMapper()),
            new PortfolioDecisionSignalService(),
            new PortfolioSnapshotComparisonService(),
            new PortfolioReadModelMapper());

        var signals = await service.GetAsync(42, new PortfolioReadQueryOptions(SnapshotCount: 1), CancellationToken.None);

        Assert.IsEmpty(signals);
    }

    private static PortfolioReadModelState CreateState()
    {
        var snapshot = new PortfolioSnapshot(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            [
                new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10d, WorkPackageLifecycleState.Active),
                new PortfolioSnapshotItem(2, "PRJ-200", "WP-2", 0.8d, 6d, WorkPackageLifecycleState.Retired)
            ]);

        return new PortfolioReadModelState(
            snapshot,
            "Sprint 3",
            null,
            null,
            62.5d,
            16d,
            new Dictionary<int, string>
            {
                [1] = "Product A",
                [2] = "Product B"
            });
    }

    private static PortfolioSnapshot CreateSnapshot(double progress, double weight, DateTimeOffset timestamp)
        => new(
            timestamp,
            [
                new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", progress, weight, WorkPackageLifecycleState.Active)
            ]);

    private sealed class StubPortfolioReadModelStateService : IPortfolioReadModelStateService
    {
        private readonly PortfolioReadModelState _state;
        private readonly PortfolioReadModelHistoryState? _historyState;
        private readonly PortfolioReadModelComparisonState? _comparisonState;

        public StubPortfolioReadModelStateService(
            PortfolioReadModelState state,
            PortfolioReadModelHistoryState? historyState = null,
            PortfolioReadModelComparisonState? comparisonState = null)
        {
            _state = state;
            _historyState = historyState;
            _comparisonState = comparisonState;
        }

        public Task<PortfolioReadModelState?> GetLatestStateAsync(int productOwnerId, CancellationToken cancellationToken)
            => Task.FromResult<PortfolioReadModelState?>(_state);

        public Task<PortfolioReadModelHistoryState?> GetHistoryStateAsync(
            int productOwnerId,
            PortfolioReadQueryOptions? options,
            CancellationToken cancellationToken)
            => Task.FromResult(_historyState);

        public Task<PortfolioReadModelComparisonState?> GetComparisonStateAsync(
            int productOwnerId,
            PortfolioReadQueryOptions? options,
            CancellationToken cancellationToken)
            => Task.FromResult(_comparisonState);
    }
}
