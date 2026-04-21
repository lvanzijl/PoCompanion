using PoTool.Shared.Statistics;

namespace PoTool.Core.Domain.Cdc.ExecutionRealityCheck;

/// <summary>
/// Internal per-anomaly interpretation statuses for the execution reality-check layer.
/// </summary>
public enum ExecutionRealityCheckAnomalyStatus
{
    Inactive = 0,
    Weak = 1,
    Strong = 2
}

/// <summary>
/// Internal overall interpretation states for the execution reality-check layer.
/// </summary>
public enum ExecutionRealityCheckOverallState
{
    Stable = 0,
    Watch = 1,
    Investigate = 2,
    InsufficientEvidence = 3
}

/// <summary>
/// Interpreted state for one execution anomaly.
/// </summary>
public sealed record ExecutionRealityCheckAnomalyInterpretation(
    string AnomalyKey,
    ExecutionRealityCheckAnomalyStatus Status,
    int PersistenceLength);

/// <summary>
/// Internal interpretation output for the execution reality-check layer.
/// </summary>
public sealed record ExecutionRealityCheckInterpretation(
    IReadOnlyList<ExecutionRealityCheckAnomalyInterpretation> Anomalies,
    ExecutionRealityCheckOverallState OverallState,
    int TotalSeverity);

/// <summary>
/// Converts the Phase 23c execution CDC slice into anomaly and overall interpretation states.
/// </summary>
public interface IExecutionRealityCheckInterpretationService
{
    ExecutionRealityCheckInterpretation Interpret(ExecutionRealityCheckCdcSliceResult sliceResult);
}

/// <summary>
/// Default implementation of the Phase 24 execution interpretation layer.
/// </summary>
public sealed class ExecutionRealityCheckInterpretationService : IExecutionRealityCheckInterpretationService
{
    private const int WeakTriggerLength = 3;
    private const int StrongTriggerLength = 4;
    private const int ClearTriggerLength = 2;

    public ExecutionRealityCheckInterpretation Interpret(ExecutionRealityCheckCdcSliceResult sliceResult)
    {
        ArgumentNullException.ThrowIfNull(sliceResult);

        if (!sliceResult.HasSufficientEvidence || sliceResult.Slice is null)
        {
            return new ExecutionRealityCheckInterpretation(
                [],
                ExecutionRealityCheckOverallState.InsufficientEvidence,
                TotalSeverity: 0);
        }

        var anomalyInterpretations = sliceResult.Slice.AnomalyInputs
            .Select(InterpretAnomaly)
            .ToList();
        var totalSeverity = anomalyInterpretations.Sum(GetSeverity);
        var overallState = totalSeverity switch
        {
            <= 0 => ExecutionRealityCheckOverallState.Stable,
            1 => ExecutionRealityCheckOverallState.Watch,
            _ => ExecutionRealityCheckOverallState.Investigate
        };

        return new ExecutionRealityCheckInterpretation(
            anomalyInterpretations,
            overallState,
            totalSeverity);
    }

    private static ExecutionRealityCheckAnomalyInterpretation InterpretAnomaly(ExecutionRealityCheckAnomalyInput input)
    {
        var orderedConditions = input.OrderedWindowValues
            .Select(value => EvaluateCondition(
                input.AnomalyKey,
                value,
                input.BaselineMedian,
                input.BaselineSpreadReference))
            .ToList();

        var trailingConditionLength = CountTrailingValues(orderedConditions, expectedValue: true);
        if (trailingConditionLength >= StrongTriggerLength)
        {
            return new ExecutionRealityCheckAnomalyInterpretation(
                input.AnomalyKey,
                ExecutionRealityCheckAnomalyStatus.Strong,
                trailingConditionLength);
        }

        if (trailingConditionLength >= WeakTriggerLength)
        {
            return new ExecutionRealityCheckAnomalyInterpretation(
                input.AnomalyKey,
                ExecutionRealityCheckAnomalyStatus.Weak,
                trailingConditionLength);
        }

        var trailingNormalLength = CountTrailingValues(orderedConditions, expectedValue: false);
        if (trailingNormalLength == 1)
        {
            var priorConditionLength = CountTrailingValues(
                orderedConditions.Take(orderedConditions.Count - 1).ToList(),
                expectedValue: true);

            if (priorConditionLength >= WeakTriggerLength)
            {
                return new ExecutionRealityCheckAnomalyInterpretation(
                    input.AnomalyKey,
                    ExecutionRealityCheckAnomalyStatus.Weak,
                    priorConditionLength);
            }
        }

        if (trailingNormalLength >= ClearTriggerLength)
        {
            return new ExecutionRealityCheckAnomalyInterpretation(
                input.AnomalyKey,
                ExecutionRealityCheckAnomalyStatus.Inactive,
                PersistenceLength: 0);
        }

        return new ExecutionRealityCheckAnomalyInterpretation(
            input.AnomalyKey,
            ExecutionRealityCheckAnomalyStatus.Inactive,
            trailingConditionLength);
    }

    private static bool EvaluateCondition(
        string anomalyKey,
        double value,
        double baselineMedian,
        ExecutionRealityCheckSpreadReference spreadReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anomalyKey);
        ArgumentNullException.ThrowIfNull(spreadReference);

        var deviation = value - baselineMedian;
        var sortedDeviations = spreadReference.OrderedDeviationFromMedian
            .OrderBy(static deviationValue => deviationValue)
            .ToArray();

        if (sortedDeviations.Length == 0)
        {
            return false;
        }

        return anomalyKey switch
        {
            ExecutionRealityCheckCdcKeys.CompletionBelowTypicalAnomalyKey
                => IsBelowTypical(deviation, sortedDeviations),
            ExecutionRealityCheckCdcKeys.CompletionVariabilityAnomalyKey
                => IsVariabilityHigh(deviation, sortedDeviations),
            ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey
                => IsSpilloverIncreasing(deviation, sortedDeviations),
            _ => throw new ArgumentOutOfRangeException(nameof(anomalyKey), anomalyKey, "Unknown execution anomaly key.")
        };
    }

    private static bool IsBelowTypical(double deviation, IReadOnlyList<double> sortedDeviations)
    {
        var lowerBand = PercentileMath.LinearInterpolation(sortedDeviations, 25);
        return lowerBand < 0d && deviation <= lowerBand;
    }

    private static bool IsSpilloverIncreasing(double deviation, IReadOnlyList<double> sortedDeviations)
    {
        var upperBand = PercentileMath.LinearInterpolation(sortedDeviations, 75);
        return upperBand > 0d && deviation >= upperBand;
    }

    private static bool IsVariabilityHigh(double deviation, IReadOnlyList<double> sortedDeviations)
    {
        var absoluteDeviations = sortedDeviations
            .Select(static deviationValue => Math.Abs(deviationValue))
            .OrderBy(static deviationValue => deviationValue)
            .ToArray();
        var deviationBand = PercentileMath.LinearInterpolation(absoluteDeviations, 75);

        return deviationBand > 0d && Math.Abs(deviation) >= deviationBand;
    }

    private static int CountTrailingValues(IReadOnlyList<bool> values, bool expectedValue)
    {
        var count = 0;

        for (var index = values.Count - 1; index >= 0; index--)
        {
            if (values[index] != expectedValue)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static int GetSeverity(ExecutionRealityCheckAnomalyInterpretation anomaly)
    {
        return anomaly.Status switch
        {
            ExecutionRealityCheckAnomalyStatus.Weak => 1,
            ExecutionRealityCheckAnomalyStatus.Strong => 2,
            _ => 0
        };
    }
}
