using PoTool.Core.Domain.EffortDiagnostics;

namespace PoTool.Tests.Unit.Domain;

[TestClass]
public sealed class EffortDiagnosticsStatisticsTests
{
    private static readonly EffortDiagnosticsStatistics Statistics = new CanonicalEffortDiagnosticsStatistics();

    [TestMethod]
    public void Mean_ReturnsArithmeticAverage()
    {
        var result = Statistics.Mean(new[] { 10d, 20d, 30d, 40d });

        Assert.AreEqual(25d, result, 0.001);
    }

    [TestMethod]
    public void Median_ReturnsExpectedValue_ForBothOddAndEvenSizedArrays()
    {
        var oddMedian = Statistics.Median(new[] { 7d, 1d, 4d });
        var evenMedian = Statistics.Median(new[] { 40d, 10d, 30d, 20d });

        Assert.AreEqual(4d, oddMedian, 0.001);
        Assert.AreEqual(25d, evenMedian, 0.001);
    }

    [TestMethod]
    public void Variance_ReturnsPopulationVariance()
    {
        var result = Statistics.Variance(new[] { 10d, 20d, 30d, 40d });

        Assert.AreEqual(125d, result, 0.001);
    }

    [TestMethod]
    public void CoefficientOfVariation_ReturnsStandardDeviationOverMean()
    {
        var values = new[] { 10d, 20d, 30d, 40d };
        var variance = Statistics.Variance(values);
        var mean = Statistics.Mean(values);
        var result = Statistics.CoefficientOfVariation(variance, mean);

        Assert.AreEqual(0.447, result, 0.001);
    }

    [TestMethod]
    public void HHI_ReturnsCalculatedRawIndex()
    {
        var result = Statistics.HHI(new[] { 0.5d, 0.25d, 0.25d });

        Assert.AreEqual(0.375d, result, 0.001);
    }

    [TestMethod]
    public void DeviationAndShare_ReturnExpectedRatios()
    {
        var deviation = Statistics.DeviationFromMean(40d, 25d);
        var share = Statistics.ShareOfTotal(25d, 100d);

        Assert.AreEqual(0.6d, deviation, 0.001);
        Assert.AreEqual(0.25d, share, 0.001);
    }

    [TestMethod]
    public void HHI_WithOutOfRangeShare_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            Statistics.HHI(new[] { 0.5d, -0.1d, 0.6d }));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            Statistics.HHI(new[] { 1.5d }));
    }
}
