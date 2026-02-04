using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetAllWorkItemsWithValidationQueryHandlerTests
{
    private Mock<IWorkItemReadProvider> _mockProvider = null!;
    private Mock<IWorkItemValidator> _mockValidator = null!;
    private ProfileFilterService _profileFilterService = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<ILogger<GetAllWorkItemsWithValidationQueryHandler>> _mockLogger = null!;
    private GetAllWorkItemsWithValidationQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockProvider = new Mock<IWorkItemReadProvider>();
        _mockValidator = new Mock<IWorkItemValidator>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockLogger = new Mock<ILogger<GetAllWorkItemsWithValidationQueryHandler>>();

        // Setup ProfileFilterService with its dependencies
        var mockSettingsRepository = new Mock<ISettingsRepository>();
        var mockProfileRepository = new Mock<IProfileRepository>();
        var mockProfileFilterLogger = new Mock<ILogger<ProfileFilterService>>();
        _profileFilterService = new ProfileFilterService(
            mockSettingsRepository.Object,
            mockProfileRepository.Object,
            mockProfileFilterLogger.Object);

        // Setup default mock behaviors
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());

        _handler = new GetAllWorkItemsWithValidationQueryHandler(
            _mockProvider.Object,
            _mockValidator.Object,
            _profileFilterService,
            _mockProductRepository.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoProductIds_LoadsAllProducts()
    {
        // Arrange
        var products = new List<ProductDto>
        {
            CreateProduct(1, "Product A", 100),
            CreateProduct(2, "Product B", 200),
            CreateProduct(3, "Product C", 300)
        };

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Product A Root"),
            CreateWorkItem(200, "Epic", "Product B Root"),
            CreateWorkItem(300, "Epic", "Product C Root")
        };

        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
        _mockProvider.Setup(p => p.GetByRootIdsAsync(
                It.Is<int[]>(ids => ids.Length == 3 && ids.Contains(100) && ids.Contains(200) && ids.Contains(300)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var query = new GetAllWorkItemsWithValidationQuery(null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count());
        _mockProductRepository.Verify(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockProvider.Verify(p => p.GetByRootIdsAsync(
            It.Is<int[]>(ids => ids.Length == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithSpecificProductIds_LoadsOnlyThoseProducts()
    {
        // Arrange
        var allProducts = new List<ProductDto>
        {
            CreateProduct(1, "Product A", 100),
            CreateProduct(2, "Product B", 200),
            CreateProduct(3, "Product C", 300)
        };

        var workItemsForProductA = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Product A Root"),
            CreateWorkItem(101, "Feature", "Feature 1")
        };

        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allProducts);
        _mockProvider.Setup(p => p.GetByRootIdsAsync(
                It.Is<int[]>(ids => ids.Length == 1 && ids.Contains(100)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItemsForProductA);

        // Query with only Product A's ID
        var query = new GetAllWorkItemsWithValidationQuery(new[] { 1 });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count());
        _mockProductRepository.Verify(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockProvider.Verify(p => p.GetByRootIdsAsync(
            It.Is<int[]>(ids => ids.Length == 1 && ids[0] == 100),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithMultipleProductIds_LoadsCorrectProducts()
    {
        // Arrange
        var allProducts = new List<ProductDto>
        {
            CreateProduct(1, "Product A", 100),
            CreateProduct(2, "Product B", 200),
            CreateProduct(3, "Product C", 300)
        };

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Product A Root"),
            CreateWorkItem(200, "Epic", "Product B Root")
        };

        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allProducts);
        _mockProvider.Setup(p => p.GetByRootIdsAsync(
                It.Is<int[]>(ids => ids.Length == 2 && ids.Contains(100) && ids.Contains(200)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        // Query with Product A and B IDs
        var query = new GetAllWorkItemsWithValidationQuery(new[] { 1, 2 });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count());
        _mockProvider.Verify(p => p.GetByRootIdsAsync(
            It.Is<int[]>(ids => ids.Length == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_AttachesValidationResults()
    {
        // Arrange
        var products = new List<ProductDto>
        {
            CreateProduct(1, "Product A", 100)
        };

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Product A Root"),
            CreateWorkItem(101, "Feature", "Feature 1")
        };

        var validationIssues = new Dictionary<int, List<ValidationIssue>>
        {
            { 101, new List<ValidationIssue> { new ValidationIssue("Warning", "Missing effort") } }
        };

        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
        _mockProvider.Setup(p => p.GetByRootIdsAsync(It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationIssues);

        var query = new GetAllWorkItemsWithValidationQuery(new[] { 1 });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var resultList = result.ToList();
        Assert.HasCount(2, resultList);
        
        var itemWithIssue = resultList.First(wi => wi.TfsId == 101);
        Assert.HasCount(1, itemWithIssue.ValidationIssues);
        Assert.AreEqual("Missing effort", itemWithIssue.ValidationIssues[0].Message);
        
        var itemWithoutIssue = resultList.First(wi => wi.TfsId == 100);
        Assert.IsEmpty(itemWithoutIssue.ValidationIssues);
    }

    private static ProductDto CreateProduct(int id, string name, int backlogRootWorkItemId)
    {
        return new ProductDto(
            Id: id,
            ProductOwnerId: 1,
            Name: name,
            BacklogRootWorkItemId: backlogRootWorkItemId,
            Order: id,
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

    private static WorkItemDto CreateWorkItem(int tfsId, string type, string title)
    {
        return new WorkItemDto(
            TfsId: tfsId,
            Type: type,
            Title: title,
            ParentTfsId: null,
            AreaPath: "Test",
            IterationPath: "Sprint 1",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null,
            Tags: null
        );
    }
}
