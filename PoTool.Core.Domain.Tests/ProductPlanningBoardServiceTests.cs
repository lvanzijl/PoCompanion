using PoTool.Core.Contracts;
using PoTool.Core.Planning;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.Domain.Tests;

[TestClass]
public sealed class ProductPlanningBoardServiceTests
{
    [TestMethod]
    public async Task BuildPlanningBoardAsync_BootstrapsDeterministicallyOnMainLaneWithDefaultDuration()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(502, "Epic", "Roadmap Epic B", 100, 10d, "roadmap"),
            CreateWorkItem(501, "Epic", "Roadmap Epic A", 100, 20d, "roadmap"),
            CreateWorkItem(503, "Epic", "Non-roadmap Epic", 100, 5d, "not-roadmap"));

        var firstBoard = await service.BuildPlanningBoardAsync(7);
        var secondBoard = await service.BuildPlanningBoardAsync(7);

        Assert.IsNotNull(firstBoard);
        Assert.IsNotNull(secondBoard);
        Assert.AreEqual(7, firstBoard.ProductId);
        Assert.AreEqual("Roadmap Product", firstBoard.ProductName);
        Assert.AreEqual(firstBoard.ProductId, secondBoard.ProductId);
        Assert.AreEqual(firstBoard.ProductName, secondBoard.ProductName);
        Assert.HasCount(1, firstBoard.Tracks);
        Assert.IsTrue(firstBoard.Tracks[0].IsMainLane);
        CollectionAssert.AreEqual(new[] { 502, 501 }, firstBoard.EpicItems.Select(static epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 0 }, firstBoard.EpicItems.Select(static epic => epic.TrackIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 1 }, firstBoard.EpicItems.Select(static epic => epic.DurationInSprints).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, firstBoard.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
        CollectionAssert.AreEqual(
            firstBoard.EpicItems.Select(static epic => epic.EpicId).ToArray(),
            secondBoard.EpicItems.Select(static epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(
            firstBoard.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray(),
            secondBoard.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
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
    public async Task ExecuteMoveAndAdjustSpacingBeforeAsync_PropagateChangedAffectedAndValidation()
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
        Assert.AreEqual(3, spacingResult.EpicItems.Single(static epic => epic.EpicId == 703).ComputedStartSprintIndex);

        Assert.IsNotNull(invalidShiftResult);
        Assert.HasCount(1, invalidShiftResult.Issues);
        Assert.AreEqual("InvalidOperationInput", invalidShiftResult.Issues[0].Code);
        Assert.IsEmpty(invalidShiftResult.ChangedEpicIds);
        Assert.IsEmpty(invalidShiftResult.AffectedEpicIds);
    }

    [TestMethod]
    public async Task ExecuteRunInParallelAndReturnToMainAsync_SurfaceTrackChangesInReadModel()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(801, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(802, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(803, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        var parallelResult = await service.ExecuteRunInParallelAsync(7, 803);
        var returnToMainResult = await service.ExecuteReturnToMainAsync(7, 803);

        Assert.IsNotNull(parallelResult);
        Assert.AreEqual(1, parallelResult.EpicItems.Single(static epic => epic.EpicId == 803).TrackIndex);
        Assert.AreEqual(2, parallelResult.EpicItems.Single(static epic => epic.EpicId == 803).ComputedStartSprintIndex);
        Assert.HasCount(2, parallelResult.Tracks);
        CollectionAssert.AreEqual(new[] { 803 }, parallelResult.Tracks.Single(static track => track.TrackIndex == 1).EpicIds.ToArray());

        Assert.IsNotNull(returnToMainResult);
        Assert.AreEqual(0, returnToMainResult.EpicItems.Single(static epic => epic.EpicId == 803).TrackIndex);
        Assert.IsEmpty(returnToMainResult.ChangedEpicIds);
        CollectionAssert.AreEqual(new[] { 803 }, returnToMainResult.AffectedEpicIds.ToArray());
    }

    [TestMethod]
    public async Task ExecuteReorderAndShiftPlanAsync_ReturnUpdatedRoadmapOrderAndSuffixShape()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(901, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(902, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(903, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        var reorderResult = await service.ExecuteReorderEpicAsync(7, 903, 2);
        var shiftResult = await service.ExecuteShiftPlanAsync(7, 902, 3);

        Assert.IsNotNull(reorderResult);
        CollectionAssert.AreEqual(new[] { 901, 903, 902 }, reorderResult.EpicItems.Select(static epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, reorderResult.EpicItems.Select(static epic => epic.RoadmapOrder).ToArray());
        CollectionAssert.AreEqual(new[] { 903, 902 }, reorderResult.AffectedEpicIds.ToArray());

        Assert.IsNotNull(shiftResult);
        CollectionAssert.AreEqual(new[] { 0, 4, 5 }, shiftResult.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 4, 5 }, shiftResult.EpicItems.Skip(1).Select(static epic => epic.PlannedStartSprintIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 902, 903 }, shiftResult.AffectedEpicIds.ToArray());
    }

    [TestMethod]
    public async Task ExecuteOperations_WithSameInputs_RemainDeterministic()
    {
        var service = CreateService(
            CreateWorkItem(100, "Objective", "Root Objective", null, null, null),
            CreateWorkItem(1001, "Epic", "Roadmap Epic 1", 100, 1d, "roadmap"),
            CreateWorkItem(1002, "Epic", "Roadmap Epic 2", 100, 2d, "roadmap"),
            CreateWorkItem(1003, "Epic", "Roadmap Epic 3", 100, 3d, "roadmap"));

        var firstResult = await service.ExecuteReorderEpicAsync(7, 1003, 2);
        var secondResult = await service.ExecuteReorderEpicAsync(7, 1003, 2);

        Assert.IsNotNull(firstResult);
        Assert.IsNotNull(secondResult);
        CollectionAssert.AreEqual(
            firstResult.EpicItems.Select(static epic => epic.EpicId).ToArray(),
            secondResult.EpicItems.Select(static epic => epic.EpicId).ToArray());
        CollectionAssert.AreEqual(
            firstResult.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray(),
            secondResult.EpicItems.Select(static epic => epic.ComputedStartSprintIndex).ToArray());
        CollectionAssert.AreEqual(firstResult.ChangedEpicIds.ToArray(), secondResult.ChangedEpicIds.ToArray());
        CollectionAssert.AreEqual(firstResult.AffectedEpicIds.ToArray(), secondResult.AffectedEpicIds.ToArray());
    }

    [TestMethod]
    public async Task BuildPlanningBoardAsync_WhenProductMissing_ReturnsNull()
    {
        var service = new ProductPlanningBoardService(new FakeProductRepository(), new FakeWorkItemReadProvider());

        var board = await service.BuildPlanningBoardAsync(999);

        Assert.IsNull(board);
    }

    private static ProductPlanningBoardService CreateService(params WorkItemDto[] workItems)
    {
        return new ProductPlanningBoardService(
            new FakeProductRepository(CreateProduct(7, "Roadmap Product", 100)),
            new FakeWorkItemReadProvider(workItems));
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
        private readonly IReadOnlyList<WorkItemDto> _workItems;

        public FakeWorkItemReadProvider(params WorkItemDto[] workItems)
        {
            _workItems = workItems;
        }

        public Task<IEnumerable<WorkItemDto>> GetByRootIdsAsync(int[] rootWorkItemIds, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<WorkItemDto>>(_workItems);
        }

        public Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
