using PoTool.Integrations.Tfs.Clients;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemIdRangeSegmentBuilderTests
{
    [TestMethod]
    public void Build_WithDenseIds_ProducesFewLargeSegments()
    {
        var ids = Enumerable.Range(1, 100).ToArray();
        var segments = WorkItemIdRangeSegmentBuilder.Build(
            ids,
            maxGap: 200,
            maxSpan: 5000,
            minIdsPerSegment: 25,
            maxSegmentsPerWindow: 200);

        Assert.HasCount(1, segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(1, 100), segments[0]);
    }

    [TestMethod]
    public void Build_WithSparseIds_RespectsGapSpanAndMinIds()
    {
        var ids = new[] { 1, 2, 200, 201, 202, 2000, 2001, 2002, 2003 };
        var segments = WorkItemIdRangeSegmentBuilder.Build(
            ids,
            maxGap: 10,
            maxSpan: 1000,
            minIdsPerSegment: 3,
            maxSegmentsPerWindow: 200);

        Assert.HasCount(2, segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(1, 202), segments[0]);
        Assert.AreEqual(new WorkItemIdRangeSegment(2000, 2003), segments[1]);
    }

    [TestMethod]
    public void Build_IsDeterministic_ForSameInput()
    {
        var ids = new[] { 11, 2, 10, 1, 3, 500, 501, 900 };
        var first = WorkItemIdRangeSegmentBuilder.Build(
            ids,
            maxGap: 50,
            maxSpan: 5000,
            minIdsPerSegment: 4,
            maxSegmentsPerWindow: 200);
        var second = WorkItemIdRangeSegmentBuilder.Build(
            ids,
            maxGap: 50,
            maxSpan: 5000,
            minIdsPerSegment: 4,
            maxSegmentsPerWindow: 200);

        CollectionAssert.AreEqual(first.ToArray(), second.ToArray());
    }

    [TestMethod]
    public void Build_WhenSegmentCountExceedsSafetyCap_CondensesToSingleSegment()
    {
        var ids = Enumerable.Range(1, 1000)
            .Select(i => i * 100)
            .ToArray();
        var result = WorkItemIdRangeSegmentBuilder.BuildWithMetadata(
            ids,
            maxGap: 0,
            maxSpan: 1,
            minIdsPerSegment: 1,
            maxSegmentsPerWindow: 3);

        Assert.IsTrue(result.WasCondensedToSingleSegment);
        Assert.HasCount(1, result.Segments);
        Assert.AreEqual(new WorkItemIdRangeSegment(100, 100000), result.Segments[0]);
    }
}
