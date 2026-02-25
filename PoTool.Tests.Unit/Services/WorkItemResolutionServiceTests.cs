using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemResolutionServiceTests
{
    [TestMethod]
    public void ResolveAncestry_PbiUnderFeatureUnderEpic_ResolvesCorrectly()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature", parentId: 100),
            [300] = CreateWorkItem(300, WorkItemType.Pbi, "PBI", parentId: 200),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(300, workItems);

        Assert.AreEqual(100, epicId, "EpicId should be 100");
        Assert.AreEqual(200, featureId, "FeatureId should be 200");
    }

    [TestMethod]
    public void ResolveAncestry_FeatureItself_ResolvesFeatureButNotEpic()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature", parentId: 100),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(200, workItems);

        Assert.AreEqual(100, epicId, "EpicId should be 100");
        Assert.AreEqual(200, featureId, "FeatureId should be the feature itself");
    }

    [TestMethod]
    public void ResolveAncestry_EpicItself_ResolvesEpicOnly()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic"),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(100, workItems);

        Assert.AreEqual(100, epicId, "EpicId should be the epic itself");
        Assert.IsNull(featureId, "FeatureId should be null for an Epic");
    }

    [TestMethod]
    public void ResolveAncestry_TaskUnderPbiUnderFeature_ResolvesFullChain()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Epic, "Epic"),
            [200] = CreateWorkItem(200, WorkItemType.Feature, "Feature", parentId: 100),
            [300] = CreateWorkItem(300, WorkItemType.Pbi, "PBI", parentId: 200),
            [400] = CreateWorkItem(400, WorkItemType.Task, "Task", parentId: 300),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(400, workItems);

        Assert.AreEqual(100, epicId, "Task should resolve to Epic 100");
        Assert.AreEqual(200, featureId, "Task should resolve to Feature 200");
    }

    [TestMethod]
    public void ResolveAncestry_OrphanItem_ResolvesNull()
    {
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [300] = CreateWorkItem(300, WorkItemType.Pbi, "Orphan PBI"),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(300, workItems);

        Assert.IsNull(epicId, "EpicId should be null for orphan");
        Assert.IsNull(featureId, "FeatureId should be null for orphan");
    }

    [TestMethod]
    public void ResolveAncestry_CircularParentChain_DoesNotInfiniteLoop()
    {
        // Create a circular reference: A -> B -> A
        var workItems = new Dictionary<int, WorkItemEntity>
        {
            [100] = CreateWorkItem(100, WorkItemType.Pbi, "PBI A", parentId: 200),
            [200] = CreateWorkItem(200, WorkItemType.Pbi, "PBI B", parentId: 100),
        };

        var (epicId, featureId) = WorkItemResolutionService.ResolveAncestry(100, workItems);

        // Should not crash; values don't matter, just that it terminates
        Assert.IsNull(epicId);
        Assert.IsNull(featureId);
    }

    private static WorkItemEntity CreateWorkItem(
        int tfsId, string type, string title,
        int? effort = null, string state = "New",
        int? parentId = null)
    {
        return new WorkItemEntity
        {
            TfsId = tfsId,
            Type = type,
            Title = title,
            Effort = effort,
            State = state,
            ParentTfsId = parentId,
            AreaPath = "\\Project",
            IterationPath = "\\Project\\Sprint 1",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
        };
    }
}
