using PoTool.Core.Metrics.EffortDiagnostics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class EffortDiagnosticsAnalyzerTests
{
    [TestMethod]
    public void AnalyzeImbalance_ComputesCanonicalBucketsAndOverallScore()
    {
        var analyzer = new EffortDiagnosticsAnalyzer();

        var result = analyzer.AnalyzeImbalance(
            new Dictionary<string, int>
            {
                ["Area A"] = 30,
                ["Area B"] = 10,
                ["Area C"] = 10
            },
            new Dictionary<string, int>
            {
                ["Sprint 1"] = 25,
                ["Sprint 2"] = 25
            },
            0.3);

        Assert.AreEqual(ImbalanceRiskLevel.High, result.OverallRiskLevel);
        Assert.AreEqual(60.8d, result.ImbalanceScore, 0.001);
        Assert.HasCount(3, result.AreaBuckets);
        Assert.HasCount(2, result.IterationBuckets);

        var dominantArea = result.AreaBuckets[0];
        Assert.AreEqual("Area A", dominantArea.BucketKey);
        Assert.AreEqual(30d, dominantArea.EffortAmount, 0.001);
        Assert.AreEqual(16.667d, dominantArea.MeanEffort, 0.001);
        Assert.AreEqual(0.8d, dominantArea.DeviationFromMean, 0.001);
        Assert.AreEqual(ImbalanceRiskLevel.Critical, dominantArea.RiskLevel);
    }

    [TestMethod]
    public void AnalyzeConcentration_UsesFullBucketDistributionForIndex()
    {
        var analyzer = new EffortDiagnosticsAnalyzer();

        var result = analyzer.AnalyzeConcentration(
            new Dictionary<string, int>
            {
                ["Area A"] = 50,
                ["Area B"] = 25,
                ["Area C"] = 25
            },
            new Dictionary<string, int>());

        Assert.AreEqual(ConcentrationRiskLevel.Medium, result.OverallRiskLevel);
        Assert.AreEqual(37.5d, result.ConcentrationIndex, 0.001);
        Assert.HasCount(3, result.AreaBuckets);
        Assert.HasCount(0, result.IterationBuckets);

        var dominantArea = result.AreaBuckets[0];
        Assert.AreEqual("Area A", dominantArea.BucketKey);
        Assert.AreEqual(50d, dominantArea.EffortAmount, 0.001);
        Assert.AreEqual(0.5d, dominantArea.EffortShare, 0.001);
        Assert.AreEqual(ConcentrationRiskLevel.Medium, dominantArea.RiskLevel);
    }

    [TestMethod]
    public void AnalyzeConcentration_WithMismatchedTotals_ThrowsArgumentException()
    {
        var analyzer = new EffortDiagnosticsAnalyzer();

        Assert.ThrowsExactly<ArgumentException>(() => analyzer.AnalyzeConcentration(
            new Dictionary<string, int>
            {
                ["Area A"] = 50
            },
            new Dictionary<string, int>
            {
                ["Sprint 1"] = 40
            }));
    }
}
