using PoTool.Core.Domain.Statistics;

namespace PoTool.Core.Domain.Cdc.ExecutionRealityCheck;

/// <summary>
/// Canonical internal keys for the execution reality-check CDC slice.
/// </summary>
public static class ExecutionRealityCheckCdcKeys
{
    public const string CommitmentCompletionMetricKey = "commitment-completion";
    public const string SpilloverRateMetricKey = "spillover-rate";

    public const string CompletionBelowTypicalAnomalyKey = "completion-below-typical";
    public const string CompletionVariabilityAnomalyKey = "completion-variability";
    public const string SpilloverIncreaseAnomalyKey = "spillover-increase";
}

/// <summary>
/// Result of attempting to build the execution reality-check CDC slice.
/// </summary>
public sealed record ExecutionRealityCheckCdcSliceResult(
    bool HasSufficientEvidence,
    ExecutionRealityCheckCdcSlice? Slice)
{
    public static ExecutionRealityCheckCdcSliceResult InsufficientEvidence()
        => new(false, null);

    public static ExecutionRealityCheckCdcSliceResult Success(ExecutionRealityCheckCdcSlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        return new(true, slice);
    }
}

/// <summary>
/// Internal CDC slice containing the validated historical window, baselines, and raw anomaly inputs.
/// </summary>
public sealed record ExecutionRealityCheckCdcSlice(
    IReadOnlyList<ExecutionRealityCheckWindowRow> WindowRows,
    IReadOnlyList<ExecutionRealityCheckBaseline> Baselines,
    IReadOnlyList<ExecutionRealityCheckAnomalyInput> AnomalyInputs);

/// <summary>
/// Internal per-sprint row for the execution reality-check window.
/// </summary>
public sealed record ExecutionRealityCheckWindowRow(
    int SprintId,
    string SprintPath,
    int TeamId,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    double CommitmentCompletion,
    double SpilloverRate,
    bool HasAuthoritativeDenominator,
    bool HasContinuousOrdering);

/// <summary>
/// Median-centered spread reference without thresholds or interpretation states.
/// </summary>
public sealed record ExecutionRealityCheckSpreadReference(
    double MinimumObservedValue,
    double MaximumObservedValue,
    IReadOnlyList<double> OrderedDeviationFromMedian);

/// <summary>
/// Internal baseline for one canonical anomaly metric.
/// </summary>
public sealed record ExecutionRealityCheckBaseline(
    string MetricKey,
    IReadOnlyList<int> WindowSprintIds,
    IReadOnlyList<double> WindowValues,
    double Median,
    ExecutionRealityCheckSpreadReference SpreadReference);

/// <summary>
/// Raw per-anomaly input prepared for the later interpretation layer.
/// </summary>
public sealed record ExecutionRealityCheckAnomalyInput(
    string AnomalyKey,
    string MetricKey,
    int CurrentSprintId,
    double CurrentValue,
    double BaselineMedian,
    ExecutionRealityCheckSpreadReference BaselineSpreadReference,
    IReadOnlyList<int> OrderedWindowSprintIds,
    IReadOnlyList<double> OrderedWindowValues);

/// <summary>
/// Pure projector for turning a validated 8-sprint window into baselines and raw anomaly inputs.
/// </summary>
public interface IExecutionRealityCheckCdcSliceProjector
{
    ExecutionRealityCheckCdcSliceResult TryProject(IReadOnlyList<ExecutionRealityCheckWindowRow> orderedWindowRows);
}

/// <summary>
/// Default implementation of the Phase 23c execution reality-check CDC projector.
/// </summary>
public sealed class ExecutionRealityCheckCdcSliceProjector : IExecutionRealityCheckCdcSliceProjector
{
    public const int RequiredWindowSize = 8;

    public ExecutionRealityCheckCdcSliceResult TryProject(IReadOnlyList<ExecutionRealityCheckWindowRow> orderedWindowRows)
    {
        ArgumentNullException.ThrowIfNull(orderedWindowRows);

        if (orderedWindowRows.Count != RequiredWindowSize
            || orderedWindowRows.Any(row => !row.HasAuthoritativeDenominator || !row.HasContinuousOrdering))
        {
            return ExecutionRealityCheckCdcSliceResult.InsufficientEvidence();
        }

        var completionBaseline = CreateBaseline(
            ExecutionRealityCheckCdcKeys.CommitmentCompletionMetricKey,
            orderedWindowRows,
            static row => row.CommitmentCompletion);
        var spilloverBaseline = CreateBaseline(
            ExecutionRealityCheckCdcKeys.SpilloverRateMetricKey,
            orderedWindowRows,
            static row => row.SpilloverRate);
        var currentSprint = orderedWindowRows[^1];

        return ExecutionRealityCheckCdcSliceResult.Success(new ExecutionRealityCheckCdcSlice(
            orderedWindowRows.ToList(),
            [completionBaseline, spilloverBaseline],
            [
                CreateAnomalyInput(
                    ExecutionRealityCheckCdcKeys.CompletionBelowTypicalAnomalyKey,
                    completionBaseline,
                    currentSprint.SprintId,
                    currentSprint.CommitmentCompletion),
                CreateAnomalyInput(
                    ExecutionRealityCheckCdcKeys.CompletionVariabilityAnomalyKey,
                    completionBaseline,
                    currentSprint.SprintId,
                    currentSprint.CommitmentCompletion),
                CreateAnomalyInput(
                    ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey,
                    spilloverBaseline,
                    currentSprint.SprintId,
                    currentSprint.SpilloverRate)
            ]));
    }

    private static ExecutionRealityCheckBaseline CreateBaseline(
        string metricKey,
        IReadOnlyList<ExecutionRealityCheckWindowRow> orderedWindowRows,
        Func<ExecutionRealityCheckWindowRow, double> selector)
    {
        var sprintIds = orderedWindowRows.Select(static row => row.SprintId).ToList();
        var values = orderedWindowRows.Select(selector).ToList();
        var median = StatisticsMath.Median(values);

        return new ExecutionRealityCheckBaseline(
            metricKey,
            sprintIds,
            values,
            median,
            new ExecutionRealityCheckSpreadReference(
                values.Min(),
                values.Max(),
                values.Select(value => value - median).ToList()));
    }

    private static ExecutionRealityCheckAnomalyInput CreateAnomalyInput(
        string anomalyKey,
        ExecutionRealityCheckBaseline baseline,
        int currentSprintId,
        double currentValue)
    {
        return new ExecutionRealityCheckAnomalyInput(
            anomalyKey,
            baseline.MetricKey,
            currentSprintId,
            currentValue,
            baseline.Median,
            baseline.SpreadReference,
            baseline.WindowSprintIds,
            baseline.WindowValues);
    }
}
