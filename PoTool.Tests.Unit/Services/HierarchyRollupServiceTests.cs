using PoTool.Core.Domain.Models;
using PoTool.Core.Metrics.Services;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class HierarchyRollupServiceTests
{
    private readonly IHierarchyRollupService _service = new HierarchyRollupService(new CanonicalStoryPointResolutionService());

    [TestMethod]
    public void RollupCanonicalScope_FeatureScope_UsesDirectPbiEstimates()
    {
        var feature = CreateWorkItem(100, WorkItemType.Feature, "Active", parentTfsId: null, effort: null);
        var donePbi = CreateWorkItem(201, WorkItemType.Pbi, "Done", 100, 5);
        var activePbi = CreateWorkItem(202, WorkItemType.Pbi, "Active", 100, 8);
        var workItems = new[] { feature, donePbi, activePbi };

        var result = _service.RollupCanonicalScope(feature.WorkItem, workItems.Select(item => item.WorkItem).ToList(), BuildDoneLookup(workItems));

        Assert.AreEqual(13d, result.Total, 0.001d);
        Assert.AreEqual(5d, result.Completed, 0.001d);
    }

    [TestMethod]
    public void RollupCanonicalScope_EpicScope_RollsUpNestedFeatureChildren()
    {
        var epic = CreateWorkItem(1, WorkItemType.Epic, "Active", parentTfsId: null, effort: null);
        var featureA = CreateWorkItem(10, WorkItemType.Feature, "Active", 1, null);
        var featureB = CreateWorkItem(11, WorkItemType.Feature, "Done", 1, null);
        var pbiA = CreateWorkItem(101, WorkItemType.Pbi, "Done", 10, 5);
        var pbiB = CreateWorkItem(102, WorkItemType.Pbi, "Active", 10, 3);
        var pbiC = CreateWorkItem(103, WorkItemType.Pbi, "Done", 11, 8);
        var workItems = new[] { epic, featureA, featureB, pbiA, pbiB, pbiC };

        var result = _service.RollupCanonicalScope(epic.WorkItem, workItems.Select(item => item.WorkItem).ToList(), BuildDoneLookup(workItems));

        Assert.AreEqual(16d, result.Total, 0.001d);
        Assert.AreEqual(13d, result.Completed, 0.001d);
    }

    [TestMethod]
    public void RollupCanonicalScope_ParentFallback_OnlyAppliesWhenChildPbisLackEstimates()
    {
        var feature = CreateWorkItem(100, WorkItemType.Feature, "Done", parentTfsId: null, effort: null, storyPoints: null, businessValue: 8);
        var missingPbi = CreateWorkItem(201, WorkItemType.Pbi, "Done", 100, null, storyPoints: null, businessValue: null);
        var workItems = new[] { feature, missingPbi };

        var result = _service.RollupCanonicalScope(feature.WorkItem, workItems.Select(item => item.WorkItem).ToList(), BuildDoneLookup(workItems));

        Assert.AreEqual(8d, result.Total, 0.001d);
        Assert.AreEqual(8d, result.Completed, 0.001d);
    }

    [TestMethod]
    public void RollupCanonicalScope_ExcludesBugAndTaskStoryPoints()
    {
        var feature = CreateWorkItem(100, WorkItemType.Feature, "Active", parentTfsId: null, effort: null);
        var pbi = CreateWorkItem(201, WorkItemType.Pbi, "Done", 100, 5);
        var bug = CreateWorkItem(202, WorkItemType.Bug, "Done", 100, 13);
        var task = CreateWorkItem(203, WorkItemType.Task, "Done", 100, 8);
        var workItems = new[] { feature, pbi, bug, task };

        var result = _service.RollupCanonicalScope(feature.WorkItem, workItems.Select(item => item.WorkItem).ToList(), BuildDoneLookup(workItems));

        Assert.AreEqual(5d, result.Total, 0.001d);
        Assert.AreEqual(5d, result.Completed, 0.001d);
    }

    [TestMethod]
    public void RollupCanonicalScope_UsesFractionalDerivedEstimates()
    {
        var feature = CreateWorkItem(100, WorkItemType.Feature, "Active", parentTfsId: null, effort: null);
        var estimatedPbiA = CreateWorkItem(201, WorkItemType.Pbi, "Done", 100, 3);
        var estimatedPbiB = CreateWorkItem(202, WorkItemType.Pbi, "Active", 100, 4);
        var missingPbi = CreateWorkItem(203, WorkItemType.Pbi, "Active", 100, null, storyPoints: null, businessValue: null);
        var workItems = new[] { feature, estimatedPbiA, estimatedPbiB, missingPbi };

        var result = _service.RollupCanonicalScope(feature.WorkItem, workItems.Select(item => item.WorkItem).ToList(), BuildDoneLookup(workItems));

        Assert.AreEqual(10.5d, result.Total, 0.001d);
        Assert.AreEqual(3d, result.Completed, 0.001d);
    }

    private static Dictionary<int, bool> BuildDoneLookup(IEnumerable<TestWorkItem> workItems)
    {
        return workItems.ToDictionary(
            workItem => workItem.WorkItem.WorkItemId,
            workItem => workItem.IsDone);
    }

    private static TestWorkItem CreateWorkItem(
        int id,
        string type,
        string state,
        int? parentTfsId,
        int? effort,
        int? storyPoints = null,
        int? businessValue = null)
    {
        return new TestWorkItem(
            new CanonicalWorkItem(
                id,
                type,
                parentTfsId,
                businessValue,
                storyPoints ?? effort),
            string.Equals(state, "Done", StringComparison.OrdinalIgnoreCase));
    }

    private readonly record struct TestWorkItem(CanonicalWorkItem WorkItem, bool IsDone);
}
