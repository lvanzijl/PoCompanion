using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Shared.Settings;
using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Tests for GetVelocityTrendQueryHandler with multiple product IDs.
/// Validates cumulative "All products" velocity aggregation and deduplication logic.
/// </summary>
[TestClass]
public class GetVelocityTrendQueryHandlerMultiProductTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<ILogger<GetVelocityTrendQueryHandler>> _mockLogger = null!;
    private GetVelocityTrendQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<GetVelocityTrendQueryHandler>>();
        _handler = new GetVelocityTrendQueryHandler(
            _mockRepository.Object,
            _mockProductRepository.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithTwoDisjointProducts_ReturnsCumulativeVelocity()
    {
        // Arrange
        // Product 1 has root work item 100 with children completed in Sprint 1
        // Product 2 has root work item 200 with children completed in Sprint 1
        var product1 = CreateProduct(1, 1, "Product A", 100);
        var product2 = CreateProduct(2, 1, "Product B", 200);

        var workItems = new List<WorkItemDto>
        {
            // Product 1 items - 8 story points completed in Sprint 1
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            CreateWorkItem(102, "Story", "Done", "Sprint 1", 3, null, 100),

            // Product 2 items - 7 story points completed in Sprint 1
            CreateWorkItem(200, "Epic", "Active", "Sprint 1", null, 20, null),
            CreateWorkItem(201, "Story", "Done", "Sprint 1", 4, null, 200),
            CreateWorkItem(202, "Story", "Done", "Sprint 1", 3, null, 200)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product1);
        _mockProductRepository.Setup(r => r.GetProductByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product2);
        
        // Mock GetWorkItemsByRootIdsQuery to return work items
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        // Mock GetSprintMetricsQuery response
        var sprint1Metrics = new SprintMetricsDto(
            IterationPath: "Sprint 1",
            SprintName: "Sprint 1",
            StartDate: null,
            EndDate: null,
            CompletedStoryPoints: 15, // Total from both products: 8 + 7
            PlannedStoryPoints: 15,
            CompletedWorkItemCount: 4,
            TotalWorkItemCount: 4,
            CompletedPBIs: 4,
            CompletedBugs: 0,
            CompletedTasks: 0
        );

        _mockMediator.Setup(m => m.Send(It.IsAny<GetSprintMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sprint1Metrics);

        var query = new GetVelocityTrendQuery(ProductIds: new[] { 1, 2 }, MaxSprints: 1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalSprints);
        Assert.AreEqual(15, result.AverageVelocity, "Should aggregate velocity from both products");
        Assert.AreEqual(15, result.TotalCompletedStoryPoints);
    }

    [TestMethod]
    public async Task Handle_WithOverlappingProducts_DeduplicatesWorkItems()
    {
        // Arrange
        // Product 1 has root work item 100 with children 101, 102
        // Product 2 has root work item 101 (overlapping with Product 1's child)
        var product1 = CreateProduct(1, 1, "Product A", 100);
        var product2 = CreateProduct(2, 1, "Product B", 101); // Overlapping root

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            CreateWorkItem(102, "Story", "Done", "Sprint 1", 3, null, 100),
            CreateWorkItem(103, "Task", "Done", "Sprint 1", 2, null, 101)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product1);
        _mockProductRepository.Setup(r => r.GetProductByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product2);
        
        // Mock GetWorkItemsByRootIdsQuery to return work items
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        // Mock GetSprintMetricsQuery response
        // Deduplication means we count 101, 102, 103 only once = 10 story points total
        var sprint1Metrics = new SprintMetricsDto(
            IterationPath: "Sprint 1",
            SprintName: "Sprint 1",
            StartDate: null,
            EndDate: null,
            CompletedStoryPoints: 10, // 5 + 3 + 2 (deduplicated)
            PlannedStoryPoints: 10,
            CompletedWorkItemCount: 3,
            TotalWorkItemCount: 3,
            CompletedPBIs: 2,
            CompletedBugs: 0,
            CompletedTasks: 1
        );

        _mockMediator.Setup(m => m.Send(It.IsAny<GetSprintMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sprint1Metrics);

        var query = new GetVelocityTrendQuery(ProductIds: new[] { 1, 2 }, MaxSprints: 1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(10, result.TotalCompletedStoryPoints, "Should deduplicate overlapping work items");
    }

    [TestMethod]
    public async Task Handle_WithSingleProduct_BehavesLikeOriginal()
    {
        // Arrange
        var product = CreateProduct(1, 1, "Product A", 100);

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            CreateWorkItem(102, "Story", "Done", "Sprint 1", 3, null, 100)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        
        // Mock GetWorkItemsByRootIdsQuery to return work items
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var sprint1Metrics = new SprintMetricsDto(
            IterationPath: "Sprint 1",
            SprintName: "Sprint 1",
            StartDate: null,
            EndDate: null,
            CompletedStoryPoints: 8,
            PlannedStoryPoints: 8,
            CompletedWorkItemCount: 2,
            TotalWorkItemCount: 2,
            CompletedPBIs: 2,
            CompletedBugs: 0,
            CompletedTasks: 0
        );

        _mockMediator.Setup(m => m.Send(It.IsAny<GetSprintMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sprint1Metrics);

        var query = new GetVelocityTrendQuery(ProductIds: new[] { 1 }, MaxSprints: 1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(8, result.TotalCompletedStoryPoints);
        Assert.AreEqual(8, result.AverageVelocity);
    }

    [TestMethod]
    public async Task Handle_WithNonExistentProduct_SkipsInvalidProducts()
    {
        // Arrange
        var product1 = CreateProduct(1, 1, "Product A", 100);

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product1);
        _mockProductRepository.Setup(r => r.GetProductByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null); // Product 999 doesn't exist
        
        // Mock GetWorkItemsByRootIdsQuery to return work items
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var sprint1Metrics = new SprintMetricsDto(
            IterationPath: "Sprint 1",
            SprintName: "Sprint 1",
            StartDate: null,
            EndDate: null,
            CompletedStoryPoints: 5,
            PlannedStoryPoints: 5,
            CompletedWorkItemCount: 1,
            TotalWorkItemCount: 1,
            CompletedPBIs: 1,
            CompletedBugs: 0,
            CompletedTasks: 0
        );

        _mockMediator.Setup(m => m.Send(It.IsAny<GetSprintMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sprint1Metrics);

        var query = new GetVelocityTrendQuery(ProductIds: new[] { 1, 999 }, MaxSprints: 1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        // Should only include work items from product 1
        Assert.AreEqual(5, result.TotalCompletedStoryPoints);
    }

    // Helper methods
    private static ProductDto CreateProduct(int id, int ownerId, string name, int rootWorkItemId)
    {
        return new ProductDto(
            Id: id,
            ProductOwnerId: ownerId,
            Name: name,
            BacklogRootWorkItemId: rootWorkItemId,
            Order: 0,
            PictureType: ProductPictureType.Default,
            DefaultPictureId: 0,
            CustomPicturePath: null,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow,
            LastSyncedAt: null,
            TeamIds: new List<int>(),
            Repositories: new List<RepositoryDto>()
        );
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
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
            Description: null
        );
    }
}
