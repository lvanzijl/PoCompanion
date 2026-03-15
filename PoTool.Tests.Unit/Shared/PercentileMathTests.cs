using PoTool.Shared.Statistics;

namespace PoTool.Tests.Unit.Statistics;

[TestClass]
public sealed class PercentileMathTests
{
    [TestMethod]
    public void LinearInterpolation_ReturnsInterpolatedPercentile_ForSortedSample()
    {
        var result = PercentileMath.LinearInterpolation(new[] { 24d, 48d, 72d, 96d }, 75);

        Assert.AreEqual(78d, result, 0.001);
    }

    [TestMethod]
    public void LinearInterpolation_ReturnsZeroForEmptySample_AndOnlyValueForSingleSample()
    {
        var emptyResult = PercentileMath.LinearInterpolation(Array.Empty<double>(), 90);
        var singleResult = PercentileMath.LinearInterpolation(new[] { 42d }, 90);

        Assert.AreEqual(0d, emptyResult, 0.001);
        Assert.AreEqual(42d, singleResult, 0.001);
    }

    [TestMethod]
    public void LinearInterpolation_ThrowsWhenPercentileIsOutsideInclusiveRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PercentileMath.LinearInterpolation(new[] { 1d, 2d }, -0.1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PercentileMath.LinearInterpolation(new[] { 1d, 2d }, 100.1));
    }
}
