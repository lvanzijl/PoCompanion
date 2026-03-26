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
            RemainingStoryPoints = 9,
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
            TotalStoryPoints: 21,
            DoneStoryPoints: 8,
            RemainingStoryPoints: 13,
            EstimatedVelocity: 5,
            SprintsRemaining: 3,
            EstimatedCompletionDate: null,
            Confidence: ForecastConfidence.Medium,
            ForecastByDate: Array.Empty<SprintForecast>(),
            AreaPath: "Area",
            AnalysisTimestamp: DateTimeOffset.UnixEpoch);

        var json = JsonSerializer.Serialize(dto);

        StringAssert.Contains(json, "\"TotalStoryPoints\":21");
        StringAssert.Contains(json, "\"DoneStoryPoints\":8");
        StringAssert.Contains(json, "\"DeliveredStoryPoints\":8");
        StringAssert.Contains(json, "\"RemainingStoryPoints\":13");
        Assert.DoesNotContain(json, "CompletedEffort");
        Assert.DoesNotContain(json, "RemainingEffort");
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
            TotalStoryPoints = 10.5,
            DoneStoryPoints = 7.5
        };
        var epicProgress = new EpicProgressDto
        {
            EpicId = 11,
            EpicTitle = "Epic",
            ProductId = 2,
            ProgressPercent = 60,
            TotalStoryPoints = 20.5,
            DoneStoryPoints = 12.5
        };
        var featureDelivery = new FeatureDeliveryDto
        {
            FeatureId = 12,
            FeatureTitle = "Feature Delivery",
            ProductId = 2,
            ProductName = "Product",
            TotalStoryPoints = 9.5,
            SprintCompletedEffort = 4.5
        };

        var featureJson = JsonSerializer.Serialize(featureProgress);
        var epicJson = JsonSerializer.Serialize(epicProgress);
        var deliveryJson = JsonSerializer.Serialize(featureDelivery);

        StringAssert.Contains(featureJson, "\"TotalStoryPoints\":10.5");
        StringAssert.Contains(featureJson, "\"DoneStoryPoints\":7.5");
        StringAssert.Contains(featureJson, "\"DeliveredStoryPoints\":7.5");
        StringAssert.Contains(epicJson, "\"TotalStoryPoints\":20.5");
        StringAssert.Contains(epicJson, "\"DoneStoryPoints\":12.5");
        StringAssert.Contains(epicJson, "\"DeliveredStoryPoints\":12.5");
        StringAssert.Contains(deliveryJson, "\"TotalStoryPoints\":9.5");
        StringAssert.Contains(deliveryJson, "\"SprintCompletedEffort\":4.5");
        StringAssert.Contains(deliveryJson, "\"DeliveredStoryPoints\":4.5");
        Assert.DoesNotContain(featureJson, "DoneEffort");
        Assert.DoesNotContain(epicJson, "DoneEffort");
        Assert.DoesNotContain(deliveryJson, "TotalEffort");
    }

    [TestMethod]
    public void EpicProgressDto_SerializesNullProgressPercent_WhenEpicProgressIsUnknown()
    {
        var epicProgress = new EpicProgressDto
        {
            EpicId = 11,
            EpicTitle = "Epic",
            ProductId = 2,
            ProgressPercent = null,
            AggregatedProgress = null,
            TotalStoryPoints = 0,
            DoneStoryPoints = 0
        };

        var json = JsonSerializer.Serialize(epicProgress);

        StringAssert.Contains(json, "\"ProgressPercent\":null");
        StringAssert.Contains(json, "\"AggregatedProgress\":null");
    }

    [TestMethod]
    public void SprintForecast_SerializesCanonicalStoryPointAliases()
    {
        var dto = new SprintForecast(
            SprintName: "Sprint 1",
            IterationPath: "Project\\Sprint 1",
            SprintStartDate: DateTimeOffset.UnixEpoch,
            SprintEndDate: DateTimeOffset.UnixEpoch.AddDays(14),
            ExpectedCompletedStoryPoints: 5.5,
            RemainingStoryPointsAfterSprint: 7.25,
            ProgressPercentage: 43.1);

        var json = JsonSerializer.Serialize(dto);

        StringAssert.Contains(json, "\"ExpectedCompletedStoryPoints\":5.5");
        StringAssert.Contains(json, "\"RemainingStoryPointsAfterSprint\":7.25");
        Assert.DoesNotContain(json, "ExpectedCompletedEffort");
        Assert.DoesNotContain(json, "RemainingEffortAfterSprint");
    }
}
