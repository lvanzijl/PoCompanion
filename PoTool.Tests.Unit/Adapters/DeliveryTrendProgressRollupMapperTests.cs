using PoTool.Api.Adapters;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Adapters;

[TestClass]
public sealed class DeliveryTrendProgressRollupMapperTests
{
    [TestMethod]
    public void ToFeatureProgressDto_MapsCanonicalStoryPointFields()
    {
        var featureProgress = new FeatureProgress(
            featureId: 101,
            featureTitle: "Feature A",
            productId: 5,
            epicId: 42,
            epicTitle: "Epic A",
            progressPercent: 60,
            totalScopeStoryPoints: 13.5,
            deliveredStoryPoints: 8.25,
            donePbiCount: 3,
            isDone: false,
            sprintDeliveredStoryPoints: 2.5,
            sprintProgressionDelta: new ProgressionDelta(18.5),
            sprintEffortDelta: 4,
            sprintCompletedPbiCount: 1,
            sprintCompletedInSprint: false,
            calculatedProgress: 55.5,
            overrideProgress: 70,
            effectiveProgress: 70,
            validationSignals: [FeatureProgressValidationSignals.OverrideOutOfRange],
            forecastConsumedEffort: 9.5,
            forecastRemainingEffort: 3.5,
            weight: 13.5,
            isExcluded: false,
            effort: 16);

        var dto = featureProgress.ToFeatureProgressDto(Array.Empty<CompletedPbiDto>());

        Assert.AreEqual(55.5d, dto.CalculatedProgress!.Value, 0.001d);
        Assert.AreEqual(70d, dto.Override!.Value, 0.001d);
        Assert.AreEqual(70d, dto.EffectiveProgress!.Value, 0.001d);
        Assert.AreEqual(9.5d, dto.ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(3.5d, dto.ForecastRemainingEffort!.Value, 0.001d);
        Assert.AreEqual(16d, dto.Effort!.Value, 0.001d);
        Assert.AreEqual(13.5d, dto.Weight, 0.001d);
        Assert.IsFalse(dto.IsExcluded);
        CollectionAssert.Contains(dto.ValidationSignals.ToList(), FeatureProgressValidationSignals.OverrideOutOfRange);
        Assert.AreEqual(13.5d, dto.TotalStoryPoints, 0.001d);
        Assert.AreEqual(8.25d, dto.DoneStoryPoints, 0.001d);
        Assert.AreEqual(8.25d, dto.DeliveredStoryPoints, 0.001d);
    }

    [TestMethod]
    public void ToFeatureProgress_MapsCanonicalEffortWithoutWorkItemLookup()
    {
        var dto = new FeatureProgressDto
        {
            FeatureId = 101,
            FeatureTitle = "Feature A",
            ProductId = 5,
            ProgressPercent = 60,
            CalculatedProgress = 55.5,
            EffectiveProgress = 60,
            ForecastConsumedEffort = 9.5,
            ForecastRemainingEffort = 3.5,
            Effort = 16,
            Weight = 13.5,
            IsExcluded = false,
            TotalStoryPoints = 13.5,
            DoneStoryPoints = 8.25
        };

        var featureProgress = dto.ToFeatureProgress();

        Assert.AreEqual(16d, featureProgress.Effort!.Value, 0.001d);
        Assert.AreEqual(9.5d, featureProgress.ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(3.5d, featureProgress.ForecastRemainingEffort!.Value, 0.001d);
    }

    [TestMethod]
    public void ToEpicProgressDto_MapsCanonicalStoryPointFields()
    {
        var epicProgress = new EpicProgress(
            epicId: 42,
            epicTitle: "Epic A",
            productId: 5,
            progressPercent: 70,
            totalScopeStoryPoints: 21.5,
            deliveredStoryPoints: 13.25,
            featureCount: 4,
            doneFeatureCount: 2,
            donePbiCount: 5,
            isDone: false,
            sprintDeliveredStoryPoints: 3.5,
            sprintProgressionDelta: new ProgressionDelta(14.5),
            sprintEffortDelta: 6,
            sprintCompletedPbiCount: 2,
            sprintCompletedFeatureCount: 1,
            aggregatedProgress: 61.63,
            forecastConsumedEffort: 20.5,
            forecastRemainingEffort: 34.5,
            excludedFeaturesCount: 1,
            includedFeaturesCount: 3,
            totalWeight: 21.5);

        var dto = epicProgress.ToEpicProgressDto();

        Assert.AreEqual(21.5d, dto.TotalStoryPoints, 0.001d);
        Assert.AreEqual(13.25d, dto.DoneStoryPoints, 0.001d);
        Assert.AreEqual(13.25d, dto.DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(61.63d, dto.AggregatedProgress!.Value, 0.001d);
        Assert.AreEqual(20.5d, dto.ForecastConsumedEffort!.Value, 0.001d);
        Assert.AreEqual(34.5d, dto.ForecastRemainingEffort!.Value, 0.001d);
        Assert.AreEqual(1, dto.ExcludedFeaturesCount);
        Assert.AreEqual(3, dto.IncludedFeaturesCount);
        Assert.AreEqual(21.5d, dto.TotalWeight, 0.001d);
    }

    [TestMethod]
    public void ToEpicProgressDto_PreservesNullProgressPercent()
    {
        var epicProgress = new EpicProgress(
            epicId: 42,
            epicTitle: "Epic A",
            productId: 5,
            progressPercent: null,
            totalScopeStoryPoints: 0,
            deliveredStoryPoints: 0,
            featureCount: 1,
            doneFeatureCount: 0,
            donePbiCount: 0,
            isDone: false,
            sprintDeliveredStoryPoints: 0,
            sprintProgressionDelta: new ProgressionDelta(0),
            sprintEffortDelta: 0,
            sprintCompletedPbiCount: 0,
            sprintCompletedFeatureCount: 0,
            aggregatedProgress: null,
            forecastConsumedEffort: null,
            forecastRemainingEffort: null,
            excludedFeaturesCount: 1,
            includedFeaturesCount: 0,
            totalWeight: 0);

        var dto = epicProgress.ToEpicProgressDto();

        Assert.IsNull(dto.ProgressPercent);
        Assert.IsNull(dto.AggregatedProgress);
    }
}
