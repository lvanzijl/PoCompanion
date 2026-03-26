using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioSnapshotComparisonServiceTests
{
    private static readonly IPortfolioSnapshotComparisonService Service = new PortfolioSnapshotComparisonService();

    [TestMethod]
    public void Compare_ReturnsProgressDeltaForMatchingBusinessKey()
    {
        var previous = CreateSnapshot(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", "WP-1", 40d, 10d));
        var current = CreateSnapshot(
            previous.Timestamp.AddDays(7),
            (1, "PRJ-100", "WP-1", 65d, 10d));

        var result = Service.Compare(new PortfolioSnapshotComparisonRequest(previous, current));

        Assert.HasCount(1, result.Items);
        Assert.AreEqual(25d, result.Items[0].ProgressDelta!.Value, 0.001d);
    }

    [TestMethod]
    public void Compare_ReturnsWeightDeltaForMatchingBusinessKey()
    {
        var previous = CreateSnapshot(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", "WP-1", 40d, 10d));
        var current = CreateSnapshot(
            previous.Timestamp.AddDays(7),
            (1, "PRJ-100", "WP-1", 40d, 15d));

        var result = Service.Compare(new PortfolioSnapshotComparisonRequest(previous, current));

        Assert.AreEqual(5d, result.Items[0].WeightDelta!.Value, 0.001d);
    }

    [TestMethod]
    public void Compare_UsesNullPreviousValuesForNewCurrentRows()
    {
        var previous = CreateSnapshot(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", "WP-1", 40d, 10d));
        var current = CreateSnapshot(
            previous.Timestamp.AddDays(7),
            (1, "PRJ-100", "WP-1", 40d, 10d),
            (1, "PRJ-100", "WP-2", 55d, 8d));

        var result = Service.Compare(new PortfolioSnapshotComparisonRequest(previous, current));
        var addedRow = result.Items.Single(item => item.WorkPackage == "WP-2");

        Assert.IsNull(addedRow.PreviousProgress);
        Assert.AreEqual(55d, addedRow.CurrentProgress!.Value, 0.001d);
        Assert.IsNull(addedRow.ProgressDelta);
        Assert.IsNull(addedRow.PreviousWeight);
        Assert.AreEqual(8d, addedRow.CurrentWeight!.Value, 0.001d);
        Assert.IsNull(addedRow.WeightDelta);
    }

    [TestMethod]
    public void Compare_UsesNullCurrentValuesForRemovedRows()
    {
        var previous = CreateSnapshot(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", "WP-1", 40d, 10d),
            (1, "PRJ-100", "WP-2", 55d, 8d));
        var current = CreateSnapshot(
            previous.Timestamp.AddDays(7),
            (1, "PRJ-100", "WP-1", 40d, 10d));

        var result = Service.Compare(new PortfolioSnapshotComparisonRequest(previous, current));
        var removedRow = result.Items.Single(item => item.WorkPackage == "WP-2");

        Assert.AreEqual(55d, removedRow.PreviousProgress!.Value, 0.001d);
        Assert.IsNull(removedRow.CurrentProgress);
        Assert.IsNull(removedRow.ProgressDelta);
        Assert.AreEqual(8d, removedRow.PreviousWeight!.Value, 0.001d);
        Assert.IsNull(removedRow.CurrentWeight);
        Assert.IsNull(removedRow.WeightDelta);
    }

    [TestMethod]
    public void Compare_IsOrderingIndependentAcrossInputRows()
    {
        var previousTimestamp = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var currentTimestamp = previousTimestamp.AddDays(7);

        var orderedPrevious = CreateSnapshot(
            previousTimestamp,
            (2, "PRJ-200", null, 30d, 12d),
            (1, "PRJ-100", "WP-1", 40d, 10d),
            (1, "PRJ-100", "WP-2", 50d, 15d));
        var orderedCurrent = CreateSnapshot(
            currentTimestamp,
            (2, "PRJ-200", null, 45d, 14d),
            (1, "PRJ-100", "WP-1", 55d, 11d),
            (1, "PRJ-100", "WP-3", 65d, 9d));

        var shuffledPrevious = CreateSnapshot(
            previousTimestamp,
            (1, "PRJ-100", "WP-2", 50d, 15d),
            (2, "PRJ-200", null, 30d, 12d),
            (1, "PRJ-100", "WP-1", 40d, 10d));
        var shuffledCurrent = CreateSnapshot(
            currentTimestamp,
            (1, "PRJ-100", "WP-3", 65d, 9d),
            (1, "PRJ-100", "WP-1", 55d, 11d),
            (2, "PRJ-200", null, 45d, 14d));

        var orderedResult = Service.Compare(new PortfolioSnapshotComparisonRequest(orderedPrevious, orderedCurrent));
        var shuffledResult = Service.Compare(new PortfolioSnapshotComparisonRequest(shuffledPrevious, shuffledCurrent));

        CollectionAssert.AreEqual(
            orderedResult.Items.Select(FormatComparisonRow).ToArray(),
            shuffledResult.Items.Select(FormatComparisonRow).ToArray());
    }

    [TestMethod]
    public void Compare_ReturnsSameOutputForSameInput()
    {
        var previous = CreateSnapshot(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", null, 40d, 10d),
            (2, "PRJ-200", "WP-1", 60d, 5d));
        var current = CreateSnapshot(
            previous.Timestamp.AddDays(7),
            (1, "PRJ-100", null, 45d, 10d),
            (2, "PRJ-200", "WP-1", 70d, 8d));

        var first = Service.Compare(new PortfolioSnapshotComparisonRequest(previous, current));
        var second = Service.Compare(new PortfolioSnapshotComparisonRequest(previous, current));

        CollectionAssert.AreEqual(
            first.Items.Select(FormatComparisonRow).ToArray(),
            second.Items.Select(FormatComparisonRow).ToArray());
    }

    [TestMethod]
    public void Compare_RejectsPreviousSnapshotNewerThanCurrentSnapshot()
    {
        var previous = CreateSnapshot(
            new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", null, 40d, 10d));
        var current = CreateSnapshot(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            (1, "PRJ-100", null, 45d, 10d));

        Assert.ThrowsExactly<ArgumentException>(() =>
            Service.Compare(new PortfolioSnapshotComparisonRequest(previous, current)));
    }

    private static PortfolioSnapshot CreateSnapshot(
        DateTimeOffset timestamp,
        params (int ProductId, string ProjectNumber, string? WorkPackage, double Progress, double TotalWeight)[] rows)
    {
        return new PortfolioSnapshot(
            timestamp,
            rows.Select(row => new PortfolioSnapshotItem(
                    timestamp,
                    row.ProductId,
                    row.ProjectNumber,
                    row.WorkPackage,
                    row.Progress,
                    row.TotalWeight))
                .ToArray());
    }

    private static string FormatComparisonRow(PortfolioSnapshotComparisonItem item)
    {
        return string.Join("|",
            item.ProductId,
            item.ProjectNumber,
            item.WorkPackage ?? "<project>",
            item.PreviousProgress?.ToString("0.###") ?? "null",
            item.CurrentProgress?.ToString("0.###") ?? "null",
            item.ProgressDelta?.ToString("0.###") ?? "null",
            item.PreviousWeight?.ToString("0.###") ?? "null",
            item.CurrentWeight?.ToString("0.###") ?? "null",
            item.WeightDelta?.ToString("0.###") ?? "null");
    }
}
