using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioSnapshotValidationServiceTests
{
    private static readonly IPortfolioSnapshotValidationService ValidationService = new PortfolioSnapshotValidationService();

    [TestMethod]
    public void PortfolioSnapshot_AllowsProjectLevelOnlyRows()
    {
        var timestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var snapshot = new PortfolioSnapshot(timestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", null, 0.4d, 20),
            new PortfolioSnapshotItem(1, "PRJ-200", null, 0.6d, 10)
        ]);

        Assert.AreEqual(timestamp, snapshot.Timestamp);
        Assert.HasCount(2, snapshot.Items);
        Assert.AreEqual(1, snapshot.Items[0].ProductId);
        Assert.AreEqual("PRJ-100", snapshot.Items[0].ProjectNumber);
        Assert.IsNull(snapshot.Items[0].WorkPackage);
        Assert.AreEqual(0.4d, snapshot.Items[0].Progress, 0.001d);
        Assert.AreEqual(20d, snapshot.Items[0].TotalWeight, 0.001d);
        Assert.AreEqual(WorkPackageLifecycleState.Active, snapshot.Items[0].LifecycleState);
    }

    [TestMethod]
    public void PortfolioSnapshot_AllowsFullWorkPackageBreakdownRows()
    {
        var timestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var snapshot = new PortfolioSnapshot(timestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10),
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-2", 0.6d, 15)
        ]);

        Assert.AreEqual(timestamp, snapshot.Timestamp);
        Assert.HasCount(2, snapshot.Items);
        CollectionAssert.AreEqual(
            new[] { "WP-1", "WP-2" },
            snapshot.Items.Select(item => item.WorkPackage).ToArray());
        Assert.AreEqual(0.4d, snapshot.Items[0].Progress, 0.001d);
        Assert.AreEqual(15d, snapshot.Items[1].TotalWeight, 0.001d);
    }

    [TestMethod]
    public void PortfolioSnapshot_RejectsMixedProjectAndWorkPackageRowsWithinSameProject()
    {
        var timestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.ThrowsExactly<ArgumentException>(() => new PortfolioSnapshot(timestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", null, 0.4d, 10),
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.6d, 15)
        ]));
    }

    [TestMethod]
    public void PortfolioSnapshotItem_AllowsUnitIntervalProgress()
    {
        var item = new PortfolioSnapshotItem(1, "PRJ-100", null, 0.5d, 10d);

        Assert.AreEqual(0.5d, item.Progress, 0.001d);
    }

    [TestMethod]
    public void PortfolioSnapshotItem_RejectsProgressOutsideUnitInterval()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new PortfolioSnapshotItem(1, "PRJ-100", null, 50d, 10d));
    }

    [TestMethod]
    public void PortfolioSnapshotItem_DoesNotOwnIndependentTimestamp()
    {
        var timestampProperty = typeof(PortfolioSnapshotItem).GetProperty(nameof(PortfolioSnapshot.Timestamp));

        Assert.IsNull(timestampProperty);
    }

    [TestMethod]
    public void ValidateCreation_RejectsIncompleteWorkPackageBreakdownForProject()
    {
        var previousTimestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var currentTimestamp = previousTimestamp.AddDays(7);

        var previousSnapshot = new PortfolioSnapshot(previousTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10),
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-2", 0.6d, 15)
        ]);

        var candidateSnapshot = new PortfolioSnapshot(currentTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.65d, 12)
        ]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ValidationService.ValidateCreation([previousSnapshot], candidateSnapshot));
    }

    [TestMethod]
    public void ValidateCreation_RejectsProjectLevelRowsAfterHistoricalWorkPackageBreakdown()
    {
        var previousTimestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var currentTimestamp = previousTimestamp.AddDays(7);

        var previousSnapshot = new PortfolioSnapshot(previousTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10)
        ]);

        var candidateSnapshot = new PortfolioSnapshot(currentTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", null, 0.65d, 12)
        ]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ValidationService.ValidateCreation([previousSnapshot], candidateSnapshot));
    }

    [TestMethod]
    public void ValidateCreation_RejectsMissingHistoricalActiveWorkPackageWhenProjectIsOmitted()
    {
        var previousTimestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var currentTimestamp = previousTimestamp.AddDays(7);

        var previousSnapshot = new PortfolioSnapshot(previousTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10)
        ]);

        var candidateSnapshot = new PortfolioSnapshot(currentTimestamp,
        [
            new PortfolioSnapshotItem(2, "PRJ-200", null, 0.65d, 12)
        ]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ValidationService.ValidateCreation([previousSnapshot], candidateSnapshot));
    }

    [TestMethod]
    public void ValidateCreation_AllowsRetiredItemsToDropOutOfFutureSnapshots()
    {
        var initialTimestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var retiredTimestamp = initialTimestamp.AddDays(7);
        var futureTimestamp = retiredTimestamp.AddDays(7);

        var initialSnapshot = new PortfolioSnapshot(initialTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10)
        ]);
        var retiredSnapshot = new PortfolioSnapshot(retiredTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10, WorkPackageLifecycleState.Retired)
        ]);
        var candidateSnapshot = new PortfolioSnapshot(futureTimestamp,
        [
            new PortfolioSnapshotItem(2, "PRJ-200", null, 0.65d, 12)
        ]);

        ValidationService.ValidateCreation([initialSnapshot, retiredSnapshot], candidateSnapshot);
    }

    [TestMethod]
    public void ValidateCreation_RejectsReactivatingRetiredWorkPackage()
    {
        var initialTimestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var retiredTimestamp = initialTimestamp.AddDays(7);
        var futureTimestamp = retiredTimestamp.AddDays(7);

        var initialSnapshot = new PortfolioSnapshot(initialTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10)
        ]);
        var retiredSnapshot = new PortfolioSnapshot(retiredTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10, WorkPackageLifecycleState.Retired)
        ]);
        var candidateSnapshot = new PortfolioSnapshot(futureTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.65d, 12)
        ]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ValidationService.ValidateCreation([initialSnapshot, retiredSnapshot], candidateSnapshot));
    }

    [TestMethod]
    public void ValidateCreation_UsesTimestampAscendingHistoryRatherThanInputOrder()
    {
        var earliestTimestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var latestTimestamp = earliestTimestamp.AddDays(7);
        var candidateTimestamp = latestTimestamp.AddDays(7);

        var earliestSnapshot = new PortfolioSnapshot(earliestTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.2d, 5)
        ]);
        var latestSnapshot = new PortfolioSnapshot(latestTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.4d, 10),
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-2", 0.6d, 15)
        ]);
        var candidateSnapshot = new PortfolioSnapshot(candidateTimestamp,
        [
            new PortfolioSnapshotItem(1, "PRJ-100", "WP-1", 0.65d, 12)
        ]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ValidationService.ValidateCreation([latestSnapshot, earliestSnapshot], candidateSnapshot));
    }
}
