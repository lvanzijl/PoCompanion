using PoTool.Api.Adapters;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Adapters;

[TestClass]
public sealed class DeliveryTrendProgressRollupMapperTests
{
    [TestMethod]
    public void ToFeatureProgressDto_MapsLegacyAndCanonicalStoryPointAliases()
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
            sprintCompletedInSprint: false);

        var dto = featureProgress.ToFeatureProgressDto(Array.Empty<CompletedPbiDto>());

        Assert.AreEqual(13.5d, dto.TotalEffort, 0.001d);
        Assert.AreEqual(13.5d, dto.TotalStoryPoints, 0.001d);
        Assert.AreEqual(8.25d, dto.DoneEffort, 0.001d);
        Assert.AreEqual(8.25d, dto.DoneStoryPoints, 0.001d);
        Assert.AreEqual(8.25d, dto.DeliveredStoryPoints, 0.001d);
    }

    [TestMethod]
    public void ToEpicProgressDto_MapsLegacyAndCanonicalStoryPointAliases()
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
            sprintCompletedFeatureCount: 1);

        var dto = epicProgress.ToEpicProgressDto();

        Assert.AreEqual(21.5d, dto.TotalEffort, 0.001d);
        Assert.AreEqual(21.5d, dto.TotalStoryPoints, 0.001d);
        Assert.AreEqual(13.25d, dto.DoneEffort, 0.001d);
        Assert.AreEqual(13.25d, dto.DoneStoryPoints, 0.001d);
        Assert.AreEqual(13.25d, dto.DeliveredStoryPoints, 0.001d);
    }
}
