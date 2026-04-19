using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Controllers;
using PoTool.Core.Domain.Tests;
using PoTool.Core.Planning;
using PoTool.Shared.Planning;
using PoTool.Shared.WorkItems;
using static PoTool.Core.Domain.Tests.ProductPlanningBoardTestFactory;

namespace PoTool.Api.Tests;

[TestClass]
public sealed class ProductPlanningBoardControllerTests
{
    [TestMethod]
    public async Task GetPlanningBoard_ValidProduct_ReturnsOkBoard()
    {
        var controller = CreateController(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(1501, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(1502, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"));

        var result = await controller.GetPlanningBoard(7, CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        var board = (ProductPlanningBoardDto)((OkObjectResult)result.Result).Value!;
        Assert.AreEqual(7, board.ProductId);
        CollectionAssert.AreEqual(new[] { 1501, 1502 }, board.EpicItems.Select(static epic => epic.EpicId).ToArray());
    }

    [TestMethod]
    public async Task GetPlanningBoard_MissingProduct_ReturnsNotFound()
    {
        var controller = new ProductPlanningBoardController(
            CreateService(
                new InMemoryProductPlanningSessionStore(),
                new InMemoryProductPlanningIntentStore(),
                new RecordingTfsClient(),
                [],
                new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>(),
                CreateDefaultSprintsByTeam([10])));

        var result = await controller.GetPlanningBoard(999, CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundResult>(result.Result);
    }

    [TestMethod]
    public async Task ResetPlanningBoard_ReturnsFreshBoardAfterSessionChanges()
    {
        var controller = CreateController(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(1601, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(1602, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(1603, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        await controller.MoveEpicBySprints(7, new ProductPlanningEpicDeltaRequest(1602, 2), CancellationToken.None);
        var resetResult = await controller.ResetPlanningBoard(7, CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(resetResult.Result);
        var board = (ProductPlanningBoardDto)((OkObjectResult)resetResult.Result).Value!;
        CollectionAssert.AreEqual(new[] { 0, 3, 4 }, board.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
    }

    [TestMethod]
    public async Task MutationEndpoints_ReturnUpdatedBoardsAndPreserveChangedAffectedIds()
    {
        var controller = CreateController(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(1701, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(1702, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(1703, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        var moveBoard = await Unwrap(controller.MoveEpicBySprints(7, new ProductPlanningEpicDeltaRequest(1702, 2), CancellationToken.None));
        var adjustBoard = await Unwrap(controller.AdjustSpacingBefore(7, new ProductPlanningEpicDeltaRequest(1703, 1), CancellationToken.None));
        var parallelBoard = await Unwrap(controller.RunInParallel(7, new ProductPlanningEpicRequest(1703), CancellationToken.None));
        var returnBoard = await Unwrap(controller.ReturnToMain(7, new ProductPlanningEpicRequest(1703), CancellationToken.None));
        var reorderBoard = await Unwrap(controller.ReorderEpic(7, new ReorderProductPlanningEpicRequest(1703, 2), CancellationToken.None));
        var shiftBoard = await Unwrap(controller.ShiftPlan(7, new ProductPlanningEpicDeltaRequest(1702, 2), CancellationToken.None));

        CollectionAssert.AreEqual(new[] { 1702, 1703 }, moveBoard.AffectedEpicIds.ToArray());
        Assert.AreEqual(4, adjustBoard.EpicItems.Single(static epic => epic.EpicId == 1703).ComputedStartSprintIndex);
        Assert.AreEqual(1, parallelBoard.EpicItems.Single(static epic => epic.EpicId == 1703).TrackIndex);
        Assert.AreEqual(0, returnBoard.EpicItems.Single(static epic => epic.EpicId == 1703).TrackIndex);
        CollectionAssert.AreEqual(new[] { 1701, 1703, 1702 }, reorderBoard.EpicItems.Select(static epic => epic.EpicId).ToArray());
        CollectionAssert.Contains(shiftBoard.AffectedEpicIds.ToArray(), 1702);
    }

    [TestMethod]
    public async Task SequentialApiCalls_ReuseSessionState_AndResetClearsIt()
    {
        var controller = CreateController(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(1801, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(1802, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(1803, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        await controller.MoveEpicBySprints(7, new ProductPlanningEpicDeltaRequest(1802, 2), CancellationToken.None);
        await controller.RunInParallel(7, new ProductPlanningEpicRequest(1803), CancellationToken.None);
        var persistedBoard = await Unwrap(controller.GetPlanningBoard(7, CancellationToken.None));
        var resetBoard = await Unwrap(controller.ResetPlanningBoard(7, CancellationToken.None));

        Assert.AreEqual(3, persistedBoard.EpicItems.Single(static epic => epic.EpicId == 1802).ComputedStartSprintIndex);
        Assert.AreEqual(1, persistedBoard.EpicItems.Single(static epic => epic.EpicId == 1803).TrackIndex);
        CollectionAssert.AreEqual(new[] { 0, 3, 4 }, resetBoard.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
        Assert.AreEqual(0, resetBoard.EpicItems.Single(static epic => epic.EpicId == 1803).TrackIndex);
    }

    [TestMethod]
    public async Task InvalidPlanningOperations_ReturnOkBoardWithValidationIssues()
    {
        var controller = CreateController(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(1901, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(1902, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"));

        var unknownEpicBoard = await Unwrap(controller.RunInParallel(7, new ProductPlanningEpicRequest(999), CancellationToken.None));
        var invalidReorderBoard = await Unwrap(controller.ReorderEpic(7, new ReorderProductPlanningEpicRequest(1901, 0), CancellationToken.None));
        var invalidShiftBoard = await Unwrap(controller.ShiftPlan(7, new ProductPlanningEpicDeltaRequest(1901, 0), CancellationToken.None));
        var invalidMoveBoard = await Unwrap(controller.MoveEpicBySprints(7, new ProductPlanningEpicDeltaRequest(1901, 0), CancellationToken.None));

        CollectionAssert.AreEqual(new[] { "EpicNotFound" }, unknownEpicBoard.Issues.Select(static issue => issue.Code).ToArray());
        CollectionAssert.AreEqual(new[] { "InvalidOperationInput" }, invalidReorderBoard.Issues.Select(static issue => issue.Code).ToArray());
        CollectionAssert.AreEqual(new[] { "InvalidOperationInput" }, invalidShiftBoard.Issues.Select(static issue => issue.Code).ToArray());
        CollectionAssert.AreEqual(new[] { "InvalidOperationInput" }, invalidMoveBoard.Issues.Select(static issue => issue.Code).ToArray());
    }

    [TestMethod]
    public async Task MissingProductMutationEndpoints_ReturnNotFound()
    {
        var controller = new ProductPlanningBoardController(
            CreateService(
                new InMemoryProductPlanningSessionStore(),
                new InMemoryProductPlanningIntentStore(),
                new RecordingTfsClient(),
                [],
                new Dictionary<int, IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto>>(),
                CreateDefaultSprintsByTeam([10])));

        var endpoints = new Func<Task<ActionResult<ProductPlanningBoardDto>>>[]
        {
            () => controller.ResetPlanningBoard(999, CancellationToken.None),
            () => controller.MoveEpicBySprints(999, new ProductPlanningEpicDeltaRequest(1, 1), CancellationToken.None),
            () => controller.AdjustSpacingBefore(999, new ProductPlanningEpicDeltaRequest(1, 1), CancellationToken.None),
            () => controller.RunInParallel(999, new ProductPlanningEpicRequest(1), CancellationToken.None),
            () => controller.ReturnToMain(999, new ProductPlanningEpicRequest(1), CancellationToken.None),
            () => controller.ReorderEpic(999, new ReorderProductPlanningEpicRequest(1, 1), CancellationToken.None),
            () => controller.ShiftPlan(999, new ProductPlanningEpicDeltaRequest(1, 1), CancellationToken.None)
        };

        foreach (var endpoint in endpoints)
        {
            var result = await endpoint();
            Assert.IsInstanceOfType<NotFoundResult>(result.Result);
        }
    }

    private static ProductPlanningBoardController CreateController(params WorkItemDto[] workItems)
    {
        return new ProductPlanningBoardController(CreateService(workItems));
    }

    private static async Task<ProductPlanningBoardDto> Unwrap(Task<ActionResult<ProductPlanningBoardDto>> action)
    {
        var result = await action;
        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        return (ProductPlanningBoardDto)((OkObjectResult)result.Result).Value!;
    }
}
