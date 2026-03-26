using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PlanningQualityServiceTests
{
    private static readonly IPlanningQualityService Service = new PlanningQualityService();

    [TestMethod]
    public void Analyze_EmitsRequiredFeatureSignals_WithoutChangingAnyUpstreamValues()
    {
        var feature = CreateFeatureProgress(
            featureId: 101,
            effort: null,
            weight: 0,
            isExcluded: true,
            overrideProgress: 0.5d,
            forecastConsumedEffort: null,
            forecastRemainingEffort: null);
        var request = new PlanningQualityRequest(
            ProductId: 9001,
            Features: [feature],
            Epics: Array.Empty<EpicProgress>(),
            Product: new ProductAggregationResult(50d, 20d, 30d, 0, 1, 10d));

        var result = Service.Analyze(request);

        Assert.AreEqual(70, result.Score);
        Assert.HasCount(5, result.Signals);
        AssertSignal(result, PlanningQualitySignalCodes.FeatureMissingEffort, PlanningQualitySeverity.Warning, PlanningQualityScope.Feature, 101);
        AssertSignal(result, PlanningQualitySignalCodes.FeatureMissingProgressBasis, PlanningQualitySeverity.Critical, PlanningQualityScope.Feature, 101);
        AssertSignal(result, PlanningQualitySignalCodes.FeatureUsingOverride, PlanningQualitySeverity.Info, PlanningQualityScope.Feature, 101);
        AssertSignal(result, PlanningQualitySignalCodes.SuspiciousOverrideRange, PlanningQualitySeverity.Warning, PlanningQualityScope.Feature, 101);
        AssertSignal(result, PlanningQualitySignalCodes.MissingForecastData, PlanningQualitySeverity.Warning, PlanningQualityScope.Feature, 101);
        Assert.IsNull(feature.Effort, "Planning Quality must not mutate the input feature data.");
        Assert.AreEqual(0d, feature.Weight, 0.001d, "Planning Quality must stay read-only.");
        Assert.AreEqual(0.5d, feature.Override!.Value, 0.001d, "Planning Quality must not rewrite override values.");
    }

    [TestMethod]
    public void Analyze_EmitsEpicAndProductSignals_ForExcludedChildrenAndMissingForecasts()
    {
        var epic = CreateEpicProgress(
            epicId: 301,
            excludedFeaturesCount: 2,
            forecastConsumedEffort: null,
            forecastRemainingEffort: null);
        var request = new PlanningQualityRequest(
            ProductId: 9001,
            Features: Array.Empty<FeatureProgress>(),
            Epics: [epic],
            Product: new ProductAggregationResult(80d, null, null, 3, 1, 21d));

        var result = Service.Analyze(request);

        Assert.AreEqual(80, result.Score);
        Assert.HasCount(4, result.Signals);
        AssertSignal(result, PlanningQualitySignalCodes.EpicContainsExcludedFeatures, PlanningQualitySeverity.Warning, PlanningQualityScope.Epic, 301);
        AssertSignal(result, PlanningQualitySignalCodes.MissingForecastData, PlanningQualitySeverity.Warning, PlanningQualityScope.Epic, 301);
        AssertSignal(result, PlanningQualitySignalCodes.ProductContainsExcludedEpics, PlanningQualitySeverity.Warning, PlanningQualityScope.Product, 9001);
        AssertSignal(result, PlanningQualitySignalCodes.MissingForecastData, PlanningQualitySeverity.Warning, PlanningQualityScope.Product, 9001);
    }

    [TestMethod]
    public void Analyze_ClampsScoreAtZero_WhenCriticalSignalsExceedAvailablePoints()
    {
        var features = Enumerable.Range(1, 8)
            .Select(index => CreateFeatureProgress(
                featureId: index,
                effort: 10d,
                weight: 0,
                isExcluded: true,
                overrideProgress: null,
                forecastConsumedEffort: 0d,
                forecastRemainingEffort: 10d))
            .ToList();
        var request = new PlanningQualityRequest(
            ProductId: 9001,
            Features: features,
            Epics: Array.Empty<EpicProgress>(),
            Product: new ProductAggregationResult(10d, 1d, 9d, 0, 0, 0d));

        var result = Service.Analyze(request);

        Assert.AreEqual(0, result.Score);
        Assert.AreEqual(8, result.Signals.Count(signal => signal.Code == PlanningQualitySignalCodes.FeatureMissingProgressBasis));
    }

    private static void AssertSignal(
        PlanningQualityResult result,
        string code,
        PlanningQualitySeverity severity,
        PlanningQualityScope scope,
        int entityId)
    {
        var signal = result.Signals.Single(candidate =>
            candidate.Code == code
            && candidate.Scope == scope
            && candidate.EntityId == entityId);

        Assert.AreEqual(severity, signal.Severity);
        Assert.IsFalse(string.IsNullOrWhiteSpace(signal.Message));
    }

    private static FeatureProgress CreateFeatureProgress(
        int featureId,
        double? effort,
        double weight,
        bool isExcluded,
        double? overrideProgress,
        double? forecastConsumedEffort,
        double? forecastRemainingEffort)
    {
        var progressPercent = overrideProgress.HasValue
            ? (int)Math.Round(Math.Clamp(overrideProgress.Value, 0d, 100d), MidpointRounding.AwayFromZero)
            : 0;

        return new FeatureProgress(
            featureId,
            $"Feature {featureId}",
            9001,
            301,
            "Epic 301",
            progressPercent,
            totalScopeStoryPoints: Math.Max(weight, 0),
            deliveredStoryPoints: 0,
            donePbiCount: 0,
            isDone: false,
            sprintDeliveredStoryPoints: 0,
            sprintProgressionDelta: new ProgressionDelta(0),
            sprintEffortDelta: 0,
            sprintCompletedPbiCount: 0,
            sprintCompletedInSprint: false,
            calculatedProgress: null,
            overrideProgress: overrideProgress,
            effectiveProgress: overrideProgress,
            validationSignals: Array.Empty<string>(),
            forecastConsumedEffort: forecastConsumedEffort,
            forecastRemainingEffort: forecastRemainingEffort,
            weight: weight,
            isExcluded: isExcluded,
            effort: effort);
    }

    private static EpicProgress CreateEpicProgress(
        int epicId,
        int excludedFeaturesCount,
        double? forecastConsumedEffort,
        double? forecastRemainingEffort)
    {
        return new EpicProgress(
            epicId,
            $"Epic {epicId}",
            9001,
            progressPercent: 50,
            totalScopeStoryPoints: 10,
            deliveredStoryPoints: 5,
            featureCount: 2,
            doneFeatureCount: 1,
            donePbiCount: 3,
            isDone: false,
            sprintDeliveredStoryPoints: 0,
            sprintProgressionDelta: new ProgressionDelta(0),
            sprintEffortDelta: 0,
            sprintCompletedPbiCount: 0,
            sprintCompletedFeatureCount: 0,
            aggregatedProgress: 50d,
            forecastConsumedEffort: forecastConsumedEffort,
            forecastRemainingEffort: forecastRemainingEffort,
            excludedFeaturesCount: excludedFeaturesCount,
            includedFeaturesCount: 1,
            totalWeight: 10d);
    }
}
