using PoTool.Core.Planning;
using PoTool.Shared.Planning;
using PoTool.Shared.Settings;
using static PoTool.Core.Domain.Tests.ProductPlanningBoardTestFactory;

namespace PoTool.Core.Domain.Tests;

[TestClass]
public sealed class ProductPlanningBoardServiceTests
{
    [TestMethod]
    public async Task BuildPlanningBoardAsync_FirstAccessBootstrapsAndStoresState()
    {
        var sessionStore = new InMemoryProductPlanningSessionStore();
        var service = CreateService(
            sessionStore,
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(502, "Epic", "Roadmap Epic B", 100, 10d, "roadmap"),
            CreateWorkItem(501, "Epic", "Roadmap Epic A", 100, 20d, "roadmap"),
            CreateWorkItem(503, "Epic", "Non-roadmap Epic", 100, 5d, "not-roadmap"));

        var board = await service.BuildPlanningBoardAsync(7);

        Assert.IsNotNull(board);
        Assert.IsTrue(sessionStore.TryGetState(7, out var storedState));
        Assert.AreEqual(7, board.ProductId);
        Assert.AreEqual("Roadmap Product", board.ProductName);
        Assert.HasCount(1, board.Tracks);
        Assert.IsTrue(board.Tracks[0].IsMainLane);
        CollectionAssert.AreEqual(new[] { 502, 501 }, board.EpicItems.Select(static epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 0 }, board.EpicItems.Select(static epic => epic.TrackIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 1 }, board.EpicItems.Select(static epic => epic.DurationInSprints).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, board.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 502, 501 }, storedState.Epics.Select(static epic => epic.EpicId).ToArray());
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_MapsRoadmapOrderAndDerivedEnds()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(601, "Epic", "First Roadmap Epic", 100, 3d, "roadmap"),
            CreateWorkItem(602, "Epic", "Second Roadmap Epic", 100, 8d, "roadmap"));

        var board = await service.GetPlanningBoardAsync(7);

        Assert.IsNotNull(board);
        CollectionAssert.AreEqual(new[] { 601, 602 }, board.EpicItems.Select(static epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2 }, board.EpicItems.Select(static epic => epic.RoadmapOrder).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, board.EpicItems.Select(static epic => epic.PlannedStartSprintIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2 }, board.EpicItems.Select(static epic => epic.EndSprintIndexExclusive).ToArray());
        CollectionAssert.AreEqual(new[] { 601, 602 }, board.Tracks[0].EpicIds.ToArray());
    }

    [TestMethod]
    public async Task ExecuteMoveAndAdjustSpacingBeforeAsync_PropagateChangedAffectedAndValidationWithinSession()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(701, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(702, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(703, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        var moveResult = await service.ExecuteMoveEpicBySprintsAsync(7, 702, 2);
        var spacingResult = await service.ExecuteAdjustSpacingBeforeAsync(7, 703, 1);
        var invalidShiftResult = await service.ExecuteShiftPlanAsync(7, 702, 0);

        Assert.IsNotNull(moveResult);
        Assert.AreEqual(3, moveResult.EpicItems.Single(static epic => epic.EpicId == 702).ComputedStartSprintIndex);
        CollectionAssert.AreEquivalent(new[] { 702, 703 }, moveResult.ChangedEpicIds.ToArray());
        CollectionAssert.AreEqual(new[] { 702, 703 }, moveResult.AffectedEpicIds.ToArray());
        Assert.IsTrue(moveResult.EpicItems.Single(static epic => epic.EpicId == 702).IsChanged);
        Assert.IsTrue(moveResult.EpicItems.Single(static epic => epic.EpicId == 703).IsAffected);

        Assert.IsNotNull(spacingResult);
        Assert.AreEqual(4, spacingResult.EpicItems.Single(static epic => epic.EpicId == 703).ComputedStartSprintIndex);

        Assert.IsNotNull(invalidShiftResult);
        Assert.HasCount(1, invalidShiftResult.Issues);
        Assert.AreEqual("InvalidOperationInput", invalidShiftResult.Issues[0].Code);
        Assert.IsEmpty(invalidShiftResult.ChangedEpicIds);
        Assert.IsEmpty(invalidShiftResult.AffectedEpicIds);
    }

    [TestMethod]
    public async Task ExecuteOperations_MaintainSessionContinuityAcrossMultipleOperations()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(801, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(802, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(803, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        var movedBoard = await service.ExecuteMoveEpicBySprintsAsync(7, 802, 2);
        var parallelBoard = await service.ExecuteRunInParallelAsync(7, 803);
        var persistedBoard = await service.GetPlanningBoardAsync(7);

        Assert.IsNotNull(movedBoard);
        Assert.AreEqual(3, movedBoard.EpicItems.Single(static epic => epic.EpicId == 802).ComputedStartSprintIndex);

        Assert.IsNotNull(parallelBoard);
        Assert.AreEqual(1, parallelBoard.EpicItems.Single(static epic => epic.EpicId == 803).TrackIndex);
        Assert.AreEqual(3, parallelBoard.EpicItems.Single(static epic => epic.EpicId == 803).ComputedStartSprintIndex);
        Assert.AreEqual(3, parallelBoard.EpicItems.Single(static epic => epic.EpicId == 802).ComputedStartSprintIndex);

        Assert.IsNotNull(persistedBoard);
        Assert.AreEqual(1, persistedBoard.EpicItems.Single(static epic => epic.EpicId == 803).TrackIndex);
        Assert.AreEqual(3, persistedBoard.EpicItems.Single(static epic => epic.EpicId == 802).ComputedStartSprintIndex);
        CollectionAssert.AreEqual(new[] { 803 }, persistedBoard.Tracks.Single(static track => track.TrackIndex == 1).EpicIds.ToArray());
    }

    [TestMethod]
    public async Task ExecuteRunInParallelAndReturnToMainAsync_SurfaceTrackChangesInReadModel()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(901, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(902, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(903, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        var parallelResult = await service.ExecuteRunInParallelAsync(7, 903);
        var returnToMainResult = await service.ExecuteReturnToMainAsync(7, 903);

        Assert.IsNotNull(parallelResult);
        Assert.AreEqual(1, parallelResult.EpicItems.Single(static epic => epic.EpicId == 903).TrackIndex);
        Assert.HasCount(2, parallelResult.Tracks);

        Assert.IsNotNull(returnToMainResult);
        Assert.AreEqual(0, returnToMainResult.EpicItems.Single(static epic => epic.EpicId == 903).TrackIndex);
        CollectionAssert.AreEqual(new[] { 903 }, returnToMainResult.ChangedEpicIds.ToArray());
        CollectionAssert.AreEqual(new[] { 903 }, returnToMainResult.AffectedEpicIds.ToArray());
    }

    [TestMethod]
    public async Task ExecuteReorderAndShiftPlanAsync_ReturnUpdatedRoadmapOrderAndSuffixShape()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(1001, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(1002, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(1003, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        var reorderResult = await service.ExecuteReorderEpicAsync(7, 1003, 2);
        var shiftResult = await service.ExecuteShiftPlanAsync(7, 1002, 3);

        Assert.IsNotNull(reorderResult);
        CollectionAssert.AreEqual(
            new[] { 1001, 1003, 1002 },
            reorderResult.EpicItems.Select(static epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(
            new[] { 1, 2, 3 },
            reorderResult.EpicItems.Select(static epic => epic.RoadmapOrder).ToArray());

        Assert.IsNotNull(shiftResult);
        CollectionAssert.AreEqual(new[] { 0, 2, 4 }, shiftResult.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
        Assert.AreEqual(4, shiftResult.EpicItems.Single(static epic => epic.EpicId == 1002).PlannedStartSprintIndex);
        CollectionAssert.Contains(shiftResult.AffectedEpicIds.ToArray(), 1002);
    }

    [TestMethod]
    public async Task ResetPlanningBoardAsync_DiscardsPriorSessionStateAndRebootstraps()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(1101, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(1102, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(1103, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        await service.ExecuteMoveEpicBySprintsAsync(7, 1102, 2);
        var resetBoard = await service.ResetPlanningBoardAsync(7);
        var nextRead = await service.GetPlanningBoardAsync(7);

        Assert.IsNotNull(resetBoard);
        CollectionAssert.AreEqual(new[] { 0, 3, 4 }, resetBoard.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
        Assert.IsEmpty(resetBoard.ChangedEpicIds);
        Assert.IsEmpty(resetBoard.AffectedEpicIds);

        Assert.IsNotNull(nextRead);
        CollectionAssert.AreEqual(
            resetBoard.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray(),
            nextRead.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_PersistedIntentWinsOverDifferingTfsDates()
    {
        var sessionStore = new InMemoryProductPlanningSessionStore();
        var intentStore = new InMemoryProductPlanningIntentStore();
        var tfsClient = new RecordingTfsClient();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 6);
        var product = CreateProduct(7, "Roadmap Product", [100], [10]);
        intentStore.Seed(new ProductPlanningIntentRecord(
            7,
            2102,
            sprints[10][1].StartUtc!.Value.UtcDateTime.Date,
            2,
            null,
            DateTime.UtcNow));

        var service = CreateService(
            sessionStore,
            intentStore,
            tfsClient,
            [product],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
                    CreateWorkItem(2101, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
                    CreateWorkItem(
                        2102,
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
        var resetBoard = await service.ResetPlanningBoardAsync(7);

        Assert.IsNotNull(board);
        Assert.AreEqual(1, board.EpicItems.Single(static epic => epic.EpicId == 2102).PlannedStartSprintIndex);
        Assert.IsNotNull(resetBoard);
        Assert.AreEqual(1, resetBoard.EpicItems.Single(static epic => epic.EpicId == 2102).PlannedStartSprintIndex);
        Assert.IsEmpty(tfsClient.PlanningDateUpdates);
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_RecoversExactIntentFromTfsDates_AndPersistsIt()
    {
        var sessionStore = new InMemoryProductPlanningSessionStore();
        var intentStore = new InMemoryProductPlanningIntentStore();
        var tfsClient = new RecordingTfsClient();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 6);

        var service = CreateService(
            sessionStore,
            intentStore,
            tfsClient,
            [
                CreateProduct(7, "Roadmap Product", [100], [10])
            ],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
                    CreateWorkItem(
                        2201,
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
        var persistedIntent = (await intentStore.GetByProductAsync(7)).Single();

        Assert.IsNotNull(board);
        Assert.AreEqual(0, board.EpicItems.Single().PlannedStartSprintIndex);
        Assert.AreEqual(2, persistedIntent.DurationInSprints);
        Assert.AreEqual(ProductPlanningRecoveryStatus.RecoveredExact, persistedIntent.RecoveryStatus);
        Assert.IsEmpty(tfsClient.PlanningDateUpdates);
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_RecoversWithNormalization_AndRewritesTfsDates()
    {
        var sessionStore = new InMemoryProductPlanningSessionStore();
        var intentStore = new InMemoryProductPlanningIntentStore();
        var tfsClient = new RecordingTfsClient();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 6);

        var service = CreateService(
            sessionStore,
            intentStore,
            tfsClient,
            [
                CreateProduct(7, "Roadmap Product", [100], [10])
            ],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
                    CreateWorkItem(
                        2301,
                        "Epic",
                        "Roadmap Epic 1",
                        100,
                        1d,
                        "roadmap",
                        startDate: sprints[10][0].StartUtc!.Value.AddDays(2),
                        targetDate: sprints[10][1].StartUtc!.Value.AddDays(3))
                ]
            },
            sprints);

        _ = await service.BuildPlanningBoardAsync(7);
        var persistedIntent = (await intentStore.GetByProductAsync(7)).Single();

        Assert.AreEqual(ProductPlanningRecoveryStatus.RecoveredWithNormalization, persistedIntent.RecoveryStatus);
        Assert.HasCount(1, tfsClient.PlanningDateUpdates);
        Assert.AreEqual(2301, tfsClient.PlanningDateUpdates[0].WorkItemId);
        Assert.AreEqual(DateOnly.FromDateTime(sprints[10][0].StartUtc!.Value.UtcDateTime), tfsClient.PlanningDateUpdates[0].StartDate);
        Assert.AreEqual(DateOnly.FromDateTime(sprints[10][1].EndUtc!.Value.UtcDateTime.AddDays(-1)), tfsClient.PlanningDateUpdates[0].TargetDate);
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_RecoveryFailureFallsBackToBootstrap()
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
                        2401,
                        "Epic",
                        "Roadmap Epic 1",
                        100,
                        1d,
                        "roadmap",
                        startDate: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                        targetDate: sprints[10][0].EndUtc!.Value.AddDays(-1))
                ]
            },
            sprints);

        var board = await service.BuildPlanningBoardAsync(7);

        Assert.IsNotNull(board);
        Assert.AreEqual(0, board.EpicItems.Single().PlannedStartSprintIndex);
        Assert.IsEmpty(await intentStore.GetByProductAsync(7));
        Assert.IsEmpty(tfsClient.PlanningDateUpdates);
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_RemovesPersistedIntentForEpicThatDisappearedUpstream()
    {
        var intentStore = new InMemoryProductPlanningIntentStore();
        var sprints = CreateDefaultSprintsByTeam([10], sprintCount: 6);
        intentStore.Seed(
            new ProductPlanningIntentRecord(7, 2501, sprints[10][0].StartUtc!.Value.UtcDateTime.Date, 1, null, DateTime.UtcNow),
            new ProductPlanningIntentRecord(7, 2599, sprints[10][1].StartUtc!.Value.UtcDateTime.Date, 1, null, DateTime.UtcNow));

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
                    CreateWorkItem(2501, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap")
                ]
            },
            sprints);

        _ = await service.BuildPlanningBoardAsync(7);
        var intents = await intentStore.GetByProductAsync(7);

        CollectionAssert.AreEqual(new[] { 2501 }, intents.Select(static intent => intent.EpicId).ToArray());
    }

    [TestMethod]
    public async Task ExecuteMoveEpicBySprintsAsync_PersistsIntentAndWritesPlanningDates()
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
                    CreateWorkItem(2601, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
                    CreateWorkItem(2602, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap")
                ]
            },
            sprints);

        var board = await service.ExecuteMoveEpicBySprintsAsync(7, 2602, 2);
        var intents = await intentStore.GetByProductAsync(7);

        Assert.IsNotNull(board);
        Assert.AreEqual(3, board.EpicItems.Single(static epic => epic.EpicId == 2602).ComputedStartSprintIndex);
        Assert.HasCount(2, intents);
        Assert.HasCount(2, tfsClient.PlanningDateUpdates);
        Assert.AreEqual(
            DateOnly.FromDateTime(sprints[10][3].StartUtc!.Value.UtcDateTime),
            tfsClient.PlanningDateUpdates.Single(static update => update.WorkItemId == 2602).StartDate);
    }

    [TestMethod]
    public async Task ExecuteMoveEpicBySprintsAsync_WithInsufficientCalendarCoverage_Throws()
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
                    CreateWorkItem(2701, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
                    CreateWorkItem(2702, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap")
                ]
            },
            new Dictionary<int, IReadOnlyList<SprintDto>>
            {
                [10] = CreateSequentialSprints(10, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 2)
            });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.ExecuteMoveEpicBySprintsAsync(7, 2702, 2).AsTask());
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_WithAmbiguousCalendarAndPersistedIntent_Throws()
    {
        var intentStore = new InMemoryProductPlanningIntentStore();
        intentStore.Seed(new ProductPlanningIntentRecord(7, 2801, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 1, null, DateTime.UtcNow));

        var service = CreateService(
            new InMemoryProductPlanningSessionStore(),
            intentStore,
            new RecordingTfsClient(),
            [CreateProduct(7, "Roadmap Product", [100], [10, 11])],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
                    CreateWorkItem(2801, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap")
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

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.BuildPlanningBoardAsync(7).AsTask());
    }

    [TestMethod]
    public async Task Sessions_AreIsolatedPerProduct()
    {
        var store = new InMemoryProductPlanningSessionStore();
        var service = CreateService(
            store,
            [
                CreateProduct(7, "Roadmap Product A", 100),
                CreateProduct(8, "Roadmap Product B", 200)
            ],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>
            {
                [100] =
                [
                    CreateWorkItem(100, "Objective", "Root Objective A", null, null, null),
                    CreateWorkItem(1201, "Epic", "Roadmap Epic A1", 100, 1d, "roadmap"),
                    CreateWorkItem(1202, "Epic", "Roadmap Epic A2", 100, 2d, "roadmap")
                ],
                [200] =
                [
                    CreateWorkItem(200, "Objective", "Root Objective B", null, null, null),
                    CreateWorkItem(2201, "Epic", "Roadmap Epic B1", 200, 1d, "roadmap"),
                    CreateWorkItem(2202, "Epic", "Roadmap Epic B2", 200, 2d, "roadmap")
                ]
            });

        await service.ExecuteMoveEpicBySprintsAsync(7, 1202, 2);
        var productABoard = await service.GetPlanningBoardAsync(7);
        var productBBoard = await service.GetPlanningBoardAsync(8);

        Assert.IsNotNull(productABoard);
        Assert.IsNotNull(productBBoard);
        Assert.IsTrue(store.TryGetState(7, out _));
        Assert.IsTrue(store.TryGetState(8, out _));
        Assert.AreEqual(3, productABoard.EpicItems.Single(static epic => epic.EpicId == 1202).ComputedStartSprintIndex);
        Assert.AreEqual(1, productBBoard.EpicItems.Single(static epic => epic.EpicId == 2202).ComputedStartSprintIndex);
    }

    [TestMethod]
    public async Task GetPlanningBoardAsync_WithoutNewOperations_RemainsDeterministicWithinSession()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(1301, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(1302, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(1303, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        await service.ExecuteMoveEpicBySprintsAsync(7, 1302, 2);
        var firstRead = await service.GetPlanningBoardAsync(7);
        var secondRead = await service.GetPlanningBoardAsync(7);

        Assert.IsNotNull(firstRead);
        Assert.IsNotNull(secondRead);
        CollectionAssert.AreEqual(
            firstRead.EpicItems.Select(static epic => epic.EpicId).ToArray(),
            secondRead.EpicItems.Select(static epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(
            firstRead.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray(),
            secondRead.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
    }

    [TestMethod]
    public async Task SameOperationSequenceOnSameBootstrap_YieldsSameResultAfterReset()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(1401, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(1402, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(1403, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        await service.ExecuteMoveEpicBySprintsAsync(7, 1402, 2);
        await service.ExecuteRunInParallelAsync(7, 1403);
        var firstSequenceResult = await service.GetPlanningBoardAsync(7);

        var resetBoard = await service.ResetPlanningBoardAsync(7);
        await service.ExecuteMoveEpicBySprintsAsync(7, 1402, 2);
        await service.ExecuteRunInParallelAsync(7, 1403);
        var secondSequenceResult = await service.GetPlanningBoardAsync(7);

        Assert.IsNotNull(firstSequenceResult);
        Assert.IsNotNull(resetBoard);
        Assert.IsNotNull(secondSequenceResult);
        Assert.AreEqual(3, firstSequenceResult.EpicItems.Single(static epic => epic.EpicId == 1402).ComputedStartSprintIndex);
        Assert.AreEqual(3, resetBoard.EpicItems.Single(static epic => epic.EpicId == 1402).ComputedStartSprintIndex);
        Assert.AreEqual(5, secondSequenceResult.EpicItems.Single(static epic => epic.EpicId == 1402).ComputedStartSprintIndex);
        Assert.AreEqual(1, firstSequenceResult.EpicItems.Single(static epic => epic.EpicId == 1403).TrackIndex);
        Assert.AreEqual(1, secondSequenceResult.EpicItems.Single(static epic => epic.EpicId == 1403).TrackIndex);
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_WhenProductMissing_ReturnsNull()
    {
        var service = CreateService(
            new InMemoryProductPlanningSessionStore(),
            new InMemoryProductPlanningIntentStore(),
            new RecordingTfsClient(),
            [],
            new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>(),
            CreateDefaultSprintsByTeam([10]));

        var board = await service.BuildPlanningBoardAsync(999);

        Assert.IsNull(board);
    }
}
