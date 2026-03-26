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
            new PortfolioSnapshotItem(timestamp, 1, "PRJ-100", null, 40, 20),
            new PortfolioSnapshotItem(timestamp, 1, "PRJ-200", null, 60, 10)
        ]);

        Assert.AreEqual(timestamp, snapshot.Timestamp);
        Assert.HasCount(2, snapshot.Items);
    }

    [TestMethod]
    public void PortfolioSnapshot_AllowsFullWorkPackageBreakdownRows()
    {
        var timestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var snapshot = new PortfolioSnapshot(timestamp,
        [
            new PortfolioSnapshotItem(timestamp, 1, "PRJ-100", "WP-1", 40, 10),
            new PortfolioSnapshotItem(timestamp, 1, "PRJ-100", "WP-2", 60, 15)
        ]);

        Assert.AreEqual(timestamp, snapshot.Timestamp);
        Assert.HasCount(2, snapshot.Items);
    }

    [TestMethod]
    public void PortfolioSnapshot_RejectsMixedProjectAndWorkPackageRowsWithinSameProject()
    {
        var timestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.ThrowsExactly<ArgumentException>(() => new PortfolioSnapshot(timestamp,
        [
            new PortfolioSnapshotItem(timestamp, 1, "PRJ-100", null, 40, 10),
            new PortfolioSnapshotItem(timestamp, 1, "PRJ-100", "WP-1", 60, 15)
        ]));
    }

    [TestMethod]
    public void ValidateCreation_RejectsIncompleteWorkPackageBreakdownForProject()
    {
        var previousTimestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var currentTimestamp = previousTimestamp.AddDays(7);

        var previousSnapshot = new PortfolioSnapshot(previousTimestamp,
        [
            new PortfolioSnapshotItem(previousTimestamp, 1, "PRJ-100", "WP-1", 40, 10),
            new PortfolioSnapshotItem(previousTimestamp, 1, "PRJ-100", "WP-2", 60, 15)
        ]);

        var candidateSnapshot = new PortfolioSnapshot(currentTimestamp,
        [
            new PortfolioSnapshotItem(currentTimestamp, 1, "PRJ-100", "WP-1", 65, 12)
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
            new PortfolioSnapshotItem(previousTimestamp, 1, "PRJ-100", "WP-1", 40, 10)
        ]);

        var candidateSnapshot = new PortfolioSnapshot(currentTimestamp,
        [
            new PortfolioSnapshotItem(currentTimestamp, 1, "PRJ-100", null, 65, 12)
        ]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ValidationService.ValidateCreation([previousSnapshot], candidateSnapshot));
    }

    [TestMethod]
    public void ValidateCreation_UsesTimestampAscendingHistoryRatherThanInputOrder()
    {
        var earliestTimestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var latestTimestamp = earliestTimestamp.AddDays(7);
        var candidateTimestamp = latestTimestamp.AddDays(7);

        var earliestSnapshot = new PortfolioSnapshot(earliestTimestamp,
        [
            new PortfolioSnapshotItem(earliestTimestamp, 1, "PRJ-100", "WP-1", 20, 5)
        ]);
        var latestSnapshot = new PortfolioSnapshot(latestTimestamp,
        [
            new PortfolioSnapshotItem(latestTimestamp, 1, "PRJ-100", "WP-1", 40, 10),
            new PortfolioSnapshotItem(latestTimestamp, 1, "PRJ-100", "WP-2", 60, 15)
        ]);
        var candidateSnapshot = new PortfolioSnapshot(candidateTimestamp,
        [
            new PortfolioSnapshotItem(candidateTimestamp, 1, "PRJ-100", "WP-1", 65, 12)
        ]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ValidationService.ValidateCreation([latestSnapshot, earliestSnapshot], candidateSnapshot));
    }
}
