using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PlanBoardWorkItemRulesTests
{
    [TestMethod]
    public void IsPlanBoardItem_MatchesPbiAliasesAndBug()
    {
        Assert.IsTrue(PlanBoardWorkItemRules.IsPlanBoardItem(WorkItemTypeHelper.Pbi));
        Assert.IsTrue(PlanBoardWorkItemRules.IsPlanBoardItem(" PBI "));
        Assert.IsTrue(PlanBoardWorkItemRules.IsPlanBoardItem(WorkItemTypeHelper.Bug));
        Assert.IsFalse(PlanBoardWorkItemRules.IsPlanBoardItem(WorkItemTypeHelper.Feature));
        Assert.IsFalse(PlanBoardWorkItemRules.IsPlanBoardItem(null));
    }

    [TestMethod]
    public void ResolveFeatureTitle_ReturnsParentTitleWhenPresent()
    {
        var feature = CreateWorkItem(10, WorkItemTypeHelper.Feature, "Shared Inbox");
        var bug = CreateWorkItem(20, WorkItemTypeHelper.Bug, "Message ordering breaks", parentId: 10);

        var title = PlanBoardWorkItemRules.ResolveFeatureTitle(
            bug,
            new Dictionary<int, WorkItemDto> { [feature.TfsId] = feature });

        Assert.AreEqual("Shared Inbox", title);
    }

    [TestMethod]
    public void GetTypeLabel_NormalizesPbiAndBugLabels()
    {
        Assert.AreEqual("PBI", PlanBoardWorkItemRules.GetTypeLabel(WorkItemTypeHelper.Pbi));
        Assert.AreEqual("PBI", PlanBoardWorkItemRules.GetTypeLabel(" pbi "));
        Assert.AreEqual(WorkItemTypeHelper.Bug, PlanBoardWorkItemRules.GetTypeLabel(WorkItemTypeHelper.Bug));
    }

    private static WorkItemDto CreateWorkItem(int tfsId, string type, string title, int? parentId = null)
    {
        return new WorkItemDto
        {
            TfsId = tfsId,
            Type = type,
            Title = title,
            ParentTfsId = parentId,
            AreaPath = "Project\\Team",
            IterationPath = "Project",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = DateTimeOffset.UtcNow
        };
    }
}
