using PoTool.Api.Adapters;
using PoTool.Core.Domain.WorkItems;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Adapters;

[TestClass]
public sealed class StateClassificationInputMapperTests
{
    [TestMethod]
    public void ToCanonicalDomainStateClassifications_CanonicalizesPbiLikeTypes()
    {
        var classifications = new[]
        {
            new WorkItemStateClassificationDto
            {
                WorkItemType = PoTool.Core.WorkItems.WorkItemType.Pbi,
                StateName = "Done",
                Classification = StateClassification.Done
            },
            new WorkItemStateClassificationDto
            {
                WorkItemType = PoTool.Core.WorkItems.WorkItemType.UserStory,
                StateName = "Done",
                Classification = StateClassification.Done
            }
        };

        var result = classifications.ToCanonicalDomainStateClassifications();

        Assert.IsTrue(result.All(classification => classification.WorkItemType == CanonicalWorkItemTypes.Pbi));
    }
}
