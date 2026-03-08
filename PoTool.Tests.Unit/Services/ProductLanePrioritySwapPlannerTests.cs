using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ProductLanePrioritySwapPlannerTests
{
    [TestMethod]
    public void TryCreateExactNeighborSwap_DistinctRealPriorities_ReturnsExactSwapPlan()
    {
        var created = ProductLanePrioritySwapPlanner.TryCreateExactNeighborSwap(
            101,
            999245551d,
            102,
            999247811d,
            out var plan,
            out var failureReason);

        Assert.IsTrue(created);
        Assert.IsNotNull(plan);
        Assert.AreEqual(101, plan.SelectedObjectiveTfsId);
        Assert.AreEqual(999245551d, plan.SelectedOriginalPriority);
        Assert.AreEqual(102, plan.NeighborObjectiveTfsId);
        Assert.AreEqual(999247811d, plan.NeighborOriginalPriority);
        Assert.AreEqual(999247811d, plan.SelectedWrittenPriority);
        Assert.AreEqual(999245551d, plan.NeighborWrittenPriority);
        Assert.IsNull(failureReason);
    }

    [TestMethod]
    public void TryCreateExactNeighborSwap_MissingPriority_RequiresFallbackRecovery()
    {
        var created = ProductLanePrioritySwapPlanner.TryCreateExactNeighborSwap(
            101,
            null,
            102,
            999247811d,
            out var plan,
            out var failureReason);

        Assert.IsFalse(created);
        Assert.IsNull(plan);
        Assert.AreEqual(ProductLanePrioritySwapFailureReason.MissingPriority, failureReason);
    }

    [TestMethod]
    public void TryCreateExactNeighborSwap_DuplicatePriority_RequiresFallbackRecovery()
    {
        var created = ProductLanePrioritySwapPlanner.TryCreateExactNeighborSwap(
            101,
            999245551d,
            102,
            999245551d,
            out var plan,
            out var failureReason);

        Assert.IsFalse(created);
        Assert.IsNull(plan);
        Assert.AreEqual(ProductLanePrioritySwapFailureReason.DuplicatePriority, failureReason);
    }

    [TestMethod]
    public void TryCreateExactNeighborSwap_InvalidPriority_RequiresFallbackRecovery()
    {
        var created = ProductLanePrioritySwapPlanner.TryCreateExactNeighborSwap(
            101,
            double.NaN,
            102,
            999247811d,
            out var plan,
            out var failureReason);

        Assert.IsFalse(created);
        Assert.IsNull(plan);
        Assert.AreEqual(ProductLanePrioritySwapFailureReason.InvalidPriority, failureReason);
    }
}
