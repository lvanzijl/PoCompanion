using PoTool.Core.Metrics.EffortDiagnostics;

namespace PoTool.Tests.Unit.Domain;

[TestClass]
public sealed class EffortDiagnosticsStatisticsTests
{
    [TestMethod]
    public void Mean_ReturnsArithmeticAverage()
    {
        var result = EffortDiagnosticsStatistics.Mean(new[] { 10d, 20d, 30d, 40d });

        Assert.AreEqual(25d, result, 0.001);
    }

    [TestMethod]
    public void Median_ReturnsExpectedValue_ForBothOddAndEvenSizedArrays()
    {
        var oddMedian = EffortDiagnosticsStatistics.Median(new[] { 7d, 1d, 4d });
        var evenMedian = EffortDiagnosticsStatistics.Median(new[] { 40d, 10d, 30d, 20d });

        Assert.AreEqual(4d, oddMedian, 0.001);
        Assert.AreEqual(25d, evenMedian, 0.001);
    }

    [TestMethod]
    public void Variance_ReturnsPopulationVariance()
    {
        var result = EffortDiagnosticsStatistics.Variance(new[] { 10d, 20d, 30d, 40d });

        Assert.AreEqual(125d, result, 0.001);
    }

    [TestMethod]
    public void CoefficientOfVariation_ReturnsStandardDeviationOverMean()
    {
        var result = EffortDiagnosticsStatistics.CoefficientOfVariation(new[] { 10d, 20d, 30d, 40d });

        Assert.AreEqual(0.447, result, 0.001);
    }

    [TestMethod]
    public void HHI_ReturnsCalculatedIndex()
    {
        var normalizedResult = EffortDiagnosticsStatistics.HHI(new[] { 0.5d, 0.25d, 0.25d });

        Assert.AreEqual(37.5d, normalizedResult, 0.001);
    }

    [TestMethod]
    public void HHI_CapsResultAt100()
    {
        var result = EffortDiagnosticsStatistics.HHI(new[] { 1.5d });

        Assert.AreEqual(100d, result, 0.001);
    }

    [TestMethod]
    public void HHI_WithNegativeShare_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            EffortDiagnosticsStatistics.HHI(new[] { 0.5d, -0.1d, 0.6d }));
    }
}
