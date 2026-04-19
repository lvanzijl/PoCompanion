using PoTool.Core.Contracts;
using PoTool.Core.Planning;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

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
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, resetBoard.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
        Assert.IsEmpty(resetBoard.ChangedEpicIds);
        Assert.IsEmpty(resetBoard.AffectedEpicIds);

        Assert.IsNotNull(nextRead);
        CollectionAssert.AreEqual(
            resetBoard.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray(),
            nextRead.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
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
            [
                CreateWorkItem(100, "Objective", "Root Objective A", null, null, null),
                CreateWorkItem(1201, "Epic", "Roadmap Epic A1", 100, 1d, "roadmap"),
                CreateWorkItem(1202, "Epic", "Roadmap Epic A2", 100, 2d, "roadmap")
            ],
            [
                CreateWorkItem(200, "Objective", "Root Objective B", null, null, null),
                CreateWorkItem(2201, "Epic", "Roadmap Epic B1", 200, 1d, "roadmap"),
                CreateWorkItem(2202, "Epic", "Roadmap Epic B2", 200, 2d, "roadmap")
            ]);

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

        await service.ResetPlanningBoardAsync(7);
        await service.ExecuteMoveEpicBySprintsAsync(7, 1402, 2);
        await service.ExecuteRunInParallelAsync(7, 1403);
        var secondSequenceResult = await service.GetPlanningBoardAsync(7);

        Assert.IsNotNull(firstSequenceResult);
        Assert.IsNotNull(secondSequenceResult);
        CollectionAssert.AreEqual(
            firstSequenceResult.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray(),
            secondSequenceResult.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
        CollectionAssert.AreEqual(
            firstSequenceResult.EpicItems.Select(static epic => epic.TrackIndex).ToArray(),
            secondSequenceResult.EpicItems.Select(static epic => epic.TrackIndex).ToArray());
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_WhenProductMissing_ReturnsNull()
    {
        var service = new ProductPlanningBoardService(
            new FakeProductRepository(),
            new FakeWorkItemReadProvider(),
            new InMemoryProductPlanningSessionStore());

        var board = await service.BuildPlanningBoardAsync(999);

        Assert.IsNull(board);
    }

    private static ProductPlanningBoardService CreateService(params WorkItemDto[] workItems)
    {
        return CreateService(new InMemoryProductPlanningSessionStore(), workItems);
    }

    private static ProductPlanningBoardService CreateService(
        InMemoryProductPlanningSessionStore sessionStore,
        params WorkItemDto[] workItems)
    {
        return new ProductPlanningBoardService(
            new FakeProductRepository(CreateProduct(7, "Roadmap Product", 100)),
            new FakeWorkItemReadProvider(workItems),
            sessionStore);
    }

    private static ProductPlanningBoardService CreateService(
        InMemoryProductPlanningSessionStore sessionStore,
        IReadOnlyList<ProductDto> products,
        IReadOnlyList<WorkItemDto> product7WorkItems,
        IReadOnlyList<WorkItemDto> product8WorkItems)
    {
        return new ProductPlanningBoardService(
            new FakeProductRepository(products.ToArray()),
            new FakeWorkItemReadProvider(new Dictionary<int, IReadOnlyList<WorkItemDto>>
            {
                [100] = product7WorkItems,
                [200] = product8WorkItems
            }),
            sessionStore);
    }

    private static ProductDto CreateProduct(int productId, string name, params int[] rootIds)
    {
        return new ProductDto(
            productId,
            null,
            name,
            rootIds.ToList(),
            0,
            ProductPictureType.Default,
            0,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            [],
            []);
    }

    private static WorkItemDto CreateWorkItem(
        int tfsId,
        string type,
        string title,
        int? parentId,
        double? backlogPriority,
        string? tags)
    {
        return new WorkItemDto(
            tfsId,
            type,
            title,
            parentId,
            "Area",
            "Iteration",
            "Active",
            DateTimeOffset.UtcNow,
            null,
            null,
            BacklogPriority: backlogPriority,
            Tags: tags);
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly Dictionary<int, ProductDto> _products;

        public FakeProductRepository(params ProductDto[] products)
        {
            _products = products.ToDictionary(static product => product.Id);
        }

        public Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_products.GetValueOrDefault(id));
        }

        public Task<IEnumerable<ProductDto>> GetProductsByOwnerAsync(int productOwnerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> GetProductsByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductDto> CreateProductAsync(int? productOwnerId, string name, List<int> backlogRootWorkItemIds, ProductPictureType pictureType, int defaultPictureId, string? customPicturePath, EstimationMode estimationMode = EstimationMode.StoryPoints, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductDto> UpdateProductAsync(int id, string name, List<int> backlogRootWorkItemIds, ProductPictureType? pictureType, int? defaultPictureId, string? customPicturePath, EstimationMode? estimationMode = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductDto> ChangeProductOwnerAsync(int productId, int? newProductOwnerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> GetSelectableProductsAsync(int productOwnerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> ReorderProductsAsync(int productOwnerId, List<int> productIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> LinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UnlinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeWorkItemReadProvider : IWorkItemReadProvider
    {
        private readonly IReadOnlyList<WorkItemDto>? _workItems;
        private readonly IReadOnlyDictionary<int, IReadOnlyList<WorkItemDto>>? _workItemsByRootId;

        public FakeWorkItemReadProvider(params WorkItemDto[] workItems)
        {
            _workItems = workItems;
        }

        public FakeWorkItemReadProvider(IReadOnlyDictionary<int, IReadOnlyList<WorkItemDto>> workItemsByRootId)
        {
            _workItemsByRootId = workItemsByRootId;
        }

        public Task<IEnumerable<WorkItemDto>> GetByRootIdsAsync(int[] rootWorkItemIds, CancellationToken cancellationToken = default)
        {
            if (_workItemsByRootId is not null)
            {
                var workItems = rootWorkItemIds
                    .Distinct()
                    .OrderBy(static rootId => rootId)
                    .SelectMany(rootId => _workItemsByRootId.GetValueOrDefault(rootId, Array.Empty<WorkItemDto>()))
                    .ToArray();

                return Task.FromResult<IEnumerable<WorkItemDto>>(workItems);
            }

            return Task.FromResult<IEnumerable<WorkItemDto>>(_workItems ?? Array.Empty<WorkItemDto>());
        }

        public Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
