using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Services;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Validators;
using PoTool.Shared.Settings;

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
    private Mock<IHierarchicalWorkItemValidator> _mockValidator = null!;
    private Mock<ILogger<GetMultiIterationBacklogHealthQueryHandler>> _mockLogger = null!;
    private GetMultiIterationBacklogHealthQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockProvider = new Mock<IWorkItemReadProvider>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockSprintRepository = new Mock<ISprintRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockValidator = new Mock<IHierarchicalWorkItemValidator>();
        _mockLogger = new Mock<ILogger<GetMultiIterationBacklogHealthQueryHandler>>();

        // Setup default mock behaviors
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        
        // Setup default sprint data to ensure tests work with date-based selection
        // Create sprints that span past, current, and future periods
        var now = DateTimeOffset.UtcNow;
        var defaultSprints = new List<SprintDto>
        {
            // Past sprints
            new SprintDto(
                Id: 1,
                TeamId: 1,
                TfsIterationId: "past3-guid",
                Path: "Past Sprint 3",
                Name: "Past Sprint 3",
                StartUtc: now.AddDays(-90),
                EndUtc: now.AddDays(-76),
                TimeFrame: "past",
                LastSyncedUtc: now
            ),
            new SprintDto(
                Id: 2,
                TeamId: 1,
                TfsIterationId: "past2-guid",
                Path: "Past Sprint 2",
                Name: "Past Sprint 2",
                StartUtc: now.AddDays(-75),
                EndUtc: now.AddDays(-61),
                TimeFrame: "past",
                LastSyncedUtc: now
            ),
            new SprintDto(
                Id: 3,
                TeamId: 1,
                TfsIterationId: "past1-guid",
                Path: "Past Sprint 1",
                Name: "Past Sprint 1",
                StartUtc: now.AddDays(-60),
                EndUtc: now.AddDays(-46),
                TimeFrame: "past",
                LastSyncedUtc: now
            ),
            // Current sprint (Sprint 1 - where test work items are)
            new SprintDto(
                Id: 4,
                TeamId: 1,
                TfsIterationId: "sprint1-guid",
                Path: "Sprint 1",
                Name: "Sprint 1",
                StartUtc: now.AddDays(-45),
                EndUtc: now.AddDays(1),
                TimeFrame: "current",
                LastSyncedUtc: now
            ),
            // Future sprints
            new SprintDto(
                Id: 5,
                TeamId: 1,
                TfsIterationId: "future1-guid",
                Path: "Future Sprint 1",
                Name: "Future Sprint 1",
                StartUtc: now.AddDays(2),
                EndUtc: now.AddDays(16),
                TimeFrame: "future",
                LastSyncedUtc: now
            ),
            new SprintDto(
                Id: 6,
                TeamId: 1,
                TfsIterationId: "future2-guid",
                Path: "Future Sprint 2",
                Name: "Future Sprint 2",
                StartUtc: now.AddDays(17),
                EndUtc: now.AddDays(31),
                TimeFrame: "future",
                LastSyncedUtc: now
            )
        };
        _mockSprintRepository.Setup(r => r.GetAllSprintsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultSprints);

        _handler = new GetMultiIterationBacklogHealthQueryHandler(
            _mockProvider.Object,
            _mockProductRepository.Object,
            _mockSprintRepository.Object,
            _mockMediator.Object,
            _mockValidator.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithTwoDisjointProducts_ReturnsCumulativeTotals()
    {
        // Arrange
        // Product 1 has root work item 100 with 2 children (101, 102)
        // Product 2 has root work item 200 with 2 children (201, 202)
        var product1 = CreateProduct(1, 1, "Product A", 100);
        var product2 = CreateProduct(2, 1, "Product B", 200);

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            CreateWorkItem(102, "Story", "In Progress", "Sprint 1", null, null, 100), // Missing effort

            CreateWorkItem(200, "Epic", "Active", "Sprint 1", null, 20, null),
            CreateWorkItem(201, "Story", "Done", "Sprint 1", 3, null, 200),
            CreateWorkItem(202, "Story", "New", "Sprint 1", null, null, 200) // Missing effort
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product1);
        _mockProductRepository.Setup(r => r.GetProductByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product2);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(Array.Empty<HierarchicalValidationResult>());

        var query = new GetMultiIterationBacklogHealthQuery(ProductIds: new[] { 1, 2 }, MaxIterations: 5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IterationHealth.Any());
        
        var healthForSprint1 = result.IterationHealth.First();
        Assert.AreEqual(6, healthForSprint1.TotalWorkItems, "Should count all work items from both products");
        // Epics 100 and 200 have null effort, plus items 102 and 202 = 4 items without effort
        Assert.AreEqual(4, healthForSprint1.WorkItemsWithoutEffort, "Should count 4 items without effort (100, 200, 102, 202)");
        Assert.AreEqual(1, healthForSprint1.WorkItemsInProgressWithoutEffort, "Should count only item 102");
        
        // Totals should be cumulative
        Assert.AreEqual(6, result.TotalWorkItems);
    }

    [TestMethod]
    public async Task Handle_WithOverlappingProducts_DeduplicatesWorkItems()
    {
        // Arrange
        // Product 1 has root work item 100 with children 101, 102
        // Product 2 has root work item 101 (overlapping with Product 1's child)
        // This simulates shared work items between products
        var product1 = CreateProduct(1, 1, "Product A", 100);
        var product2 = CreateProduct(2, 1, "Product B", 101); // Overlapping root

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Active", "Sprint 1", null, 10, null),
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            CreateWorkItem(102, "Story", "In Progress", "Sprint 1", null, null, 100), // Missing effort
            CreateWorkItem(103, "Task", "New", "Sprint 1", 2, null, 101)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product1);
        _mockProductRepository.Setup(r => r.GetProductByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product2);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(Array.Empty<HierarchicalValidationResult>());

        var query = new GetMultiIterationBacklogHealthQuery(ProductIds: new[] { 1, 2 }, MaxIterations: 5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IterationHealth.Any());
        
        var healthForSprint1 = result.IterationHealth.First();
        // Work items 101 and 103 appear in product 2, but also 100-103 appear in product 1
        // Deduplication should give us 4 unique work items (100, 101, 102, 103)
        Assert.AreEqual(4, healthForSprint1.TotalWorkItems, "Should deduplicate overlapping work items by TfsId");
        // Items without effort: 100 (Epic, null), 102 (Story, null) = 2 items
        Assert.AreEqual(2, healthForSprint1.WorkItemsWithoutEffort, "Should count 100 and 102 without effort");
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
            CreateWorkItem(102, "Story", "In Progress", "Sprint 1", null, null, 100)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        // Mock the mediator to return work items for the product hierarchy
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(Array.Empty<HierarchicalValidationResult>());

        var query = new GetMultiIterationBacklogHealthQuery(ProductIds: new[] { 1 }, MaxIterations: 5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IterationHealth.Any());
        
        var healthForSprint1 = result.IterationHealth.First();
        Assert.AreEqual(3, healthForSprint1.TotalWorkItems);
        // Items without effort: 100 (Epic, null), 102 (Story, null) = 2 items
        Assert.AreEqual(2, healthForSprint1.WorkItemsWithoutEffort);
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
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(Array.Empty<HierarchicalValidationResult>());

        var query = new GetMultiIterationBacklogHealthQuery(ProductIds: new[] { 1, 999 }, MaxIterations: 5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IterationHealth.Any());
        
        // Should only include work items from product 1
        var healthForSprint1 = result.IterationHealth.First();
        Assert.AreEqual(2, healthForSprint1.TotalWorkItems);
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
