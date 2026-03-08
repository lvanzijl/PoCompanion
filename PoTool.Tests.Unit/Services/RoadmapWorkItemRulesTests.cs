using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class RoadmapWorkItemRulesTests
{
    [TestMethod]
    public void IsEpic_UsesTrimmedCaseInsensitiveExactMatch()
    {
        Assert.IsTrue(RoadmapWorkItemRules.IsEpic(" Epic "));
        Assert.IsTrue(RoadmapWorkItemRules.IsEpic("epic"));
        Assert.IsFalse(RoadmapWorkItemRules.IsEpic("Epic Feature"));
        Assert.IsFalse(RoadmapWorkItemRules.IsEpic(" "));
        Assert.IsFalse(RoadmapWorkItemRules.IsEpic(null));
    }

    [TestMethod]
    public void HasRoadmapTag_HandlesSemicolonWhitespaceAndCase()
    {
        Assert.IsTrue(RoadmapWorkItemRules.HasRoadmapTag(" roadmap ; Alpha "));
        Assert.IsTrue(RoadmapWorkItemRules.HasRoadmapTag("Alpha;ROADMAP;Beta"));
        Assert.IsFalse(RoadmapWorkItemRules.HasRoadmapTag("Alpha; road map ; Beta"));
        Assert.IsFalse(RoadmapWorkItemRules.HasRoadmapTag(" "));
        Assert.IsFalse(RoadmapWorkItemRules.HasRoadmapTag(null));
    }

    [TestMethod]
    public void Analyze_ComputesDiscoveryStagesAndRootScope()
    {
        var report = RoadmapEpicDiscoveryAnalysis.Analyze(
            new[] { 10, 99 },
            new[]
            {
                CreateWorkItem(10, " Objective ", "Root Objective", parentId: null, tags: null, backlogPriority: 100),
                CreateWorkItem(20, " Epic ", "Roadmap Epic", parentId: 10, tags: " roadmap ; alpha ", backlogPriority: 200),
                CreateWorkItem(21, "Epic", "Available Epic", parentId: 10, tags: "alpha", backlogPriority: 300),
                CreateWorkItem(30, "Feature", "Roadmap Feature", parentId: 20, tags: "roadmap", backlogPriority: null),
            });

        Assert.HasCount(4, report.RawItems);
        Assert.HasCount(2, report.EpicLikeItems);
        Assert.HasCount(1, report.RoadmapEpicItems);
        Assert.HasCount(1, report.AvailableEpicItems);
        CollectionAssert.AreEqual(new[] { 10, 99 }, report.ConfiguredRootIds.ToArray());
        CollectionAssert.AreEqual(new[] { 10 }, report.PresentRootIds.ToArray());
        CollectionAssert.AreEqual(new[] { 99 }, report.MissingRootIds.ToArray());
        Assert.AreEqual(20, report.RoadmapEpicItems[0].TfsId);
        Assert.AreEqual(21, report.AvailableEpicItems[0].TfsId);
    }

    private static WorkItemDto CreateWorkItem(
        int tfsId,
        string type,
        string title,
        int? parentId,
        string? tags,
        double? backlogPriority)
    {
        return new WorkItemDto
        {
            TfsId = tfsId,
            Type = type,
            Title = title,
            ParentTfsId = parentId,
            Tags = tags,
            BacklogPriority = backlogPriority,
            AreaPath = "Area",
            IterationPath = "Iteration",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = DateTimeOffset.UtcNow,
        };
    }
}
