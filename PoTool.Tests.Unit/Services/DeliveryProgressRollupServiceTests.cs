using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class DeliveryProgressRollupServiceTests
{
    [TestMethod]
    public void ComputeFeatureProgress_CalculatesFeatureRollupAndSprintProgressChange()
    {
        var service = CreateService();

        var result = service.ComputeFeatureProgress(new DeliveryFeatureProgressRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100),
                CreateResolvedWorkItem(202, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active"),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, "Done PBI", parentId: 100, state: "Done", storyPoints: 5),
                [202] = CreateWorkItem(202, CanonicalWorkItemTypes.ProductBacklogItem, "Active PBI", parentId: 100, state: "Active", storyPoints: 10)
            },
            [1],
            SprintCompletedPbiIds: new HashSet<int> { 201 }));

        Assert.HasCount(1, result);
        Assert.AreEqual(33, result[0].ProgressPercent);
        Assert.AreEqual(15d, result[0].TotalScopeStoryPoints, 0.001d);
        Assert.AreEqual(5d, result[0].DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(5d, result[0].SprintDeliveredStoryPoints, 0.001d);
        Assert.AreEqual(33.33d, result[0].SprintProgressionDelta.Percentage, 0.001d);
    }

    [TestMethod]
    public void ComputeFeatureProgress_KeepsSprintAssignedFeatureVisibleWithoutActivity()
    {
        var service = CreateService();

        var result = service.ComputeFeatureProgress(new DeliveryFeatureProgressRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100, resolvedSprintId: 7)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active"),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, "Assigned PBI", parentId: 100, state: "Active", storyPoints: 8)
            },
            [1],
            ActiveWorkItemIds: Array.Empty<int>(),
            SprintAssignedPbiIds: new HashSet<int> { 201 }));

        Assert.HasCount(1, result, "Sprint-assigned PBIs should keep the parent feature visible even without activity.");
        Assert.AreEqual(100, result[0].FeatureId);
    }

    [TestMethod]
    public void ComputeFeatureProgress_IncludesOverrideForFeatureWithoutPbis()
    {
        var service = CreateService();

        var result = service.ComputeFeatureProgress(new DeliveryFeatureProgressRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active", timeCriticality: 150)
            },
            [1]));

        Assert.HasCount(1, result);
        Assert.IsNull(result[0].CalculatedProgress);
        Assert.AreEqual(150d, result[0].Override!.Value, 0.001d);
        Assert.AreEqual(100d, result[0].EffectiveProgress!.Value, 0.001d);
        CollectionAssert.Contains(result[0].ValidationSignals.ToList(), FeatureProgressValidationSignals.OverrideOutOfRange);
    }

    [TestMethod]
    public void ComputeEpicProgress_AggregatesFeatureRollups()
    {
        var service = CreateService();

        var result = service.ComputeEpicProgress(new DeliveryEpicProgressRequest(
            [
                new FeatureProgress(200, "Feature A", 1, 100, "Epic X", 50, 40, 20, 2, false, 10, new ProgressionDelta(25), 4, 1, false),
                new FeatureProgress(201, "Feature B", 1, 100, "Epic X", 80, 60, 48, 3, false, 20, new ProgressionDelta(33.33), 6, 2, true)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, "Epic", "Epic X", state: "Active")
            }));

        Assert.HasCount(1, result);
        Assert.AreEqual(100, result[0].TotalScopeStoryPoints, 0.001d);
        Assert.AreEqual(68, result[0].DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(30d, result[0].SprintDeliveredStoryPoints, 0.001d);
        Assert.AreEqual(30d, result[0].SprintProgressionDelta.Percentage, 0.001d);
        Assert.AreEqual(10, result[0].SprintEffortDelta);
        Assert.AreEqual(1, result[0].SprintCompletedFeatureCount);
    }

    [TestMethod]
    public void ComputeProductSummaries_AggregatesEpicOutputsByProduct()
    {
        var result = DeliveryProgressSummaryCalculator.ComputeProductSummaries(
        [
            new EpicProgress(100, "Epic X", 1, 50, 40, 20, 2, 1, 2, false, 10, new ProgressionDelta(25), 4, 1, 1),
            new EpicProgress(101, "Epic Y", 1, 70, 60, 42, 3, 2, 4, false, 12, new ProgressionDelta(20), -1, 2, 0),
            new EpicProgress(200, "Epic Z", 2, 80, 50, 40, 2, 1, 3, false, 8, new ProgressionDelta(16), 6, 2, 2)
        ]);

        Assert.HasCount(2, result);
        Assert.IsTrue(result.ContainsKey(1));
        Assert.IsTrue(result.ContainsKey(2));
        Assert.AreEqual(3, result[1].ScopeChangeEffort);
        Assert.AreEqual(1, result[1].CompletedFeatureCount);
        Assert.AreEqual(6, result[2].ScopeChangeEffort);
        Assert.AreEqual(2, result[2].CompletedFeatureCount);
    }

    [TestMethod]
    public void ComputeProgressionDelta_ReturnsZero_WhenNoFeaturesExist()
    {
        var service = CreateService();

        var result = service.ComputeProgressionDelta(new SprintDeliveryProgressionRequest(
            Array.Empty<DeliveryTrendResolvedWorkItem>(),
            new Dictionary<int, DeliveryTrendWorkItem>(),
            new Dictionary<int, IReadOnlyList<FieldChangeEvent>>()));

        Assert.AreEqual(0d, result.Percentage, 0.001d);
    }

    [TestMethod]
    public void ComputeProgressionDelta_AveragesOnlyFeaturesWithStateProgress()
    {
        var service = CreateService();

        var result = service.ComputeProgressionDelta(new SprintDeliveryProgressionRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature),
                CreateResolvedWorkItem(101, CanonicalWorkItemTypes.Feature),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100),
                CreateResolvedWorkItem(202, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 100),
                CreateResolvedWorkItem(301, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 101),
                CreateResolvedWorkItem(302, CanonicalWorkItemTypes.ProductBacklogItem, resolvedFeatureId: 101)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active"),
                [101] = CreateWorkItem(101, CanonicalWorkItemTypes.Feature, "Feature B", state: "Active"),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.ProductBacklogItem, "Feature A Done", parentId: 100, state: "Done", storyPoints: 5),
                [202] = CreateWorkItem(202, CanonicalWorkItemTypes.ProductBacklogItem, "Feature A Active", parentId: 100, state: "Active", storyPoints: 5),
                [301] = CreateWorkItem(301, CanonicalWorkItemTypes.ProductBacklogItem, "Feature B Done", parentId: 101, state: "Done", storyPoints: 5),
                [302] = CreateWorkItem(302, CanonicalWorkItemTypes.ProductBacklogItem, "Feature B Active", parentId: 101, state: "Active", storyPoints: 5)
            },
            new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
            {
                [201] = [CreateFieldChangeEvent(201, "System.State", DateTimeOffset.UtcNow, "Active", "Done")]
            }));

        Assert.AreEqual(50d, result.Percentage, 0.001d);
    }

    private static IDeliveryProgressRollupService CreateService()
    {
        var storyPointResolutionService = new CanonicalStoryPointResolutionService();
        var hierarchyRollupService = new HierarchyRollupService(storyPointResolutionService);
        return new DeliveryProgressRollupService(storyPointResolutionService, hierarchyRollupService);
    }

    private static DeliveryTrendResolvedWorkItem CreateResolvedWorkItem(
        int workItemId,
        string workItemType,
        int? resolvedFeatureId = null,
        int? resolvedEpicId = null,
        int? resolvedProductId = 1,
        int? resolvedSprintId = 1)
    {
        return new DeliveryTrendResolvedWorkItem(
            workItemId,
            workItemType,
            resolvedProductId,
            resolvedFeatureId,
            resolvedEpicId,
            resolvedSprintId);
    }

    private static DeliveryTrendWorkItem CreateWorkItem(
        int workItemId,
        string workItemType,
        string title,
        int? parentId = null,
        string? state = "New",
        string? iterationPath = "\\Project\\Sprint 1",
        int? effort = null,
        int? storyPoints = null,
        int? businessValue = null,
        DateTimeOffset? createdDate = null,
        double? timeCriticality = null)
    {
        return new DeliveryTrendWorkItem(
            workItemId,
            workItemType,
            title,
            parentId,
            state,
            iterationPath,
            effort,
            storyPoints,
            businessValue,
            createdDate,
            timeCriticality);
    }

    private static FieldChangeEvent CreateFieldChangeEvent(
        int workItemId,
        string fieldRefName,
        DateTimeOffset timestamp,
        string? oldValue,
        string? newValue)
    {
        return new FieldChangeEvent(
            1,
            workItemId,
            1,
            fieldRefName,
            timestamp,
            timestamp.UtcDateTime,
            oldValue,
            newValue);
    }
}
