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
                }));
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

    private sealed class StubPortfolioReadModelStateService : IPortfolioReadModelStateService
    {
        private readonly PortfolioReadModelState _state;

        public StubPortfolioReadModelStateService(PortfolioReadModelState state)
        {
            _state = state;
        }

        public Task<PortfolioReadModelState?> GetLatestStateAsync(int productOwnerId, CancellationToken cancellationToken)
            => Task.FromResult<PortfolioReadModelState?>(_state);
    }
}
