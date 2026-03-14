using PoTool.Api.Adapters;
using PoTool.Api.Services;
using PoTool.Core.Domain.Models;
using SharedStateClassification = PoTool.Shared.Settings.StateClassification;
using WorkItemStateClassificationDto = PoTool.Shared.Settings.WorkItemStateClassificationDto;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class StateClassificationInputMapperTests
{
    [TestMethod]
    public void ToDomainStateClassification_MapsTransportClassificationIntoDomainInput()
    {
        var dto = new WorkItemStateClassificationDto
        {
            WorkItemType = "Product Backlog Item",
            StateName = "Resolved",
            Classification = SharedStateClassification.Done
        };

        var classification = dto.ToDomainStateClassification();

        Assert.AreEqual("Product Backlog Item", classification.WorkItemType);
        Assert.AreEqual("Resolved", classification.StateName);
        Assert.AreEqual(StateClassification.Done, classification.Classification);
    }

    [TestMethod]
    public void ToDto_MapsDomainDefaultClassificationBackToTransportContract()
    {
        var classification = new WorkItemStateClassification(
            "Feature",
            "Active",
            StateClassification.InProgress);

        var dto = classification.ToDto();

        Assert.AreEqual("Feature", dto.WorkItemType);
        Assert.AreEqual("Active", dto.StateName);
        Assert.AreEqual(SharedStateClassification.InProgress, dto.Classification);
    }
}
