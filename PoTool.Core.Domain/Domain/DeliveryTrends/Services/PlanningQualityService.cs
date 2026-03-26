using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Produces centralized read-only Planning Quality diagnostics from already-computed delivery data.
/// </summary>
public interface IPlanningQualityService
{
    /// <summary>
    /// Analyzes planning quality signals and score without mutating upstream calculations.
    /// </summary>
    PlanningQualityResult Analyze(PlanningQualityRequest request);
}

/// <summary>
/// Pure diagnostic engine for centralized Planning Quality warnings and completeness indicators.
/// </summary>
public sealed class PlanningQualityService : IPlanningQualityService
{
    private const int StartingScore = 100;
    private const int WarningDeduction = 5;
    private const int CriticalDeduction = 15;

    /// <inheritdoc />
    public PlanningQualityResult Analyze(PlanningQualityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Product);
        DeliveryTrendModelValidation.ValidatePositiveId(request.ProductId, nameof(request.ProductId), "Product ID");

        var signals = new List<PlanningQualitySignal>();
        var features = request.Features ?? [];
        var epics = request.Epics ?? [];

        foreach (var feature in features)
        {
            if (!feature.Effort.HasValue)
            {
                signals.Add(CreateSignal(
                    PlanningQualitySignalCodes.FeatureMissingEffort,
                    PlanningQualitySeverity.Warning,
                    PlanningQualityScope.Feature,
                    "Feature effort is missing, so forecast effort cannot be fully trusted.",
                    feature.FeatureId));
            }

            if (feature.IsExcluded || feature.Weight <= 0)
            {
                signals.Add(CreateSignal(
                    PlanningQualitySignalCodes.FeatureMissingProgressBasis,
                    PlanningQualitySeverity.Critical,
                    PlanningQualityScope.Feature,
                    "Feature has no usable PBIs or weight and is excluded from weighted aggregation.",
                    feature.FeatureId));
            }

            if (feature.Override.HasValue)
            {
                signals.Add(CreateSignal(
                    PlanningQualitySignalCodes.FeatureUsingOverride,
                    PlanningQualitySeverity.Info,
                    PlanningQualityScope.Feature,
                    "Feature is using a manual override value for effective progress.",
                    feature.FeatureId));
            }

            if (feature.Override is > 0d and <= 1d)
            {
                signals.Add(CreateSignal(
                    PlanningQualitySignalCodes.SuspiciousOverrideRange,
                    PlanningQualitySeverity.Warning,
                    PlanningQualityScope.Feature,
                    "Feature override is between 0.01 and 1.0, which likely indicates a 0-1 scale instead of 0-100.",
                    feature.FeatureId));
            }

            if (!feature.ForecastConsumedEffort.HasValue || !feature.ForecastRemainingEffort.HasValue)
            {
                signals.Add(CreateSignal(
                    PlanningQualitySignalCodes.MissingForecastData,
                    PlanningQualitySeverity.Warning,
                    PlanningQualityScope.Feature,
                    "Feature forecast data is missing because consumed or remaining effort is unavailable.",
                    feature.FeatureId));
            }
        }

        foreach (var epic in epics)
        {
            if (epic.ExcludedFeaturesCount > 0)
            {
                signals.Add(CreateSignal(
                    PlanningQualitySignalCodes.EpicContainsExcludedFeatures,
                    PlanningQualitySeverity.Warning,
                    PlanningQualityScope.Epic,
                    "Epic contains excluded features that could not participate in weighted aggregation.",
                    epic.EpicId));
            }

            if (!epic.ForecastConsumedEffort.HasValue || !epic.ForecastRemainingEffort.HasValue)
            {
                signals.Add(CreateSignal(
                    PlanningQualitySignalCodes.MissingForecastData,
                    PlanningQualitySeverity.Warning,
                    PlanningQualityScope.Epic,
                    "Epic forecast data is missing because consumed or remaining effort is unavailable.",
                    epic.EpicId));
            }
        }

        if (request.Product.ExcludedEpicsCount > 0)
        {
            signals.Add(CreateSignal(
                PlanningQualitySignalCodes.ProductContainsExcludedEpics,
                PlanningQualitySeverity.Warning,
                PlanningQualityScope.Product,
                "Product contains excluded epics that could not participate in weighted aggregation.",
                request.ProductId));
        }

        if (!request.Product.ProductForecastConsumed.HasValue || !request.Product.ProductForecastRemaining.HasValue)
        {
            signals.Add(CreateSignal(
                PlanningQualitySignalCodes.MissingForecastData,
                PlanningQualitySeverity.Warning,
                PlanningQualityScope.Product,
                "Product forecast data is missing because consumed or remaining effort is unavailable.",
                request.ProductId));
        }

        return new PlanningQualityResult(
            ComputeScore(signals),
            signals);
    }

    private static int ComputeScore(IReadOnlyList<PlanningQualitySignal> signals)
    {
        var score = StartingScore;

        foreach (var signal in signals)
        {
            score -= signal.Severity switch
            {
                PlanningQualitySeverity.Warning => WarningDeduction,
                PlanningQualitySeverity.Critical => CriticalDeduction,
                _ => 0
            };
        }

        return Math.Clamp(score, 0, 100);
    }

    private static PlanningQualitySignal CreateSignal(
        string code,
        PlanningQualitySeverity severity,
        PlanningQualityScope scope,
        string message,
        int entityId)
    {
        DeliveryTrendModelValidation.ValidatePositiveId(entityId, nameof(entityId), "Planning Quality entity ID");
        DeliveryTrendModelValidation.ValidateRequiredText(code, nameof(code), "Planning Quality code");
        DeliveryTrendModelValidation.ValidateRequiredText(message, nameof(message), "Planning Quality message");

        return new PlanningQualitySignal(code, severity, scope, message, entityId);
    }
}
