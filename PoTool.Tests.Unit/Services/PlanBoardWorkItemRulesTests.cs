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
    public void IsFeature_ReturnsTrueForFeatureType()
    {
        Assert.IsTrue(PlanBoardWorkItemRules.IsFeature(WorkItemTypeHelper.Feature));
        Assert.IsTrue(PlanBoardWorkItemRules.IsFeature(" Feature "));
        Assert.IsFalse(PlanBoardWorkItemRules.IsFeature(WorkItemTypeHelper.Epic));
        Assert.IsFalse(PlanBoardWorkItemRules.IsFeature(WorkItemTypeHelper.Pbi));
        Assert.IsFalse(PlanBoardWorkItemRules.IsFeature(null));
    }

    [TestMethod]
    public void IsEpic_ReturnsTrueForEpicType()
    {
        Assert.IsTrue(PlanBoardWorkItemRules.IsEpic(WorkItemTypeHelper.Epic));
        Assert.IsTrue(PlanBoardWorkItemRules.IsEpic(" Epic "));
        Assert.IsFalse(PlanBoardWorkItemRules.IsEpic(WorkItemTypeHelper.Feature));
        Assert.IsFalse(PlanBoardWorkItemRules.IsEpic(WorkItemTypeHelper.Pbi));
        Assert.IsFalse(PlanBoardWorkItemRules.IsEpic(null));
    }

    [TestMethod]
    public void IsDoneOrRemoved_MatchesCaseInsensitively()
    {
        var states = new HashSet<(string, string)>
        {
            ("epic", "closed"),
            ("feature", "done"),
            ("product backlog item", "removed")
        };

        Assert.IsTrue(PlanBoardWorkItemRules.IsDoneOrRemoved("Epic", "Closed", states));
        Assert.IsTrue(PlanBoardWorkItemRules.IsDoneOrRemoved("FEATURE", "Done", states));
        Assert.IsTrue(PlanBoardWorkItemRules.IsDoneOrRemoved("Product Backlog Item", "Removed", states));
        Assert.IsFalse(PlanBoardWorkItemRules.IsDoneOrRemoved("Epic", "Active", states));
        Assert.IsFalse(PlanBoardWorkItemRules.IsDoneOrRemoved("Bug", "Closed", states));
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

    [TestMethod]
    public void CreateDescriptor_CopiesWorkItemTypeAndFeatureContext()
    {
        var feature = CreateWorkItem(10, WorkItemTypeHelper.Feature, "Shared Inbox");
        var pbi = CreateWorkItem(20, WorkItemTypeHelper.Pbi, "As a user I can sort messages", parentId: 10);
        pbi.Effort = 5;
        pbi.IterationPath = "Project\\Sprint 1";

        var descriptor = PlanBoardWorkItemRules.CreateDescriptor(
            pbi,
            new Dictionary<int, WorkItemDto> { [feature.TfsId] = feature });

        Assert.AreEqual(20, descriptor.TfsId);
        Assert.AreEqual("As a user I can sort messages", descriptor.Title);
        Assert.AreEqual(WorkItemTypeHelper.Pbi, descriptor.WorkItemType);
        Assert.AreEqual("Shared Inbox", descriptor.FeatureTitle);
        Assert.AreEqual(5, descriptor.Effort);
        Assert.AreEqual("Project\\Sprint 1", descriptor.IterationPath);
    }

    [TestMethod]
    public void BuildCandidateTree_ExcludesDoneAndRemovedLeaves()
    {
        var epic = CreateWorkItem(1, WorkItemTypeHelper.Epic, "Epic A");
        var feature = CreateWorkItem(2, WorkItemTypeHelper.Feature, "Feature 1", parentId: 1);
        var activePbi = CreateWorkItem(3, WorkItemTypeHelper.Pbi, "Active PBI", parentId: 2);
        var donePbi = CreateWorkItem(4, WorkItemTypeHelper.Pbi, "Done PBI", parentId: 2);
        donePbi.State = "Closed";
        var removedPbi = CreateWorkItem(5, WorkItemTypeHelper.Pbi, "Removed PBI", parentId: 2);
        removedPbi.State = "Removed";

        var doneOrRemoved = new HashSet<(string, string)>
        {
            ("product backlog item", "closed"),
            ("product backlog item", "removed")
        };

        var tree = PlanBoardWorkItemRules.BuildCandidateTree(
            [epic, feature, activePbi, donePbi, removedPbi],
            doneOrRemoved,
            new HashSet<string>());

        Assert.HasCount(1, tree); // 1 epic
        Assert.AreEqual(1, tree[0].TfsId);
        Assert.HasCount(1, tree[0].Children); // 1 feature
        Assert.AreEqual(2, tree[0].Children[0].TfsId);
        Assert.HasCount(1, tree[0].Children[0].Children); // 1 active leaf only
        Assert.AreEqual(3, tree[0].Children[0].Children[0].TfsId);
    }

    [TestMethod]
    public void BuildCandidateTree_ExcludesParentsWithNoEligibleDescendants()
    {
        var epic = CreateWorkItem(1, WorkItemTypeHelper.Epic, "Epic A");
        var feature = CreateWorkItem(2, WorkItemTypeHelper.Feature, "Feature 1", parentId: 1);
        var donePbi = CreateWorkItem(3, WorkItemTypeHelper.Pbi, "Done PBI", parentId: 2);
        donePbi.State = "Closed";

        var doneOrRemoved = new HashSet<(string, string)>
        {
            ("product backlog item", "closed")
        };

        var tree = PlanBoardWorkItemRules.BuildCandidateTree(
            [epic, feature, donePbi],
            doneOrRemoved,
            new HashSet<string>());

        // Feature has no eligible descendants → excluded; Epic has no features → excluded
        Assert.HasCount(0, tree);
    }

    [TestMethod]
    public void BuildCandidateTree_ExcludesAlreadyPlannedLeaves()
    {
        var epic = CreateWorkItem(1, WorkItemTypeHelper.Epic, "Epic A");
        var feature = CreateWorkItem(2, WorkItemTypeHelper.Feature, "Feature 1", parentId: 1);
        var unplannedPbi = CreateWorkItem(3, WorkItemTypeHelper.Pbi, "Unplanned PBI", parentId: 2);
        var plannedPbi = CreateWorkItem(4, WorkItemTypeHelper.Pbi, "Planned PBI", parentId: 2);
        plannedPbi.IterationPath = "Project\\Sprint 1";

        var sprintPaths = new HashSet<string> { "Project\\Sprint 1" };

        var tree = PlanBoardWorkItemRules.BuildCandidateTree(
            [epic, feature, unplannedPbi, plannedPbi],
            new HashSet<(string, string)>(),
            sprintPaths);

        Assert.HasCount(1, tree);
        var featureNode = tree[0].Children[0];
        Assert.HasCount(1, featureNode.Children);
        Assert.AreEqual(3, featureNode.Children[0].TfsId);
    }

    [TestMethod]
    public void BuildCandidateTree_AggregatesEffortOnFeatureAndEpic()
    {
        var epic = CreateWorkItem(1, WorkItemTypeHelper.Epic, "Epic A");
        var feature = CreateWorkItem(2, WorkItemTypeHelper.Feature, "Feature 1", parentId: 1);
        var pbi1 = CreateWorkItem(3, WorkItemTypeHelper.Pbi, "PBI 1", parentId: 2);
        pbi1.Effort = 3;
        var pbi2 = CreateWorkItem(4, WorkItemTypeHelper.Pbi, "PBI 2", parentId: 2);
        pbi2.Effort = 5;

        var tree = PlanBoardWorkItemRules.BuildCandidateTree(
            [epic, feature, pbi1, pbi2],
            new HashSet<(string, string)>(),
            new HashSet<string>());

        Assert.HasCount(1, tree);
        var epicNode = tree[0];
        Assert.AreEqual(8, epicNode.AggregatedEffort);
        var featureNode = epicNode.Children[0];
        Assert.AreEqual(8, featureNode.AggregatedEffort);
    }

    [TestMethod]
    public void BuildCandidateTree_OrdersByBacklogPriorityThenTfsId()
    {
        var epic = CreateWorkItem(1, WorkItemTypeHelper.Epic, "Epic A");
        var feature = CreateWorkItem(2, WorkItemTypeHelper.Feature, "Feature 1", parentId: 1);
        var pbiHigh = CreateWorkItem(10, WorkItemTypeHelper.Pbi, "High priority PBI", parentId: 2);
        pbiHigh.BacklogPriority = 1.0;
        var pbiMid = CreateWorkItem(11, WorkItemTypeHelper.Pbi, "Mid priority PBI", parentId: 2);
        pbiMid.BacklogPriority = 5.0;
        var pbiNoPriority = CreateWorkItem(12, WorkItemTypeHelper.Pbi, "No priority PBI", parentId: 2);
        // no BacklogPriority → sorted last

        var tree = PlanBoardWorkItemRules.BuildCandidateTree(
            [epic, feature, pbiNoPriority, pbiMid, pbiHigh],
            new HashSet<(string, string)>(),
            new HashSet<string>());

        var leaves = tree[0].Children[0].Children;
        Assert.HasCount(3, leaves);
        Assert.AreEqual(10, leaves[0].TfsId); // highest priority first
        Assert.AreEqual(11, leaves[1].TfsId);
        Assert.AreEqual(12, leaves[2].TfsId); // no priority last
    }

    [TestMethod]
    public void BuildCandidateTree_SetsEligiblePbiIdsOnParents()
    {
        var epic = CreateWorkItem(1, WorkItemTypeHelper.Epic, "Epic A");
        var feature = CreateWorkItem(2, WorkItemTypeHelper.Feature, "Feature 1", parentId: 1);
        var pbi1 = CreateWorkItem(3, WorkItemTypeHelper.Pbi, "PBI 1", parentId: 2);
        var pbi2 = CreateWorkItem(4, WorkItemTypeHelper.Bug, "Bug 1", parentId: 2);

        var tree = PlanBoardWorkItemRules.BuildCandidateTree(
            [epic, feature, pbi1, pbi2],
            new HashSet<(string, string)>(),
            new HashSet<string>());

        var epicNode = tree[0];
        CollectionAssert.AreEquivalent(new[] { 3, 4 }, epicNode.EligiblePbiIds);

        var featureNode = epicNode.Children[0];
        CollectionAssert.AreEquivalent(new[] { 3, 4 }, featureNode.EligiblePbiIds);
    }

    [TestMethod]
    public void BuildCandidateTree_OrphanPbisAppearsAtTopLevel()
    {
        // PBI with no Feature/Epic parent should appear at tree root level
        var orphanPbi = CreateWorkItem(1, WorkItemTypeHelper.Pbi, "Orphan PBI");

        var tree = PlanBoardWorkItemRules.BuildCandidateTree(
            [orphanPbi],
            new HashSet<(string, string)>(),
            new HashSet<string>());

        Assert.HasCount(1, tree);
        Assert.AreEqual(1, tree[0].TfsId);
        Assert.AreEqual(WorkItemTypeHelper.Pbi, tree[0].WorkItemType);
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
