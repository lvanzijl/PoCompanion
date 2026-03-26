using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Produces explainable, read-only decision signals from canonical delivery-trend outputs.
/// </summary>
public interface IInsightService
{
    /// <summary>
    /// Combines existing product aggregation, snapshot comparison, and planning quality outputs into insights.
    /// </summary>
    InsightResult Analyze(InsightRequest request);
}

/// <summary>
/// Pure centralized insight engine.
/// </summary>
public sealed class InsightService : IInsightService
{
    /// <inheritdoc />
    public InsightResult Analyze(InsightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Product);
        ArgumentNullException.ThrowIfNull(request.Comparison);
        ArgumentNullException.ThrowIfNull(request.PlanningQuality);

        var context = new InsightContext(
            request.Comparison.ProgressDelta,
            request.Comparison.ForecastRemainingDelta,
            request.PlanningQuality.Score);
        var insights = new List<Insight>();
        var progressDelta = request.Comparison.ProgressDelta;
        var forecastRemainingDelta = request.Comparison.ForecastRemainingDelta;
        var planningQualityScore = request.PlanningQuality.Score;

        if (progressDelta.HasValue && progressDelta.Value == 0d)
        {
            insights.Add(CreateInsight(
                InsightCodes.ProgressStalled,
                InsightSeverity.Warning,
                "Progress has stalled since the last snapshot.",
                context));
        }

        if (!progressDelta.HasValue)
        {
            insights.Add(CreateInsight(
                InsightCodes.ProgressUnknown,
                InsightSeverity.Warning,
                "Progress cannot be determined due to missing or incomplete snapshot data.",
                context));
        }

        if (progressDelta is < 0d)
        {
            insights.Add(CreateInsight(
                InsightCodes.ProgressReversed,
                InsightSeverity.Critical,
                "Progress has moved backward since the last snapshot.",
                context));
        }

        if (forecastRemainingDelta is > 0d && progressDelta is <= 0d)
        {
            insights.Add(CreateInsight(
                InsightCodes.ScopeIncreasedFasterThanDelivery,
                InsightSeverity.Critical,
                "Forecast remaining effort increased while delivery did not improve.",
                context));
        }

        if (progressDelta is > 0d && forecastRemainingDelta is <= 0d)
        {
            insights.Add(CreateInsight(
                InsightCodes.HealthyProgress,
                InsightSeverity.Info,
                "Progress improved while forecast remaining effort stayed flat or decreased.",
                context));
        }

        if (planningQualityScore < 70)
        {
            insights.Add(CreateInsight(
                InsightCodes.LowPlanningQuality,
                InsightSeverity.Warning,
                "Planning quality is below the acceptable threshold.",
                context));
        }

        if (planningQualityScore < 50)
        {
            insights.Add(CreateInsight(
                InsightCodes.VeryLowPlanningQuality,
                InsightSeverity.Critical,
                "Planning quality is very low and reduces confidence in the underlying plan.",
                context));
        }

        if (ContainsUnreliableForecastSignal(request.PlanningQuality.Signals))
        {
            insights.Add(CreateInsight(
                InsightCodes.ForecastUnreliable,
                InsightSeverity.Warning,
                "Forecast reliability is reduced by missing effort, missing progress basis, or missing forecast data signals.",
                context));
        }

        return new InsightResult(insights);
    }

    private static bool ContainsUnreliableForecastSignal(IReadOnlyList<PlanningQualitySignal> signals)
    {
        return signals.Any(signal => signal.Code is PlanningQualitySignalCodes.FeatureMissingEffort
            or PlanningQualitySignalCodes.FeatureMissingProgressBasis
            or PlanningQualitySignalCodes.MissingForecastData);
    }

    private static Insight CreateInsight(
        string code,
        InsightSeverity severity,
        string message,
        InsightContext context)
    {
        DeliveryTrendModelValidation.ValidateRequiredText(code, nameof(code), "Insight code");
        DeliveryTrendModelValidation.ValidateRequiredText(message, nameof(message), "Insight message");
        ArgumentNullException.ThrowIfNull(context);

        return new Insight(code, severity, message, context);
    }
}
