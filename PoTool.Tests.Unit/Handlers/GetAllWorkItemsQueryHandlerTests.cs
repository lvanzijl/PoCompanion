using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetAllWorkItemsQueryHandlerTests
{
    private Mock<IWorkItemQuery> _workItemQuery = null!;
    private ProfileFilterService _profileFilterService = null!;
    private Mock<IProductRepository> _productRepository = null!;
    private Mock<ILogger<GetAllWorkItemsQueryHandler>> _logger = null!;
    private GetAllWorkItemsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _workItemQuery = new Mock<IWorkItemQuery>();
        _productRepository = new Mock<IProductRepository>();
        _logger = new Mock<ILogger<GetAllWorkItemsQueryHandler>>();

        var settingsRepository = new Mock<ISettingsRepository>();
        var profileRepository = new Mock<IProfileRepository>();
        var profileLogger = new Mock<ILogger<ProfileFilterService>>();
        _profileFilterService = new ProfileFilterService(
            settingsRepository.Object,
            profileRepository.Object,
            profileLogger.Object);

        _handler = new GetAllWorkItemsQueryHandler(
            _workItemQuery.Object,
            _profileFilterService,
            _productRepository.Object,
            _logger.Object);
    }

    [TestMethod]
    public async Task Handle_WithConfiguredProductRoots_LoadsHierarchyFromQueryBoundary()
    {
        var products = new List<ProductDto>
        {
            CreateProduct(1, 101),
            CreateProduct(2, 202)
        };
        var expected = new List<WorkItemDto>
        {
            CreateWorkItem(101, "Epic"),
            CreateWorkItem(202, "Epic")
        };

        _productRepository
            .Setup(repository => repository.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
        _workItemQuery
            .Setup(query => query.GetByRootIdsAsync(
                It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 101, 202 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = (await _handler.Handle(new GetAllWorkItemsQuery(), CancellationToken.None)).ToList();

        Assert.HasCount(2, result);
        _workItemQuery.Verify(query => query.GetByRootIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()), Times.Once);
        _workItemQuery.Verify(query => query.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_WithoutProducts_LoadsAllCachedItems()
    {
        var expected = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic")
        };

        _productRepository
            .Setup(repository => repository.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _workItemQuery
            .Setup(query => query.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = (await _handler.Handle(new GetAllWorkItemsQuery(), CancellationToken.None)).ToList();

        Assert.HasCount(1, result);
        _workItemQuery.Verify(query => query.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ProductDto CreateProduct(int id, int backlogRootWorkItemId) =>
        new(
            Id: id,
            ProductOwnerId: 1,
            Name: $"Product {id}",
            BacklogRootWorkItemIds: [backlogRootWorkItemId],
            Order: id,
            PictureType: ProductPictureType.Default,
            DefaultPictureId: 0,
            CustomPicturePath: null,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow,
            LastSyncedAt: null,
            TeamIds: [],
            Repositories: []);

    private static WorkItemDto CreateWorkItem(int tfsId, string type) =>
        new(
            TfsId: tfsId,
            Type: type,
            Title: $"Item {tfsId}",
            ParentTfsId: null,
            AreaPath: "Area",
            IterationPath: "Iteration",
            State: "New",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null);
}
