using System.Text.Json;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class DtoContractCleanupTests
{
    [TestMethod]
    public void SprintExecutionSummaryDto_SerializesCanonicalStoryPointAliases()
    {
        var dto = new SprintExecutionSummaryDto
        {
            CommittedSP = 15,
            AddedSP = 5,
            RemovedSP = 2,
            DeliveredSP = 9,
            SpilloverSP = 4
        };

        var json = JsonSerializer.Serialize(dto);

        StringAssert.Contains(json, "\"CommittedSP\":15");
        StringAssert.Contains(json, "\"CommittedStoryPoints\":15");
        StringAssert.Contains(json, "\"AddedStoryPoints\":5");
        StringAssert.Contains(json, "\"DeliveredStoryPoints\":9");
        StringAssert.Contains(json, "\"RemainingStoryPoints\":9");
        StringAssert.Contains(json, "\"SpilloverStoryPoints\":4");
    }

    [TestMethod]
    public void EpicCompletionForecastDto_SerializesCanonicalForecastAliases()
    {
        var dto = new EpicCompletionForecastDto(
            EpicId: 42,
            Title: "Epic",
            Type: "Epic",
            TotalEffort: 21,
            CompletedEffort: 8,
            RemainingEffort: 13,
            EstimatedVelocity: 5,
            SprintsRemaining: 3,
            EstimatedCompletionDate: null,
            Confidence: ForecastConfidence.Medium,
            ForecastByDate: Array.Empty<SprintForecast>(),
            AreaPath: "Area",
            AnalysisTimestamp: DateTimeOffset.UnixEpoch);

        var json = JsonSerializer.Serialize(dto);

        StringAssert.Contains(json, "\"CompletedEffort\":8");
        StringAssert.Contains(json, "\"DeliveredStoryPoints\":8");
        StringAssert.Contains(json, "\"RemainingEffort\":13");
        StringAssert.Contains(json, "\"RemainingStoryPoints\":13");
    }

    [TestMethod]
    public void DeliveryProgressDtos_SerializeCanonicalDeliveredStoryPointAliases()
    {
        var featureProgress = new FeatureProgressDto
        {
            FeatureId = 10,
            FeatureTitle = "Feature",
            ProductId = 2,
            ProgressPercent = 50,
            DoneEffort = 7.5
        };
        var epicProgress = new EpicProgressDto
        {
            EpicId = 11,
            EpicTitle = "Epic",
            ProductId = 2,
            ProgressPercent = 60,
            DoneEffort = 12.5
        };
        var featureDelivery = new FeatureDeliveryDto
        {
            FeatureId = 12,
            FeatureTitle = "Feature Delivery",
            ProductId = 2,
            ProductName = "Product",
            SprintCompletedEffort = 4.5
        };

        var featureJson = JsonSerializer.Serialize(featureProgress);
        var epicJson = JsonSerializer.Serialize(epicProgress);
        var deliveryJson = JsonSerializer.Serialize(featureDelivery);

        StringAssert.Contains(featureJson, "\"DoneEffort\":7.5");
        StringAssert.Contains(featureJson, "\"DeliveredStoryPoints\":7.5");
        StringAssert.Contains(epicJson, "\"DoneEffort\":12.5");
        StringAssert.Contains(epicJson, "\"DeliveredStoryPoints\":12.5");
        StringAssert.Contains(deliveryJson, "\"SprintCompletedEffort\":4.5");
        StringAssert.Contains(deliveryJson, "\"DeliveredStoryPoints\":4.5");
    }
}
