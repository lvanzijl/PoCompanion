using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Planning;

namespace PoTool.Api.Tests;

[TestClass]
public sealed class ProductPlanningBoardClientUiTests
{
    [TestMethod]
    public async Task ProductPlanningBoardClientService_GetBoardAsync_UsesProductBoardEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var service = CreateService((request, _) =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateBoardContent()
            };
        });

        var result = await service.GetBoardAsync(7);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Get, capturedRequest.Method);
        Assert.AreEqual("http://localhost/api/products/7/planning-board", capturedRequest.RequestUri!.AbsoluteUri);
    }

    [TestMethod]
    public async Task ProductPlanningBoardClientService_ResetAsync_UsesResetEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var service = CreateService((request, _) =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateBoardContent()
            };
        });

        var result = await service.ResetAsync(7);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Post, capturedRequest.Method);
        Assert.AreEqual("http://localhost/api/products/7/planning-board/reset", capturedRequest.RequestUri!.AbsoluteUri);
        Assert.IsNull(capturedRequest.Content);
    }

    [TestMethod]
    public async Task ProductPlanningBoardClientService_MoveEpicBySprintsAsync_PostsEpicDeltaRequest()
    {
        await AssertDeltaMutationAsync(
            (service, request) => service.MoveEpicBySprintsAsync(7, request),
            "/api/products/7/planning-board/move");
    }

    [TestMethod]
    public async Task ProductPlanningBoardClientService_AdjustSpacingBeforeAsync_PostsEpicDeltaRequest()
    {
        await AssertDeltaMutationAsync(
            (service, request) => service.AdjustSpacingBeforeAsync(7, request),
            "/api/products/7/planning-board/adjust-spacing");
    }

    [TestMethod]
    public async Task ProductPlanningBoardClientService_ShiftPlanAsync_PostsEpicDeltaRequest()
    {
        await AssertDeltaMutationAsync(
            (service, request) => service.ShiftPlanAsync(7, request),
            "/api/products/7/planning-board/shift-plan");
    }

    [TestMethod]
    public async Task ProductPlanningBoardClientService_RunInParallelAsync_PostsEpicRequest()
    {
        await AssertEpicMutationAsync(
            (service, request) => service.RunInParallelAsync(7, request),
            "/api/products/7/planning-board/run-in-parallel");
    }

    [TestMethod]
    public async Task ProductPlanningBoardClientService_ReturnToMainAsync_PostsEpicRequest()
    {
        await AssertEpicMutationAsync(
            (service, request) => service.ReturnToMainAsync(7, request),
            "/api/products/7/planning-board/return-to-main");
    }

    [TestMethod]
    public async Task ProductPlanningBoardClientService_ReconcileProjectionAsync_PostsEpicRequest()
    {
        await AssertEpicMutationAsync(
            (service, request) => service.ReconcileProjectionAsync(7, request),
            "/api/products/7/planning-board/reconcile");
    }

    [TestMethod]
    public async Task ProductPlanningBoardClientService_ReorderEpicAsync_PostsReorderRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        string? body = null;
        var service = CreateService(async (request, _) =>
        {
            capturedRequest = request;
            body = request.Content == null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateBoardContent()
            };
        });

        var result = await service.ReorderEpicAsync(7, new ReorderProductPlanningEpicRequest(101, 3));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("http://localhost/api/products/7/planning-board/reorder", capturedRequest.RequestUri!.AbsoluteUri);
        StringAssert.Contains(body, "\"EpicId\":101");
        StringAssert.Contains(body, "\"TargetRoadmapOrder\":3");
    }

    [TestMethod]
    public async Task ProductPlanningBoardClientService_GetBoardAsync_MapsNotFound()
    {
        var service = CreateService((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await service.GetBoardAsync(999);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.IsNotFound);
        Assert.AreEqual("The selected product planning board could not be found.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ProductPlanningBoardClientService_GetBoardAsync_ParsesOperationalBlockerMessage()
    {
        var service = CreateService((_, _) => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("{\"message\":\"Planning is blocked by an ambiguous sprint calendar.\"}", Encoding.UTF8, "application/json")
        });

        var result = await service.GetBoardAsync(7);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("Planning is blocked by an ambiguous sprint calendar.", result.ErrorMessage);
    }

    [TestMethod]
    public void ProductPlanningBoardRenderModelFactory_Create_GroupsMainAndParallelTracks()
    {
        var board = CreateBoard();

        var renderModel = ProductPlanningBoardRenderModelFactory.Create(board);

        Assert.AreEqual(3, renderModel.MaxSprintCount);
        Assert.HasCount(2, renderModel.Tracks);
        Assert.IsTrue(renderModel.Tracks[0].IsMainLane);
        CollectionAssert.AreEqual(new[] { 101 }, renderModel.Tracks[0].Epics.Select(static epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(new[] { 102 }, renderModel.Tracks[1].Epics.Select(static epic => epic.EpicId).ToArray());
        Assert.IsTrue(renderModel.HasRecentChanges);
        Assert.AreEqual(ProductPlanningBoardStatusKind.Changed, renderModel.StatusKind);
    }

    [TestMethod]
    public void ProductPlanningBoardRenderModelFactory_Create_PrioritizesValidationWarningStatus()
    {
        var board = CreateBoard() with
        {
            Issues = [new PlanningBoardIssueDto("Validation", "Overlap", "Track overlap detected", 101)]
        };

        var renderModel = ProductPlanningBoardRenderModelFactory.Create(board);

        Assert.IsTrue(renderModel.HasValidationIssues);
        Assert.AreEqual(ProductPlanningBoardStatusKind.Warning, renderModel.StatusKind);
        StringAssert.Contains(renderModel.StatusDetail, "issue");
    }

    [TestMethod]
    public void ProductPlanningBoardRenderModelFactory_Create_SurfacesOperationalDiagnosticsSummary()
    {
        var board = CreateBoard() with
        {
            Diagnostics =
            [
                new PlanningBoardDiagnosticDto("Error", "CalendarResolutionFailure", "Sprint calendar is ambiguous.", null, true, false)
            ],
            EpicItems =
            [
                new PlanningBoardEpicItemDto(
                    101,
                    "Epic A",
                    1,
                    0,
                    0,
                    0,
                    2,
                    2,
                    [],
                    false,
                    false,
                    PlanningBoardIntentSource.Recovered,
                    ProductPlanningRecoveryStatus.RecoveredWithNormalization,
                    PlanningBoardDriftStatus.TfsProjectionMismatch,
                    false,
                    [
                        new PlanningBoardDiagnosticDto("Warning", "StaleTfsProjection", "TFS dates differ from internal intent.", 101, false, false)
                    ]),
                new PlanningBoardEpicItemDto(102, "Epic B", 2, 1, 1, 1, 2, 3, [], false, false)
            ]
        };

        var renderModel = ProductPlanningBoardRenderModelFactory.Create(board);

        Assert.IsTrue(renderModel.HasOperationalDiagnostics);
        Assert.IsTrue(renderModel.HasBlockingDiagnostics);
        Assert.AreEqual(1, renderModel.RecoveredEpicCount);
        Assert.AreEqual(1, renderModel.DriftedEpicCount);
        Assert.AreEqual(ProductPlanningBoardStatusKind.Warning, renderModel.StatusKind);
        StringAssert.Contains(renderModel.StatusLabel, "blocked");
    }

    private static async Task AssertDeltaMutationAsync(
        Func<ProductPlanningBoardClientService, ProductPlanningEpicDeltaRequest, Task<ProductPlanningBoardClientResult>> action,
        string expectedPath)
    {
        HttpRequestMessage? capturedRequest = null;
        string? body = null;
        var service = CreateService(async (request, _) =>
        {
            capturedRequest = request;
            body = request.Content == null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateBoardContent()
            };
        });

        var result = await action(service, new ProductPlanningEpicDeltaRequest(101, -2));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Post, capturedRequest.Method);
        Assert.AreEqual($"http://localhost{expectedPath}", capturedRequest.RequestUri!.AbsoluteUri);
        StringAssert.Contains(body, "\"EpicId\":101");
        StringAssert.Contains(body, "\"DeltaSprints\":-2");
    }

    private static async Task AssertEpicMutationAsync(
        Func<ProductPlanningBoardClientService, ProductPlanningEpicRequest, Task<ProductPlanningBoardClientResult>> action,
        string expectedPath)
    {
        HttpRequestMessage? capturedRequest = null;
        string? body = null;
        var service = CreateService(async (request, _) =>
        {
            capturedRequest = request;
            body = request.Content == null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateBoardContent()
            };
        });

        var result = await action(service, new ProductPlanningEpicRequest(101));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Post, capturedRequest.Method);
        Assert.AreEqual($"http://localhost{expectedPath}", capturedRequest.RequestUri!.AbsoluteUri);
        StringAssert.Contains(body, "\"EpicId\":101");
    }

    private static ProductPlanningBoardClientService CreateService(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        => CreateService((request, cancellationToken) => Task.FromResult(handler(request, cancellationToken)));

    private static ProductPlanningBoardClientService CreateService(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var httpClient = new HttpClient(new StubHandler(handler))
        {
            BaseAddress = new Uri("http://localhost")
        };

        return new ProductPlanningBoardClientService(httpClient, NullLogger<ProductPlanningBoardClientService>.Instance);
    }

    private static StringContent CreateBoardContent()
        => new(ToJson(CreateBoard()), Encoding.UTF8, "application/json");

    private static ProductPlanningBoardDto CreateBoard()
        => new(
            7,
            "Roadmap Product",
            [
                new PlanningBoardTrackDto(0, true, [101]),
                new PlanningBoardTrackDto(1, false, [102])
            ],
            [
                new PlanningBoardEpicItemDto(101, "Epic A", 1, 0, 0, 0, 2, 2, [], true, false),
                new PlanningBoardEpicItemDto(102, "Epic B", 2, 1, 1, 1, 2, 3, [], false, true)
            ],
            [],
            [101],
            [102]);

    private static string ToJson(ProductPlanningBoardDto board)
        => System.Text.Json.JsonSerializer.Serialize(board);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
