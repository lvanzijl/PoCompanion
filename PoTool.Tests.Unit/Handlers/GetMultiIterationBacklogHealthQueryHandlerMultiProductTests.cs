using Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Services;
using PoTool.Core.BacklogQuality;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Tests for GetMultiIterationBacklogHealthQueryHandler with multiple product IDs.
/// Validates cumulative "All products" aggregation and deduplication logic.
/// </summary>
[TestClass]
public class GetMultiIterationBacklogHealthQueryHandlerMultiProductTests
{
    private Mock<IWorkItemReadProvider> _mockProvider = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<ISprintRepository> _mockSprintRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<IBacklogQualityAnalysisService> _mockBacklogQualityAnalysisService = null!;
    private Mock<ILogger<GetMultiIterationBacklogHealthQueryHandler>> _mockLogger = null!;
    private GetMultiIterationBacklogHealthQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockProvider = new Mock<IWorkItemReadProvider>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockSprintRepository = new Mock<ISprintRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockBacklogQualityAnalysisService = new Mock<IBacklogQualityAnalysisService>();
        _mockLogger = new Mock<ILogger<GetMultiIterationBacklogHealthQueryHandler>>();

        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        _mockBacklogQualityAnalysisService.Setup(service => service.AnalyzeAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAnalysis());

        var now = DateTimeOffset.UtcNow;
        _mockSprintRepository.Setup(r => r.GetAllSprintsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SprintDto>
            {
                new(
                    Id: 1,
                    TeamId: 1,
                    TfsIterationId: "past3-guid",
                    Path: "Past Sprint 3",
                    Name: "Past Sprint 3",
                    StartUtc: now.AddDays(-90),
                    EndUtc: now.AddDays(-76),
                    TimeFrame: "past",
                    LastSyncedUtc: now),
                new(
                    Id: 2,
                    TeamId: 1,
                    TfsIterationId: "past2-guid",
                    Path: "Past Sprint 2",
                    Name: "Past Sprint 2",
                    StartUtc: now.AddDays(-75),
                    EndUtc: now.AddDays(-61),
                    TimeFrame: "past",
                    LastSyncedUtc: now),
                new(
                    Id: 3,
                    TeamId: 1,
                    TfsIterationId: "past1-guid",
                    Path: "Past Sprint 1",
                    Name: "Past Sprint 1",
                    StartUtc: now.AddDays(-60),
                    EndUtc: now.AddDays(-46),
                    TimeFrame: "past",
                    LastSyncedUtc: now),
                new(
                    Id: 4,
                    TeamId: 1,
                    TfsIterationId: "sprint1-guid",
                    Path: "Sprint 1",
                    Name: "Sprint 1",
                    StartUtc: now.AddDays(-45),
                    EndUtc: now.AddDays(1),
                    TimeFrame: "current",
                    LastSyncedUtc: now),
                new(
                    Id: 5,
                    TeamId: 1,
                    TfsIterationId: "future1-guid",
                    Path: "Future Sprint 1",
                    Name: "Future Sprint 1",
                    StartUtc: now.AddDays(2),
                    EndUtc: now.AddDays(16),
                    TimeFrame: "future",
                    LastSyncedUtc: now),
                new(
                    Id: 6,
                    TeamId: 1,
                    TfsIterationId: "future2-guid",
                    Path: "Future Sprint 2",
                    Name: "Future Sprint 2",
                    StartUtc: now.AddDays(17),
                    EndUtc: now.AddDays(31),
                    TimeFrame: "future",
                    LastSyncedUtc: now)
            });

        _handler = new GetMultiIterationBacklogHealthQueryHandler(
            _mockProvider.Object,
            _mockProductRepository.Object,
            _mockSprintRepository.Object,
            _mockMediator.Object,
            _mockBacklogQualityAnalysisService.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithTwoDisjointProducts_ReturnsCumulativeTotals()
    {
        var product1 = CreateProduct(1, 1, "Product A", 100);
        var product2 = CreateProduct(2, 1, "Product B", 200);

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            CreateWorkItem(102, "Story", "In Progress", "Sprint 1", null, null, 100),
            CreateWorkItem(200, "Epic", "Active", "Sprint 1", null, 20, null),
            CreateWorkItem(201, "Story", "Done", "Sprint 1", 3, null, 200),
            CreateWorkItem(202, "Story", "New", "Sprint 1", null, null, 200)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product1);
        _mockProductRepository.Setup(r => r.GetProductByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product2);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _handler.Handle(new GetMultiIterationBacklogHealthQuery(ProductIds: new[] { 1, 2 }, MaxIterations: 5), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IterationHealth.Any());

        var healthForSprint1 = result.IterationHealth.First();
        Assert.AreEqual(6, healthForSprint1.TotalWorkItems);
        Assert.AreEqual(4, healthForSprint1.WorkItemsWithoutEffort);
        Assert.AreEqual(1, healthForSprint1.WorkItemsInProgressWithoutEffort);
        Assert.AreEqual(6, result.TotalWorkItems);
    }

    [TestMethod]
    public async Task Handle_WithOverlappingProducts_DeduplicatesWorkItems()
    {
        var product1 = CreateProduct(1, 1, "Product A", 100);
        var product2 = CreateProduct(2, 1, "Product B", 101);

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            CreateWorkItem(102, "Story", "In Progress", "Sprint 1", null, null, 100),
            CreateWorkItem(103, "Task", "New", "Sprint 1", 2, null, 101)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product1);
        _mockProductRepository.Setup(r => r.GetProductByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product2);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _handler.Handle(new GetMultiIterationBacklogHealthQuery(ProductIds: new[] { 1, 2 }, MaxIterations: 5), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IterationHealth.Any());

        var healthForSprint1 = result.IterationHealth.First();
        Assert.AreEqual(4, healthForSprint1.TotalWorkItems);
        Assert.AreEqual(2, healthForSprint1.WorkItemsWithoutEffort);
    }

    [TestMethod]
    public async Task Handle_WithSingleProduct_BehavesLikeOriginal()
    {
        var product = CreateProduct(1, 1, "Product A", 100);
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            CreateWorkItem(102, "Story", "In Progress", "Sprint 1", null, null, 100)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _handler.Handle(new GetMultiIterationBacklogHealthQuery(ProductIds: new[] { 1 }, MaxIterations: 5), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IterationHealth.Any());

        var healthForSprint1 = result.IterationHealth.First();
        Assert.AreEqual(3, healthForSprint1.TotalWorkItems);
        Assert.AreEqual(2, healthForSprint1.WorkItemsWithoutEffort);
    }

    [TestMethod]
    public async Task Handle_WithNonExistentProduct_SkipsInvalidProducts()
    {
        var product1 = CreateProduct(1, 1, "Product A", 100);
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product1);
        _mockProductRepository.Setup(r => r.GetProductByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _handler.Handle(new GetMultiIterationBacklogHealthQuery(ProductIds: new[] { 1, 999 }, MaxIterations: 5), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IterationHealth.Any());
        Assert.AreEqual(2, result.IterationHealth.First().TotalWorkItems);
    }

    [TestMethod]
    public async Task Handle_UsesBacklogQualityAnalysisServiceForRealSprintSlots()
    {
        var product = CreateProduct(1, 1, "Product A", 100);
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        await _handler.Handle(new GetMultiIterationBacklogHealthQuery(ProductIds: new[] { 1 }, MaxIterations: 5), CancellationToken.None);

        _mockBacklogQualityAnalysisService.Verify(
            service => service.AnalyzeAsync(
                It.Is<IEnumerable<WorkItemDto>>(items => items.Select(item => item.TfsId).OrderBy(id => id).SequenceEqual(new[] { 100, 101 })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ProductDto CreateProduct(int id, int ownerId, string name, int rootWorkItemId)
    {
        return new ProductDto(
            Id: id,
            ProductOwnerId: ownerId,
            Name: name,
            BacklogRootWorkItemIds: new List<int> { rootWorkItemId },
            Order: 0,
            PictureType: ProductPictureType.Default,
            DefaultPictureId: 0,
            CustomPicturePath: null,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow,
            LastSyncedAt: null,
            TeamIds: new List<int>(),
            Repositories: new List<RepositoryDto>());
    }

    private static WorkItemDto CreateWorkItem(
        int tfsId,
        string type,
        string state,
        string iterationPath,
        int? effort,
        int? originalEstimate = null,
        int? parentTfsId = null)
    {
        return new WorkItemDto(
            TfsId: tfsId,
            Type: type,
            Title: $"Work Item {tfsId}",
            ParentTfsId: parentTfsId,
            AreaPath: "MyProject",
            IterationPath: iterationPath,
            State: state,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
            Description: null,
            Tags: null);
    }

    private static BacklogQualityAnalysisResult CreateAnalysis()
    {
        return new BacklogQualityAnalysisResult(
            new BacklogValidationResult(
                Array.Empty<BacklogIntegrityFinding>(),
                Array.Empty<PoTool.Core.Domain.BacklogQuality.Models.ValidationRuleResult>(),
                Array.Empty<RefinementReadinessState>(),
                Array.Empty<ImplementationReadinessState>()),
            Array.Empty<BacklogReadinessScore>());
    }
}
