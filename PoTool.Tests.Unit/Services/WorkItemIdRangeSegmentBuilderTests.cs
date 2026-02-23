using PoTool.Integrations.Tfs.Clients;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemIdRangeSegmentBuilderTests
{
    [TestMethod]
    public void Build_WithIdCountBelowMinIds_ReturnsSingleSpanningSegment()
    {
        var result = WorkItemIdRangeSegmentBuilder.BuildWithMetadata(
            [11, 2, 50],
            new WorkItemIdSegmentationOptions(0, 5000, 10, 200, "SingleRange"));

        Assert.HasCount(1, result.Segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(2, 50), result.Segments[0]);
        Assert.IsFalse(result.FallbackApplied);
        Assert.IsNull(result.FallbackMode);
    }

    [TestMethod]
    public void Build_WithMaxGap_GroupsOrSplitsByGapThreshold()
    {
        var segments = WorkItemIdRangeSegmentBuilder.Build(
            [1, 2, 4, 50],
            maxGap: 1,
            maxSpan: 5000,
            minIdsPerSegment: 1,
            maxSegmentsPerWindow: 200);

        Assert.HasCount(2, segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(1, 4), segments[0]);
        Assert.AreEqual(new WorkItemIdRangeSegment(50, 50), segments[1]);
    }

    [TestMethod]
    public void Build_WithMaxSpan_SplitsWhenSpanWouldExceedLimit()
    {
        var segments = WorkItemIdRangeSegmentBuilder.Build(
            [1, 2, 3, 9],
            maxGap: 100,
            maxSpan: 5,
            minIdsPerSegment: 1,
            maxSegmentsPerWindow: 200);

        Assert.HasCount(2, segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(1, 3), segments[0]);
        Assert.AreEqual(new WorkItemIdRangeSegment(9, 9), segments[1]);
    }

    [TestMethod]
    public void Build_WhenCapExceededAndSingleRangeMode_ReturnsSingleRangeWithFallbackMetadata()
    {
        var ids = Enumerable.Range(1, 1000)
            .Select(i => i * 100)
            .ToArray();
        var result = WorkItemIdRangeSegmentBuilder.BuildWithMetadata(
            ids,
            new WorkItemIdSegmentationOptions(0, 1, 1, 3, "SingleRange"));

        Assert.IsTrue(result.FallbackApplied);
        Assert.AreEqual("SingleRange", result.FallbackMode);
        Assert.HasCount(1, result.Segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(100, 100000), result.Segments[0]);
    }

    [TestMethod]
    public void Build_WhenCapExceededAndMergeAdjacentMode_ReturnsCapSegmentsWithCoverage()
    {
        var ids = Enumerable.Range(1, 40)
            .Select(i => i * 100)
            .ToArray();
        var result = WorkItemIdRangeSegmentBuilder.BuildWithMetadata(
            ids,
            new WorkItemIdSegmentationOptions(0, 1, 1, 3, "MergeAdjacent"));

        Assert.IsTrue(result.FallbackApplied);
        Assert.AreEqual("MergeAdjacent", result.FallbackMode);
        Assert.HasCount(3, result.Segments);
        foreach (var id in ids)
        {
            Assert.IsTrue(result.Segments.Any(segment => id >= segment.Start && id <= segment.End), $"Missing coverage for ID {id}");
        }
    }

    [TestMethod]
    public void Build_IsDeterministic_ForSameInput()
    {
        var ids = new[] { 11, 2, 10, 1, 3, 500, 501, 900 };
        var options = new WorkItemIdSegmentationOptions(50, 5000, 4, 200, "MergeAdjacent");
        var first = WorkItemIdRangeSegmentBuilder.Build(ids, options);
        var second = WorkItemIdRangeSegmentBuilder.Build(ids, options);

        CollectionAssert.AreEqual(first.ToArray(), second.ToArray());
    }
}
