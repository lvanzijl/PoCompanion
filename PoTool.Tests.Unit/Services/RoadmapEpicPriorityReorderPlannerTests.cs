using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class RoadmapEpicPriorityReorderPlannerTests
{
    [TestMethod]
    public void TryCreatePriorityPreservingPlan_NeighborMove_SwapsExistingPriorities()
    {
        var created = RoadmapEpicPriorityReorderPlanner.TryCreatePriorityPreservingPlan(
            [
                new RoadmapEpicPriorityItem(101, 1010d),
                new RoadmapEpicPriorityItem(102, 2020d),
                new RoadmapEpicPriorityItem(103, 3030d)
            ],
            currentIndex: 1,
            targetIndex: 2,
            out var writes,
            out var failureReason);

        Assert.IsTrue(created);
        Assert.IsNull(failureReason);
        Assert.IsNotNull(writes);
        Assert.HasCount(2, writes);
        Assert.AreEqual(103, writes[0].TfsId);
        Assert.AreEqual(3030d, writes[0].OriginalPriority);
        Assert.AreEqual(2020d, writes[0].NewPriority);
        Assert.AreEqual(102, writes[1].TfsId);
        Assert.AreEqual(2020d, writes[1].OriginalPriority);
        Assert.AreEqual(3030d, writes[1].NewPriority);
    }

    [TestMethod]
    public void TryCreatePriorityPreservingPlan_LongMove_ReusesExistingPrioritiesWithoutSynthetics()
    {
        var created = RoadmapEpicPriorityReorderPlanner.TryCreatePriorityPreservingPlan(
            [
                new RoadmapEpicPriorityItem(101, 10d),
                new RoadmapEpicPriorityItem(102, 20d),
                new RoadmapEpicPriorityItem(103, 30d),
                new RoadmapEpicPriorityItem(104, 40d)
            ],
            currentIndex: 0,
            targetIndex: 3,
            out var writes,
            out var failureReason);

        Assert.IsTrue(created);
        Assert.IsNull(failureReason);
        Assert.IsNotNull(writes);
        Assert.HasCount(4, writes);
        CollectionAssert.AreEqual(
            new[] { 102, 103, 104, 101 },
            writes.Select(write => write.TfsId).ToArray());
        CollectionAssert.AreEqual(
            new[] { 10d, 20d, 30d, 40d },
            writes.Select(write => write.NewPriority).ToArray());
    }

    [TestMethod]
    public void TryCreatePriorityPreservingPlan_MissingPriority_ReturnsFailureReason()
    {
        var created = RoadmapEpicPriorityReorderPlanner.TryCreatePriorityPreservingPlan(
            [
                new RoadmapEpicPriorityItem(101, 10d),
                new RoadmapEpicPriorityItem(102, null)
            ],
            currentIndex: 0,
            targetIndex: 1,
            out var writes,
            out var failureReason);

        Assert.IsFalse(created);
        Assert.IsNull(writes);
        Assert.AreEqual(RoadmapEpicPriorityReorderFailureReason.MissingPriority, failureReason);
    }

    [TestMethod]
    public void TryCreatePriorityPreservingPlan_DuplicatePriority_ReturnsFailureReason()
    {
        var created = RoadmapEpicPriorityReorderPlanner.TryCreatePriorityPreservingPlan(
            [
                new RoadmapEpicPriorityItem(101, 10d),
                new RoadmapEpicPriorityItem(102, 10d)
            ],
            currentIndex: 0,
            targetIndex: 1,
            out var writes,
            out var failureReason);

        Assert.IsFalse(created);
        Assert.IsNull(writes);
        Assert.AreEqual(RoadmapEpicPriorityReorderFailureReason.DuplicatePriority, failureReason);
    }
}
