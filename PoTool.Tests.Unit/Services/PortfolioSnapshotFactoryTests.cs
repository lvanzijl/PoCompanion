using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioSnapshotFactoryTests
{
    private static readonly IPortfolioSnapshotFactory Factory = new PortfolioSnapshotFactory();

    [TestMethod]
    public void Create_FirstSnapshotMarksAllRowsActive()
    {
        var timestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var snapshot = Factory.Create(new PortfolioSnapshotFactoryRequest(
            timestamp,
            [
                new PortfolioSnapshotFactoryEpicInput(1, "PRJ-100", "WP-1", 0.4d, 10d),
                new PortfolioSnapshotFactoryEpicInput(1, "PRJ-100", "WP-2", 0.6d, 15d)
            ]));

        Assert.AreEqual(timestamp, snapshot.Timestamp);
        CollectionAssert.AreEqual(
            new[]
            {
                "PRJ-100|WP-1|Active",
                "PRJ-100|WP-2|Active"
            },
            snapshot.Items.Select(FormatLifecycleRow).ToArray());
    }

    [TestMethod]
    public void Create_KeepsExistingRowsActiveWhenStillPresent()
    {
        var previousSnapshot = CreateSnapshot(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", "WP-1", 0.4d, 10d, WorkPackageLifecycleState.Active));

        var snapshot = Factory.Create(new PortfolioSnapshotFactoryRequest(
            previousSnapshot.Timestamp.AddDays(7),
            [
                new PortfolioSnapshotFactoryEpicInput(1, "PRJ-100", "WP-1", 0.5d, 11d)
            ],
            previousSnapshot));

        Assert.AreEqual(WorkPackageLifecycleState.Active, snapshot.Items.Single().LifecycleState);
        Assert.AreEqual(0.5d, snapshot.Items.Single().Progress, 0.001d);
        Assert.AreEqual(11d, snapshot.Items.Single().TotalWeight, 0.001d);
    }

    [TestMethod]
    public void Create_MarksMissingPriorActiveWorkPackageAsRetired()
    {
        var previousSnapshot = CreateSnapshot(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", "WP-1", 0.4d, 10d, WorkPackageLifecycleState.Active),
            (1, "PRJ-100", "WP-2", 0.6d, 15d, WorkPackageLifecycleState.Active));

        var snapshot = Factory.Create(new PortfolioSnapshotFactoryRequest(
            previousSnapshot.Timestamp.AddDays(7),
            [
                new PortfolioSnapshotFactoryEpicInput(1, "PRJ-100", "WP-1", 0.5d, 11d)
            ],
            previousSnapshot));

        CollectionAssert.AreEqual(
            new[]
            {
                "PRJ-100|WP-1|Active",
                "PRJ-100|WP-2|Retired"
            },
            snapshot.Items.Select(FormatLifecycleRow).ToArray());
    }

    [TestMethod]
    public void Create_DoesNotRepeatRetiredWorkPackageInLaterSnapshots()
    {
        var previousSnapshot = CreateSnapshot(
            new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", "WP-1", 0.4d, 10d, WorkPackageLifecycleState.Retired));

        var snapshot = Factory.Create(new PortfolioSnapshotFactoryRequest(
            previousSnapshot.Timestamp.AddDays(7),
            [
                new PortfolioSnapshotFactoryEpicInput(2, "PRJ-200", null, 0.5d, 11d)
            ],
            previousSnapshot));

        CollectionAssert.AreEqual(
            new[]
            {
                "PRJ-200|<project>|Active"
            },
            snapshot.Items.Select(FormatLifecycleRow).ToArray());
    }

    [TestMethod]
    public void Create_RejectsRetiredWorkPackageReactivation()
    {
        var previousSnapshot = CreateSnapshot(
            new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", "WP-1", 0.4d, 10d, WorkPackageLifecycleState.Retired));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Factory.Create(new PortfolioSnapshotFactoryRequest(
                previousSnapshot.Timestamp.AddDays(7),
                [
                    new PortfolioSnapshotFactoryEpicInput(1, "PRJ-100", "WP-1", 0.5d, 11d)
                ],
                previousSnapshot)));
    }

    [TestMethod]
    public void Create_IsDeterministicRegardlessOfInputOrdering()
    {
        var timestamp = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var previousSnapshot = CreateSnapshot(
            new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero),
            (2, "PRJ-200", "WP-9", 0.3d, 6d, WorkPackageLifecycleState.Active),
            (1, "PRJ-100", "WP-1", 0.4d, 10d, WorkPackageLifecycleState.Active));

        var ordered = Factory.Create(new PortfolioSnapshotFactoryRequest(
            timestamp,
            [
                new PortfolioSnapshotFactoryEpicInput(1, "PRJ-100", "WP-1", 0.5d, 11d),
                new PortfolioSnapshotFactoryEpicInput(3, "PRJ-300", null, 0.7d, 5d)
            ],
            previousSnapshot));
        var shuffled = Factory.Create(new PortfolioSnapshotFactoryRequest(
            timestamp,
            [
                new PortfolioSnapshotFactoryEpicInput(3, "PRJ-300", null, 0.7d, 5d),
                new PortfolioSnapshotFactoryEpicInput(1, "PRJ-100", "WP-1", 0.5d, 11d)
            ],
            CreateSnapshot(
                previousSnapshot.Timestamp,
                (1, "PRJ-100", "WP-1", 0.4d, 10d, WorkPackageLifecycleState.Active),
                (2, "PRJ-200", "WP-9", 0.3d, 6d, WorkPackageLifecycleState.Active))));

        CollectionAssert.AreEqual(
            ordered.Items.Select(FormatSnapshotRow).ToArray(),
            shuffled.Items.Select(FormatSnapshotRow).ToArray());
    }

    [TestMethod]
    public void Create_AllowsEmptySnapshotWhenNoInputsExist()
    {
        var timestamp = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);

        var snapshot = Factory.Create(new PortfolioSnapshotFactoryRequest(
            timestamp,
            Array.Empty<PortfolioSnapshotFactoryEpicInput>()));

        Assert.AreEqual(timestamp, snapshot.Timestamp);
        Assert.IsEmpty(snapshot.Items);
    }

    private static PortfolioSnapshot CreateSnapshot(
        DateTimeOffset timestamp,
        params (int ProductId, string ProjectNumber, string? WorkPackage, double Progress, double TotalWeight, WorkPackageLifecycleState LifecycleState)[] rows)
    {
        return new PortfolioSnapshot(
            timestamp,
            rows.Select(row => new PortfolioSnapshotItem(
                    row.ProductId,
                    row.ProjectNumber,
                    row.WorkPackage,
                    row.Progress,
                    row.TotalWeight,
                    row.LifecycleState))
                .ToArray());
    }

    private static string FormatLifecycleRow(PortfolioSnapshotItem item)
        => $"{item.ProjectNumber}|{item.WorkPackage ?? "<project>"}|{item.LifecycleState}";

    private static string FormatSnapshotRow(PortfolioSnapshotItem item)
        => string.Join("|",
            item.ProductId,
            item.ProjectNumber,
            item.WorkPackage ?? "<project>",
            item.Progress.ToString("0.###"),
            item.TotalWeight.ToString("0.###"),
            item.LifecycleState);
}
