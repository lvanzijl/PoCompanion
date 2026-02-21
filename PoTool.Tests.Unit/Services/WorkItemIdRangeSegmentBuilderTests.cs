using PoTool.Integrations.Tfs.Clients;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemIdRangeSegmentBuilderTests
{
    [TestMethod]
    public void Build_WithMultipleContiguousRuns_ReturnsSeparatedSegments()
    {
        var segments = WorkItemIdRangeSegmentBuilder.Build([1, 2, 3, 10, 11]);

        Assert.HasCount(2, segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(1, 3), segments[0]);
        Assert.AreEqual(new WorkItemIdRangeSegment(10, 11), segments[1]);
    }

    [TestMethod]
    public void Build_WithSingleId_ReturnsSinglePointSegment()
    {
        var segments = WorkItemIdRangeSegmentBuilder.Build([5]);

        Assert.HasCount(1, segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(5, 5), segments[0]);
    }

    [TestMethod]
    public void Build_WithUnsortedInput_SortsBeforeSegmenting()
    {
        var segments = WorkItemIdRangeSegmentBuilder.Build([11, 2, 10, 1, 3]);

        Assert.HasCount(2, segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(1, 3), segments[0]);
        Assert.AreEqual(new WorkItemIdRangeSegment(10, 11), segments[1]);
    }

    [TestMethod]
    public void Build_WithDuplicateIds_DeduplicatesBeforeSegmenting()
    {
        var segments = WorkItemIdRangeSegmentBuilder.Build([1, 1, 2, 2, 3, 3]);

        Assert.HasCount(1, segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(1, 3), segments[0]);
    }

    [TestMethod]
    public void Build_WithMaxRangeSize_SplitsLargeContiguousRanges()
    {
        var ids = Enumerable.Range(1, 1200).ToArray();
        var segments = WorkItemIdRangeSegmentBuilder.Build(ids, maxRangeSize: 500);

        Assert.HasCount(3, segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(1, 500), segments[0]);
        Assert.AreEqual(new WorkItemIdRangeSegment(501, 1000), segments[1]);
        Assert.AreEqual(new WorkItemIdRangeSegment(1001, 1200), segments[2]);
    }

    [TestMethod]
    public void Build_WithGapTolerance_MergesRangesWithinTolerance()
    {
        var segments = WorkItemIdRangeSegmentBuilder.Build([1, 2, 4, 10], gapTolerance: 1);

        Assert.HasCount(2, segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(1, 4), segments[0]);
        Assert.AreEqual(new WorkItemIdRangeSegment(10, 10), segments[1]);
    }
}
