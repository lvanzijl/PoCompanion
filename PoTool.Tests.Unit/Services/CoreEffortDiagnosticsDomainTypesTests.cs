using PoTool.Core.Metrics.EffortDiagnostics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class CoreEffortDiagnosticsDomainTypesTests
{
    [TestMethod]
    public void EffortImbalanceDomainTypes_CreateWithCanonicalProperties()
    {
        var areaBucket = new EffortImbalanceBucket("Area/Payments", 34d, 17d, 1d, ImbalanceRiskLevel.Critical);
        var iterationBucket = new EffortImbalanceBucket("Sprint 42", 12d, 17d, 0.294d, ImbalanceRiskLevel.Low);

        var analysis = new EffortImbalanceAnalysis(
            new[] { areaBucket },
            new[] { iterationBucket },
            ImbalanceRiskLevel.Critical,
            68.4d);

        Assert.AreEqual("Area/Payments", areaBucket.BucketKey);
        Assert.AreEqual(34d, areaBucket.EffortAmount);
        Assert.AreEqual(17d, areaBucket.MeanEffort);
        Assert.AreEqual(1d, areaBucket.DeviationFromMean);
        Assert.AreEqual(ImbalanceRiskLevel.Critical, areaBucket.RiskLevel);

        Assert.HasCount(1, analysis.AreaBuckets);
        Assert.HasCount(1, analysis.IterationBuckets);
        Assert.AreEqual(ImbalanceRiskLevel.Critical, analysis.OverallRiskLevel);
        Assert.AreEqual(68.4d, analysis.ImbalanceScore);
    }

    [TestMethod]
    public void EffortConcentrationDomainTypes_CreateWithCanonicalProperties()
    {
        var areaBucket = new EffortConcentrationBucket("Area/Payments", 40d, 0.5d, ConcentrationRiskLevel.Medium);
        var iterationBucket = new EffortConcentrationBucket("Sprint 42", 20d, 0.25d, ConcentrationRiskLevel.Low);

        var analysis = new EffortConcentrationAnalysis(
            new[] { areaBucket },
            new[] { iterationBucket },
            ConcentrationRiskLevel.Medium,
            37.5d);

        Assert.AreEqual("Area/Payments", areaBucket.BucketKey);
        Assert.AreEqual(40d, areaBucket.EffortAmount);
        Assert.AreEqual(0.5d, areaBucket.EffortShare);
        Assert.AreEqual(ConcentrationRiskLevel.Medium, areaBucket.RiskLevel);

        Assert.HasCount(1, analysis.AreaBuckets);
        Assert.HasCount(1, analysis.IterationBuckets);
        Assert.AreEqual(ConcentrationRiskLevel.Medium, analysis.OverallRiskLevel);
        Assert.AreEqual(37.5d, analysis.ConcentrationIndex);
    }
}
