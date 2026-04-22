using PoTool.Core.Domain.Cdc.ExecutionRealityCheck;

namespace PoTool.Core.Domain.Tests;

[TestClass]
public sealed class ExecutionRealityCheckInterpretationServiceTests
{
    private readonly ExecutionRealityCheckCdcSliceProjector _projector = new();
    private readonly ExecutionRealityCheckInterpretationService _service = new();

    [TestMethod]
    public void Interpret_WhenSpilloverConditionPersistsForThreeSprints_MarksWeakAndWatch()
    {
        var interpretation = Interpret(
            completionSeries: [0.8, 0.8, 0.8, 0.8, 0.8, 0.8, 0.8, 0.8],
            spilloverSeries: [0.1, 0.1, 0.1, 0.1, 0.1, 0.9, 0.9, 0.9]);

        var spillover = GetAnomaly(interpretation, ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey);
        Assert.AreEqual(ExecutionRealityCheckAnomalyStatus.Weak, spillover.Status);
        Assert.AreEqual(3, spillover.PersistenceLength);
        Assert.AreEqual(ExecutionRealityCheckOverallState.Watch, interpretation.OverallState);
        Assert.AreEqual(1, interpretation.TotalSeverity);
    }

    [TestMethod]
    public void Interpret_WhenSpilloverConditionPersistsForFourSprints_MarksStrongAndInvestigate()
    {
        var interpretation = Interpret(
            completionSeries: [0.8, 0.8, 0.8, 0.8, 0.8, 0.8, 0.8, 0.8],
            spilloverSeries: [0.1, 0.1, 0.1, 0.1, 0.9, 0.9, 0.9, 0.9]);

        var spillover = GetAnomaly(interpretation, ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey);
        Assert.AreEqual(ExecutionRealityCheckAnomalyStatus.Strong, spillover.Status);
        Assert.AreEqual(4, spillover.PersistenceLength);
        Assert.AreEqual(ExecutionRealityCheckOverallState.Investigate, interpretation.OverallState);
        Assert.AreEqual(2, interpretation.TotalSeverity);
    }

    [TestMethod]
    public void Interpret_WhenCompletionVariabilityPersistsForThreeSprints_MarksWeak()
    {
        var interpretation = Interpret(
            completionSeries: [0.5, 0.5, 0.5, 0.5, 0.5, 0.1, 0.9, 0.1],
            spilloverSeries: [0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1]);

        var variability = GetAnomaly(interpretation, ExecutionRealityCheckCdcKeys.CompletionVariabilityAnomalyKey);
        Assert.AreEqual(ExecutionRealityCheckAnomalyStatus.Weak, variability.Status);
        Assert.AreEqual(3, variability.PersistenceLength);
    }

    [TestMethod]
    public void Interpret_WhenFirstNormalSprintFollowsSustainedAnomaly_KeepsWeakPendingClear()
    {
        var interpretation = Interpret(
            completionSeries: [0.8, 0.8, 0.8, 0.8, 0.8, 0.8, 0.8, 0.8],
            spilloverSeries: [0.1, 0.1, 0.1, 0.9, 0.9, 0.9, 0.9, 0.1]);

        var spillover = GetAnomaly(interpretation, ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey);
        Assert.AreEqual(ExecutionRealityCheckAnomalyStatus.Weak, spillover.Status);
        Assert.AreEqual(4, spillover.PersistenceLength);
        Assert.AreEqual(ExecutionRealityCheckOverallState.Watch, interpretation.OverallState);
        Assert.AreEqual(1, interpretation.TotalSeverity);
    }

    [TestMethod]
    public void Interpret_WhenTwoNormalSprintsFollowSustainedAnomaly_ClearsToInactive()
    {
        var interpretation = Interpret(
            completionSeries: [0.8, 0.8, 0.8, 0.8, 0.8, 0.8, 0.8, 0.8],
            spilloverSeries: [0.1, 0.1, 0.9, 0.9, 0.9, 0.9, 0.1, 0.1]);

        var spillover = GetAnomaly(interpretation, ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey);
        Assert.AreEqual(ExecutionRealityCheckAnomalyStatus.Inactive, spillover.Status);
        Assert.AreEqual(0, spillover.PersistenceLength);
        Assert.AreEqual(ExecutionRealityCheckOverallState.Stable, interpretation.OverallState);
        Assert.AreEqual(0, interpretation.TotalSeverity);
    }

    [TestMethod]
    public void Interpret_WhenMultipleAnomaliesAreActive_EscalatesToInvestigate()
    {
        var interpretation = Interpret(
            completionSeries: [0.9, 0.9, 0.9, 0.9, 0.9, 0.1, 0.1, 0.1],
            spilloverSeries: [0.1, 0.1, 0.1, 0.1, 0.1, 0.9, 0.9, 0.9]);

        var completion = GetAnomaly(interpretation, ExecutionRealityCheckCdcKeys.CompletionBelowTypicalAnomalyKey);
        var spillover = GetAnomaly(interpretation, ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey);

        Assert.AreEqual(ExecutionRealityCheckAnomalyStatus.Weak, completion.Status);
        Assert.AreEqual(ExecutionRealityCheckAnomalyStatus.Weak, spillover.Status);
        Assert.AreEqual(ExecutionRealityCheckOverallState.Investigate, interpretation.OverallState);
        Assert.IsGreaterThanOrEqualTo(2, interpretation.TotalSeverity);
    }

    [TestMethod]
    public void Interpret_WhenSliceHasInsufficientEvidence_PassthroughsInsufficientEvidence()
    {
        var interpretation = _service.Interpret(ExecutionRealityCheckCdcSliceResult.InsufficientEvidence());

        Assert.AreEqual(ExecutionRealityCheckOverallState.InsufficientEvidence, interpretation.OverallState);
        Assert.AreEqual(0, interpretation.TotalSeverity);
        Assert.HasCount(0, interpretation.Anomalies);
    }

    private ExecutionRealityCheckInterpretation Interpret(
        IReadOnlyList<double> completionSeries,
        IReadOnlyList<double> spilloverSeries)
    {
        var result = _projector.TryProject(CreateWindowRows(completionSeries, spilloverSeries));
        Assert.IsTrue(result.HasSufficientEvidence);

        return _service.Interpret(result);
    }

    private static IReadOnlyList<ExecutionRealityCheckWindowRow> CreateWindowRows(
        IReadOnlyList<double> completionSeries,
        IReadOnlyList<double> spilloverSeries)
    {
        Assert.HasCount(ExecutionRealityCheckCdcSliceProjector.RequiredWindowSize, completionSeries);
        Assert.HasCount(ExecutionRealityCheckCdcSliceProjector.RequiredWindowSize, spilloverSeries);

        var startDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return Enumerable.Range(0, ExecutionRealityCheckCdcSliceProjector.RequiredWindowSize)
            .Select(index => new ExecutionRealityCheckWindowRow(
                SprintId: index + 1,
                SprintPath: $@"\Project\Sprint {index + 1}",
                TeamId: 1,
                StartDateUtc: startDateUtc.AddDays(index * 14),
                EndDateUtc: startDateUtc.AddDays((index * 14) + 13),
                CommitmentCompletion: completionSeries[index],
                SpilloverRate: spilloverSeries[index],
                HasAuthoritativeDenominator: true,
                HasContinuousOrdering: true))
            .ToList();
    }

    private static ExecutionRealityCheckAnomalyInterpretation GetAnomaly(
        ExecutionRealityCheckInterpretation interpretation,
        string anomalyKey)
    {
        return interpretation.Anomalies.Single(anomaly => anomaly.AnomalyKey == anomalyKey);
    }
}
