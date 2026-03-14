namespace PoTool.Core.Metrics.Services;

/// <summary>
/// Calculates canonical sprint execution story-point metrics from reconstructed totals.
/// </summary>
public interface ISprintExecutionMetricsCalculator
{
    /// <summary>
    /// Calculates canonical sprint execution metrics using already reconstructed story-point totals.
    /// </summary>
    /// <param name="input">The reconstructed sprint execution story-point totals.</param>
    /// <returns>The totals with canonical derived rates.</returns>
    SprintExecutionMetricsResult Calculate(SprintExecutionMetricsInput input);
}

/// <summary>
/// Reconstructed sprint execution story-point totals supplied to the calculator.
/// </summary>
/// <param name="CommittedSP">Committed scope story points.</param>
/// <param name="AddedSP">Scope added after commitment.</param>
/// <param name="RemovedSP">Scope removed after commitment.</param>
/// <param name="DeliveredSP">Delivered story points within the sprint window.</param>
/// <param name="DeliveredFromAddedSP">Delivered story points from added scope.</param>
/// <param name="SpilloverSP">Committed story points that spilled to the next sprint.</param>
public sealed record SprintExecutionMetricsInput(
    double CommittedSP,
    double AddedSP,
    double RemovedSP,
    double DeliveredSP,
    double DeliveredFromAddedSP,
    double SpilloverSP);

/// <summary>
/// Calculated sprint execution story-point metrics.
/// </summary>
/// <param name="CommittedSP">Committed scope story points.</param>
/// <param name="AddedSP">Scope added after commitment.</param>
/// <param name="RemovedSP">Scope removed after commitment.</param>
/// <param name="DeliveredSP">Delivered story points within the sprint window.</param>
/// <param name="DeliveredFromAddedSP">Delivered story points from added scope.</param>
/// <param name="SpilloverSP">Committed story points that spilled to the next sprint.</param>
/// <param name="ChurnRate">Canonical churn rate.</param>
/// <param name="CommitmentCompletion">Canonical commitment completion.</param>
/// <param name="SpilloverRate">Canonical spillover rate.</param>
/// <param name="AddedDeliveryRate">Canonical added delivery rate.</param>
public sealed record SprintExecutionMetricsResult(
    double CommittedSP,
    double AddedSP,
    double RemovedSP,
    double DeliveredSP,
    double DeliveredFromAddedSP,
    double SpilloverSP,
    double ChurnRate,
    double CommitmentCompletion,
    double SpilloverRate,
    double AddedDeliveryRate);

/// <summary>
/// Implements canonical sprint execution formulas from the domain metrics rules.
/// </summary>
public sealed class SprintExecutionMetricsCalculator : ISprintExecutionMetricsCalculator
{
    public SprintExecutionMetricsResult Calculate(SprintExecutionMetricsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new SprintExecutionMetricsResult(
            input.CommittedSP,
            input.AddedSP,
            input.RemovedSP,
            input.DeliveredSP,
            input.DeliveredFromAddedSP,
            input.SpilloverSP,
            ChurnRate: SafeDivide(input.AddedSP + input.RemovedSP, input.CommittedSP + input.AddedSP),
            CommitmentCompletion: SafeDivide(input.DeliveredSP, input.CommittedSP - input.RemovedSP),
            SpilloverRate: SafeDivide(input.SpilloverSP, input.CommittedSP - input.RemovedSP),
            AddedDeliveryRate: SafeDivide(input.DeliveredFromAddedSP, input.AddedSP));
    }

    private static double SafeDivide(double numerator, double denominator)
    {
        return denominator <= 0d ? 0d : numerator / denominator;
    }
}
