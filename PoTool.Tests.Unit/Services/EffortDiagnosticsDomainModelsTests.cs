using PoTool.Core.Domain.EffortDiagnostics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class EffortDiagnosticsDomainModelsTests
{
    [TestMethod]
    public void Statistics_PrimitivesMatchStableSubsetMath()
    {
        EffortDiagnosticsStatistics statistics = new CanonicalEffortDiagnosticsStatistics();
        var values = new[] { 10d, 20d, 30d, 40d };

        var mean = statistics.Mean(values);
        var deviation = statistics.DeviationFromMean(40, mean);
        var share = statistics.ShareOfTotal(25, 100);
        var median = statistics.Median(values);
        var variance = statistics.Variance(values);
        var coefficientOfVariation = statistics.CoefficientOfVariation(variance, mean);
        var hhi = statistics.HHI(new[] { 0.5d, 0.25d, 0.25d });

        Assert.AreEqual(25d, mean, 0.001);
        Assert.AreEqual(0.6d, deviation, 0.001);
        Assert.AreEqual(0.25d, share, 0.001);
        Assert.AreEqual(25d, median, 0.001);
        Assert.AreEqual(125d, variance, 0.001);
        Assert.AreEqual(0.447, coefficientOfVariation, 0.001);
        Assert.AreEqual(0.375d, hhi, 0.001);
    }

    [TestMethod]
    public void ImbalanceRules_UseThresholdRelativeBandsAndWeightedScore()
    {
        Assert.AreEqual(ImbalanceRiskLevel.Low, EffortImbalanceCanonicalRules.ClassifyBucketRisk(0.29, 0.3));
        Assert.AreEqual(ImbalanceRiskLevel.Medium, EffortImbalanceCanonicalRules.ClassifyBucketRisk(0.30, 0.3));
        Assert.AreEqual(ImbalanceRiskLevel.High, EffortImbalanceCanonicalRules.ClassifyBucketRisk(0.45, 0.3));
        Assert.AreEqual(ImbalanceRiskLevel.Critical, EffortImbalanceCanonicalRules.ClassifyBucketRisk(0.75, 0.3));

        var score = EffortImbalanceCanonicalRules.ComputeImbalanceScore(new[] { 0.3d, 0.7d });
        var overallRisk = EffortImbalanceCanonicalRules.ClassifyOverallRisk(0.7d);

        Assert.AreEqual(62d, score, 0.001);
        Assert.AreEqual(ImbalanceRiskLevel.High, overallRisk);
    }

    [TestMethod]
    public void ConcentrationRules_UseFixedBandsAndNormalizedHhi()
    {
        EffortDiagnosticsStatistics statistics = new CanonicalEffortDiagnosticsStatistics();

        Assert.AreEqual(ConcentrationRiskLevel.None, EffortConcentrationCanonicalRules.ClassifyBucketRisk(0.24));
        Assert.AreEqual(ConcentrationRiskLevel.Low, EffortConcentrationCanonicalRules.ClassifyBucketRisk(0.25));
        Assert.AreEqual(ConcentrationRiskLevel.Medium, EffortConcentrationCanonicalRules.ClassifyBucketRisk(0.40));
        Assert.AreEqual(ConcentrationRiskLevel.High, EffortConcentrationCanonicalRules.ClassifyBucketRisk(0.60));
        Assert.AreEqual(ConcentrationRiskLevel.Critical, EffortConcentrationCanonicalRules.ClassifyBucketRisk(0.80));

        var concentrationIndex = EffortConcentrationCanonicalRules.ComputeConcentrationIndex(
            new[] { 0.5d, 0.25d, 0.25d },
            statistics);
        var overallRisk = EffortConcentrationCanonicalRules.ClassifyOverallRisk(0.5d);

        Assert.AreEqual(37.5d, concentrationIndex, 0.001);
        Assert.AreEqual(ConcentrationRiskLevel.Medium, overallRisk);
    }

    [TestMethod]
    public void DomainAnalyses_PreserveSeparateAreaAndIterationBuckets()
    {
        var imbalanceAreaBucket = new EffortImbalanceBucket("Area/Payments", 40, 20, 1.0, ImbalanceRiskLevel.Critical);
        var imbalanceIterationBucket = new EffortImbalanceBucket("Sprint 42", 15, 20, 0.25, ImbalanceRiskLevel.Low);
        var imbalanceAnalysis = new EffortImbalanceAnalysis(
            new[] { imbalanceAreaBucket },
            new[] { imbalanceIterationBucket },
            ImbalanceRiskLevel.Critical,
            85);

        var concentrationAreaBucket = new EffortConcentrationBucket("Area/Payments", 40, 0.5, ConcentrationRiskLevel.Medium);
        var concentrationIterationBucket = new EffortConcentrationBucket("Sprint 42", 20, 0.25, ConcentrationRiskLevel.Low);
        var concentrationAnalysis = new EffortConcentrationAnalysis(
            new[] { concentrationAreaBucket },
            new[] { concentrationIterationBucket },
            ConcentrationRiskLevel.Medium,
            37.5);

        Assert.HasCount(1, imbalanceAnalysis.AreaPathBuckets);
        Assert.HasCount(1, imbalanceAnalysis.IterationPathBuckets);
        Assert.AreEqual("Area/Payments", imbalanceAnalysis.AreaPathBuckets[0].BucketKey);
        Assert.AreEqual(85d, imbalanceAnalysis.ImbalanceScore, 0.001);

        Assert.HasCount(1, concentrationAnalysis.AreaPathBuckets);
        Assert.HasCount(1, concentrationAnalysis.IterationPathBuckets);
        Assert.AreEqual("Sprint 42", concentrationAnalysis.IterationPathBuckets[0].BucketKey);
        Assert.AreEqual(37.5d, concentrationAnalysis.ConcentrationIndex, 0.001);
    }

    [TestMethod]
    public void Buckets_RejectInvalidCanonicalValues()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new EffortImbalanceBucket(" ", 10, 5, 1, ImbalanceRiskLevel.Medium));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new EffortConcentrationBucket("Area/Payments", -1, 0.5, ConcentrationRiskLevel.Low));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new EffortConcentrationAnalysis(Array.Empty<EffortConcentrationBucket>(), Array.Empty<EffortConcentrationBucket>(), ConcentrationRiskLevel.None, 101));
    }
}
