using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.Models;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class DomainWorkItemFieldSemanticsTests
{
    [TestMethod]
    public void CanonicalWorkItem_EpicPreservesProjectFields()
    {
        var workItem = new CanonicalWorkItem(
            1,
            WorkItemType.Epic,
            null,
            null,
            null,
            timeCriticality: 55d,
            projectNumber: "PRJ-1",
            projectElement: "ELM-1");

        Assert.AreEqual("PRJ-1", workItem.ProjectNumber);
        Assert.AreEqual("ELM-1", workItem.ProjectElement);
        Assert.IsNull(workItem.TimeCriticality);
    }

    [TestMethod]
    public void CanonicalWorkItem_FeatureUsesTimeCriticalityAndIgnoresProjectFields()
    {
        var workItem = new CanonicalWorkItem(
            2,
            WorkItemType.Feature,
            null,
            null,
            null,
            timeCriticality: 72.5d,
            projectNumber: "PRJ-2",
            projectElement: "ELM-2");

        Assert.AreEqual(72.5d, workItem.TimeCriticality!.Value, 0.001d);
        Assert.IsNull(workItem.ProjectNumber);
        Assert.IsNull(workItem.ProjectElement);
    }

    [TestMethod]
    public void DeliveryTrendWorkItem_EpicIgnoresTimeCriticality()
    {
        var workItem = new DeliveryTrendWorkItem(
            3,
            WorkItemType.Epic,
            "Epic",
            null,
            "Active",
            "Sprint 1",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            timeCriticality: 80d,
            projectNumber: "PRJ-3",
            projectElement: "ELM-3");

        Assert.IsNull(workItem.TimeCriticality);
        Assert.AreEqual("PRJ-3", workItem.ProjectNumber);
        Assert.AreEqual("ELM-3", workItem.ProjectElement);
    }

    [TestMethod]
    public void DeliveryTrendWorkItem_FeatureIgnoresProjectFieldsAndPreservesTimeCriticality()
    {
        var workItem = new DeliveryTrendWorkItem(
            4,
            WorkItemType.Feature,
            "Feature",
            null,
            "Active",
            "Sprint 1",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            timeCriticality: 65d,
            projectNumber: "PRJ-4",
            projectElement: "ELM-4");

        Assert.AreEqual(65d, workItem.TimeCriticality!.Value, 0.001d);
        Assert.IsNull(workItem.ProjectNumber);
        Assert.IsNull(workItem.ProjectElement);
    }
}
