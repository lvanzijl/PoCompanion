using PoTool.Core.Planning;
using static PoTool.Core.Domain.Tests.ProductPlanningBoardTestFactory;

namespace PoTool.Api.Tests;

[TestClass]
public sealed class ProductPlanningBoardPersistenceTests
{
    [TestMethod]
    public async Task BuildPlanningBoardAsync_PersistedIntentWinsOverDifferingTfsDates()
    {
        var intentStore = new InMemoryProductPlanningIntentStore();
        var tfsClient = new RecordingTfsClient();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 6);
        intentStore.Seed(new ProductPlanningIntentRecord(
            7,
            3102,
            sprints[10][1].StartUtc!.Value.UtcDateTime.Date,
            2,
            null,
            DateTime.UtcNow));

        var service = CreateService(
            new InMemoryProductPlanningSessionStore(),
            intentStore,
            tfsClient,
            [CreateProduct(7, "Roadmap Product", [100], [10])],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
                    CreateWorkItem(3101, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
                    CreateWorkItem(
                        3102,
                        "Epic",
                        "Roadmap Epic 2",
                        100,
                        2d,
                        "roadmap",
                        startDate: sprints[10][3].StartUtc,
                        targetDate: sprints[10][4].EndUtc!.Value.AddDays(-1))
                ]
            },
            sprints);

        var board = await service.BuildPlanningBoardAsync(7);

        Assert.IsNotNull(board);
        Assert.AreEqual(1, board.EpicItems.Single(static epic => epic.EpicId == 3102).PlannedStartSprintIndex);
        Assert.IsEmpty(tfsClient.PlanningDateUpdates);
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_RecoveryWithNormalization_PersistsIntentAndRewritesTfsDates()
    {
        var intentStore = new InMemoryProductPlanningIntentStore();
        var tfsClient = new RecordingTfsClient();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 6);

        var service = CreateService(
            new InMemoryProductPlanningSessionStore(),
            intentStore,
            tfsClient,
            [CreateProduct(7, "Roadmap Product", [100], [10])],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
                    CreateWorkItem(
                        3201,
                        "Epic",
                        "Roadmap Epic 1",
                        100,
                        1d,
                        "roadmap",
                        startDate: sprints[10][0].StartUtc!.Value.AddDays(1),
                        targetDate: sprints[10][1].StartUtc!.Value.AddDays(2))
                ]
            },
            sprints);

        _ = await service.BuildPlanningBoardAsync(7);
        var persistedIntent = (await intentStore.GetByProductAsync(7)).Single();

        Assert.AreEqual(ProductPlanningRecoveryStatus.RecoveredWithNormalization, persistedIntent.RecoveryStatus);
        Assert.HasCount(1, tfsClient.PlanningDateUpdates);
        Assert.AreEqual(3201, tfsClient.PlanningDateUpdates[0].WorkItemId);
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_RemovesPersistedIntentForOutOfScopeEpic()
    {
        var intentStore = new InMemoryProductPlanningIntentStore();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 6);
        intentStore.Seed(
            new ProductPlanningIntentRecord(7, 3301, sprints[10][0].StartUtc!.Value.UtcDateTime.Date, 1, null, DateTime.UtcNow),
            new ProductPlanningIntentRecord(7, 3399, sprints[10][1].StartUtc!.Value.UtcDateTime.Date, 1, null, DateTime.UtcNow));

        var service = CreateService(
            new InMemoryProductPlanningSessionStore(),
            intentStore,
            new RecordingTfsClient(),
            [CreateProduct(7, "Roadmap Product", [100], [10])],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
                    CreateWorkItem(3301, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap")
                ]
            },
            sprints);

        _ = await service.BuildPlanningBoardAsync(7);

        CollectionAssert.AreEqual(new[] { 3301 }, (await intentStore.GetByProductAsync(7)).Select(static intent => intent.EpicId).ToArray());
    }

    [TestMethod]
    public async Task ResetPlanningBoardAsync_ReturnsPersistedBaseAfterMutation()
    {
        var sessionStore = new InMemoryProductPlanningSessionStore();
        var intentStore = new InMemoryProductPlanningIntentStore();
        var tfsClient = new RecordingTfsClient();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 8);

        var service = CreateService(
            sessionStore,
            intentStore,
            tfsClient,
            [CreateProduct(7, "Roadmap Product", [100], [10])],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
                    CreateWorkItem(3401, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
                    CreateWorkItem(3402, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap")
                ]
            },
            sprints);

        _ = await service.ExecuteMoveEpicBySprintsAsync(7, 3402, 2);
        var resetBoard = await service.ResetPlanningBoardAsync(7);

        Assert.IsNotNull(resetBoard);
        Assert.AreEqual(3, resetBoard.EpicItems.Single(static epic => epic.EpicId == 3402).ComputedStartSprintIndex);
        Assert.HasCount(2, await intentStore.GetByProductAsync(7));
    }
}
