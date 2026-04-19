using PoTool.Core.Planning;
using PoTool.Shared.Planning;
using PoTool.Shared.Settings;
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
        Assert.AreEqual(PlanningBoardIntentSource.Authored, board.EpicItems.Single(static epic => epic.EpicId == 3102).IntentSource);
        Assert.AreEqual(PlanningBoardDriftStatus.TfsProjectionMismatch, board.EpicItems.Single(static epic => epic.EpicId == 3102).DriftStatus);
        CollectionAssert.AreEqual(
            new[] { "StaleTfsProjection" },
            board.EpicItems.Single(static epic => epic.EpicId == 3102).Diagnostics!.Select(static diagnostic => diagnostic.Code).ToArray());
        Assert.IsEmpty(tfsClient.PlanningDateUpdates);
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_PersistedIntentMatchingTfsDates_SurfacesNoDrift()
    {
        var intentStore = new InMemoryProductPlanningIntentStore();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 6);
        intentStore.Seed(new ProductPlanningIntentRecord(
            7,
            3110,
            sprints[10][1].StartUtc!.Value.UtcDateTime.Date,
            2,
            null,
            DateTime.UtcNow));

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
                    CreateWorkItem(
                        3110,
                        "Epic",
                        "Roadmap Epic 1",
                        100,
                        1d,
                        "roadmap",
                        startDate: sprints[10][1].StartUtc,
                        targetDate: sprints[10][2].EndUtc!.Value.AddDays(-1))
                ]
            },
            sprints);

        var board = await service.BuildPlanningBoardAsync(7);

        Assert.IsNotNull(board);
        var epic = board.EpicItems.Single();
        Assert.AreEqual(PlanningBoardIntentSource.Authored, epic.IntentSource);
        Assert.AreEqual(PlanningBoardDriftStatus.NoDrift, epic.DriftStatus);
        Assert.AreEqual(0, epic.Diagnostics?.Count ?? 0);
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_PersistedIntentWithMissingTfsDates_SurfacesMissingDatesDrift()
    {
        var intentStore = new InMemoryProductPlanningIntentStore();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 6);
        intentStore.Seed(new ProductPlanningIntentRecord(
            7,
            3111,
            sprints[10][0].StartUtc!.Value.UtcDateTime.Date,
            1,
            null,
            DateTime.UtcNow));

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
                    CreateWorkItem(3111, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap")
                ]
            },
            sprints);

        var board = await service.BuildPlanningBoardAsync(7);

        Assert.IsNotNull(board);
        var epic = board.EpicItems.Single();
        Assert.AreEqual(PlanningBoardDriftStatus.MissingTfsDates, epic.DriftStatus);
        CollectionAssert.AreEqual(
            new[] { "MissingTfsDates" },
            epic.Diagnostics!.Select(static diagnostic => diagnostic.Code).ToArray());
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

        var board = await service.BuildPlanningBoardAsync(7);
        var persistedIntent = (await intentStore.GetByProductAsync(7)).Single();

        Assert.AreEqual(ProductPlanningRecoveryStatus.RecoveredWithNormalization, persistedIntent.RecoveryStatus);
        Assert.HasCount(1, tfsClient.PlanningDateUpdates);
        Assert.AreEqual(3201, tfsClient.PlanningDateUpdates[0].WorkItemId);
        Assert.IsNotNull(board);
        Assert.AreEqual(PlanningBoardIntentSource.Recovered, board.EpicItems.Single().IntentSource);
        Assert.AreEqual(ProductPlanningRecoveryStatus.RecoveredWithNormalization, board.EpicItems.Single().RecoveryStatus);
        CollectionAssert.AreEqual(
            new[] { "RecoveredWithNormalization" },
            board.EpicItems.Single().Diagnostics!.Select(static diagnostic => diagnostic.Code).ToArray());
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

    [TestMethod]
    public async Task BuildPlanningBoardAsync_LegacyInvalidDates_AreSurfacedAndIgnored()
    {
        var service = CreateService(
            new InMemoryProductPlanningSessionStore(),
            new InMemoryProductPlanningIntentStore(),
            new RecordingTfsClient(),
            [CreateProduct(7, "Roadmap Product", [100], [10])],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
                    CreateWorkItem(
                        3501,
                        "Epic",
                        "Roadmap Epic 1",
                        100,
                        1d,
                        "roadmap",
                        startDate: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                        targetDate: new DateTimeOffset(2026, 1, 16, 0, 0, 0, TimeSpan.Zero))
                ]
            },
            CreateDefaultSprintsByTeam([10], sprintCount: 6));

        var board = await service.BuildPlanningBoardAsync(7);

        Assert.IsNotNull(board);
        var epic = board.EpicItems.Single();
        Assert.AreEqual(PlanningBoardIntentSource.Bootstrap, epic.IntentSource);
        Assert.AreEqual(ProductPlanningRecoveryStatus.RecoveryFailed, epic.RecoveryStatus);
        Assert.AreEqual(PlanningBoardDriftStatus.LegacyInvalidTfsDates, epic.DriftStatus);
        CollectionAssert.AreEquivalent(
            new[] { "LegacyInvalidTfsDatesIgnored", "RecoveryFailed" },
            epic.Diagnostics!.Select(static diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_PersistedIntentBeyondCalendar_SurfacesCoverageDiagnostic()
    {
        var intentStore = new InMemoryProductPlanningIntentStore();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 2);
        intentStore.Seed(new ProductPlanningIntentRecord(
            7,
            3601,
            sprints[10][0].StartUtc!.Value.UtcDateTime.Date,
            4,
            null,
            DateTime.UtcNow));

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
                    CreateWorkItem(
                        3601,
                        "Epic",
                        "Roadmap Epic 1",
                        100,
                        1d,
                        "roadmap",
                        startDate: sprints[10][0].StartUtc,
                        targetDate: sprints[10][1].EndUtc!.Value.AddDays(-1))
                ]
            },
            sprints);

        var board = await service.BuildPlanningBoardAsync(7);

        Assert.IsNotNull(board);
        var epic = board.EpicItems.Single();
        Assert.AreEqual(PlanningBoardDriftStatus.InsufficientFutureSprintCoverage, epic.DriftStatus);
        CollectionAssert.AreEqual(
            new[] { "InsufficientFutureSprintCoverage" },
            epic.Diagnostics!.Select(static diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_AmbiguousCalendarWithoutPersistedIntent_SurfacesBlockingDiagnostic()
    {
        var service = CreateService(
            new InMemoryProductPlanningSessionStore(),
            new InMemoryProductPlanningIntentStore(),
            new RecordingTfsClient(),
            [CreateProduct(7, "Roadmap Product", [100], [10, 11])],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
                    CreateWorkItem(3701, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap")
                ]
            },
            new Dictionary<int, IReadOnlyList<SprintDto>>
            {
                [10] = CreateSequentialSprints(10, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 2),
                [11] =
                [
                    new SprintDto(
                        1,
                        11,
                        "team-11-sprint-1",
                        "Team 11\\Sprint 1",
                        "Sprint 1",
                        new DateTimeOffset(new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero),
                        new DateTimeOffset(new DateTime(2026, 1, 26, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero),
                        null,
                        DateTimeOffset.UtcNow)
                ]
            });

        var board = await service.BuildPlanningBoardAsync(7);

        Assert.IsNotNull(board);
        Assert.AreEqual("CalendarResolutionFailure", board.Diagnostics!.Single().Code);
        Assert.IsTrue(board.Diagnostics![0].IsBlocking);
    }
}
