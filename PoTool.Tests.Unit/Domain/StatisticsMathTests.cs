using PoTool.Core.Domain.Statistics;

namespace PoTool.Tests.Unit.Domain;

[TestClass]
public sealed class StatisticsMathTests
{
    [TestMethod]
    public void Mean_ReturnsArithmeticAverage_ForUnsortedValues()
    {
        var result = StatisticsMath.Mean(new[] { 30d, 10d, 40d, 20d });

        Assert.AreEqual(25d, result, 0.001);
    }

    [TestMethod]
    public void Variance_ReturnsPopulationVariance_AndZeroForEmptySample()
    {
        var variance = StatisticsMath.Variance(new[] { 10d, 20d, 30d, 40d });
        var emptyVariance = StatisticsMath.Variance(Array.Empty<double>());

        Assert.AreEqual(125d, variance, 0.001);
        Assert.AreEqual(0d, emptyVariance, 0.001);
    }

    [TestMethod]
    public void StandardDeviation_ReturnsPopulationSpread_AndZeroForEmptySample()
    {
        var standardDeviation = StatisticsMath.StandardDeviation(new[] { 10d, 20d, 30d, 40d });
        var emptyStandardDeviation = StatisticsMath.StandardDeviation(Array.Empty<double>());

        Assert.AreEqual(Math.Sqrt(125d), standardDeviation, 0.001);
        Assert.AreEqual(0d, emptyStandardDeviation, 0.001);
    }

    [TestMethod]
    public void Median_ReturnsMiddleValue_ForOddSample()
    {
        var result = StatisticsMath.Median(new[] { 7d, 1d, 4d });

        Assert.AreEqual(4d, result, 0.001);
    }

    [TestMethod]
    public void Median_ReturnsAverageOfMiddlePair_ForEvenSample_AndZeroForEmptySample()
    {
        var evenResult = StatisticsMath.Median(new[] { 40d, 10d, 30d, 20d });
        var emptyResult = StatisticsMath.Median(Array.Empty<double>());

        Assert.AreEqual(25d, evenResult, 0.001);
        Assert.AreEqual(0d, emptyResult, 0.001);
    }
}
