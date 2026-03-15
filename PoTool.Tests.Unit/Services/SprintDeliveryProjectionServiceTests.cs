using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class SprintDeliveryProjectionServiceTests
{
    [TestMethod]
    public void Compute_PreservesDerivedStoryPointDiagnosticsAndUnestimatedDelivery()
    {
        var service = CreateService();
        var sprint = CreateSprint();
        var sprintStart = sprint.StartUtc!.Value;
        var sprintEnd = sprint.EndUtc!.Value;

        var result = service.Compute(new SprintDeliveryProjectionRequest(
            sprint,
            1,
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature, resolvedFeatureId: null),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.PbiShort, resolvedFeatureId: 100),
                CreateResolvedWorkItem(202, CanonicalWorkItemTypes.PbiShort, resolvedFeatureId: 100)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active"),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.PbiShort, "Estimated PBI", parentId: 100, state: "Done", effort: 50, storyPoints: 5),
                [202] = CreateWorkItem(202, CanonicalWorkItemTypes.PbiShort, "Derived PBI", parentId: 100, state: "Done")
            },
            new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
            {
                [201] = [CreateFieldChangeEvent(201, "System.State", sprintStart.AddDays(1), "Active", "Done")],
                [202] = [CreateFieldChangeEvent(202, "System.State", sprintStart.AddDays(2), "Active", "Done")]
            },
            sprintStart,
            sprintEnd));

        Assert.AreEqual(10d, result.PlannedStoryPoints, 0.001d);
        Assert.AreEqual(1, result.DerivedStoryPointCount);
        Assert.AreEqual(5d, result.DerivedStoryPoints, 0.001d);
        Assert.AreEqual(0, result.MissingStoryPointCount);
        Assert.AreEqual(5d, result.CompletedPbiStoryPoints, 0.001d);
        Assert.AreEqual(1, result.UnestimatedDeliveryCount);
        Assert.IsTrue(result.IsApproximate);
    }

    [TestMethod]
    public void Compute_UsesCommitmentAndSpilloverInputs()
    {
        var service = CreateService();
        var sprint = CreateSprint();
        var sprintStart = sprint.StartUtc!.Value;
        var sprintEnd = sprint.EndUtc!.Value;
        const string sprintPath = "\\Project\\Sprint 1";
        const string nextSprintPath = "\\Project\\Sprint 2";

        var result = service.Compute(new SprintDeliveryProjectionRequest(
            sprint,
            1,
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature, resolvedFeatureId: null),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.PbiShort, resolvedFeatureId: 100),
                CreateResolvedWorkItem(202, CanonicalWorkItemTypes.PbiShort, resolvedFeatureId: 100)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active", iterationPath: sprintPath),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.PbiShort, "Delivered PBI", parentId: 100, state: "Done", iterationPath: sprintPath, effort: 20, storyPoints: 3),
                [202] = CreateWorkItem(202, CanonicalWorkItemTypes.PbiShort, "Spillover PBI", parentId: 100, state: "Active", iterationPath: nextSprintPath, effort: 30, storyPoints: 5)
            },
            new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
            {
                [201] = [CreateFieldChangeEvent(201, "System.State", sprintStart.AddDays(1), "Active", "Done")],
                [202] = [CreateFieldChangeEvent(202, "System.IterationPath", sprintEnd.AddHours(1), sprintPath, nextSprintPath)]
            },
            sprintStart,
            sprintEnd,
            committedWorkItemIds: new HashSet<int> { 201, 202 },
            nextSprintPath: nextSprintPath,
            workItemSnapshotsById: new Dictionary<int, WorkItemSnapshot>
            {
                [100] = new(100, CanonicalWorkItemTypes.Feature, "Active", sprintPath),
                [201] = new(201, CanonicalWorkItemTypes.PbiShort, "Done", sprintPath),
                [202] = new(202, CanonicalWorkItemTypes.PbiShort, "Active", nextSprintPath)
            },
            stateEventsByWorkItem: new Dictionary<int, IReadOnlyList<FieldChangeEvent>>(),
            iterationEventsByWorkItem: new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
            {
                [202] = [CreateFieldChangeEvent(202, "System.IterationPath", sprintEnd.AddHours(1), sprintPath, nextSprintPath)]
            }));

        Assert.AreEqual(2, result.PlannedCount);
        Assert.AreEqual(50, result.PlannedEffort);
        Assert.AreEqual(8d, result.PlannedStoryPoints, 0.001d);
        Assert.AreEqual(1, result.SpilloverCount);
        Assert.AreEqual(30, result.SpilloverEffort);
        Assert.AreEqual(5d, result.SpilloverStoryPoints, 0.001d);
    }

    [TestMethod]
    public void ComputeProgressionDelta_AveragesOnlyFeaturesWithSprintProgress()
    {
        var service = CreateService();

        var result = service.ComputeProgressionDelta(new SprintDeliveryProgressionRequest(
            [
                CreateResolvedWorkItem(100, CanonicalWorkItemTypes.Feature, resolvedFeatureId: null),
                CreateResolvedWorkItem(101, CanonicalWorkItemTypes.Feature, resolvedFeatureId: null),
                CreateResolvedWorkItem(201, CanonicalWorkItemTypes.PbiShort, resolvedFeatureId: 100),
                CreateResolvedWorkItem(202, CanonicalWorkItemTypes.PbiShort, resolvedFeatureId: 100),
                CreateResolvedWorkItem(301, CanonicalWorkItemTypes.PbiShort, resolvedFeatureId: 101),
                CreateResolvedWorkItem(302, CanonicalWorkItemTypes.PbiShort, resolvedFeatureId: 101)
            ],
            new Dictionary<int, DeliveryTrendWorkItem>
            {
                [100] = CreateWorkItem(100, CanonicalWorkItemTypes.Feature, "Feature A", state: "Active"),
                [101] = CreateWorkItem(101, CanonicalWorkItemTypes.Feature, "Feature B", state: "Active"),
                [201] = CreateWorkItem(201, CanonicalWorkItemTypes.PbiShort, "Feature A Done", parentId: 100, state: "Done", storyPoints: 5),
                [202] = CreateWorkItem(202, CanonicalWorkItemTypes.PbiShort, "Feature A Active", parentId: 100, state: "Active", storyPoints: 5),
                [301] = CreateWorkItem(301, CanonicalWorkItemTypes.PbiShort, "Feature B Done", parentId: 101, state: "Done", storyPoints: 5),
                [302] = CreateWorkItem(302, CanonicalWorkItemTypes.PbiShort, "Feature B Active", parentId: 101, state: "Active", storyPoints: 5)
            },
            new Dictionary<int, IReadOnlyList<FieldChangeEvent>>
            {
                [201] = [CreateFieldChangeEvent(201, "System.State", DateTimeOffset.UtcNow, "Active", "Done")]
            }));

        Assert.AreEqual(50d, result.Percentage, 0.001d);
    }

    private static ISprintDeliveryProjectionService CreateService()
    {
        return new SprintDeliveryProjectionService(
            new CanonicalStoryPointResolutionService(),
            new HierarchyRollupService());
    }

    private static SprintDefinition CreateSprint()
    {
        return new SprintDefinition(
            1,
            1,
            "\\Project\\Sprint 1",
            "Sprint 1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 14, 0, 0, 0, TimeSpan.Zero));
    }

    private static DeliveryTrendResolvedWorkItem CreateResolvedWorkItem(
        int workItemId,
        string workItemType,
        int? resolvedFeatureId,
        int? resolvedProductId = 1,
        int? resolvedSprintId = 1)
    {
        return new DeliveryTrendResolvedWorkItem(
            workItemId,
            workItemType,
            resolvedProductId,
            resolvedFeatureId,
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
        DateTimeOffset? createdDate = null)
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
            createdDate);
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
