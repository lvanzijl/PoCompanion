using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Health;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Health;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetProductBacklogStateQueryHandlerTests
{
    private Mock<IProductRepository> _productRepository = null!;
    private Mock<IWorkItemQuery> _workItemQuery = null!;
    private Mock<IWorkItemStateClassificationService> _stateClassificationService = null!;
    private Mock<ILogger<GetProductBacklogStateQueryHandler>> _logger = null!;
    private BacklogStateComputationService _computationService = null!;
    private GetProductBacklogStateQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _productRepository = new Mock<IProductRepository>();
        _workItemQuery = new Mock<IWorkItemQuery>();
        _stateClassificationService = new Mock<IWorkItemStateClassificationService>();
        _logger = new Mock<ILogger<GetProductBacklogStateQueryHandler>>();
        _computationService = new BacklogStateComputationService();

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetStateClassificationsResponse
            {
                ProjectName = "Test",
                IsDefault = false,
                Classifications =
                [
                    new WorkItemStateClassificationDto
                    {
                        WorkItemType = WorkItemType.Pbi,
                        StateName = "Done",
                        Classification = StateClassification.Done
                    },
                    new WorkItemStateClassificationDto
                    {
                        WorkItemType = WorkItemType.Pbi,
                        StateName = "Removed",
                        Classification = StateClassification.Removed
                    }
                ]
            });

        _handler = new GetProductBacklogStateQueryHandler(
            _productRepository.Object,
            _workItemQuery.Object,
            _computationService,
            _stateClassificationService.Object,
            _logger.Object);
    }

    [TestMethod]
    public async Task Handle_WithConfiguredProduct_LoadsHierarchyFromQueryBoundary()
    {
        var product = CreateProduct(1, [1000]);
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1000, WorkItemType.Epic, "Epic"),
            CreateWorkItem(1001, WorkItemType.Feature, "Feature", 1000, "feature"),
            CreateWorkItem(1002, WorkItemType.Pbi, "Ready PBI", 1001, "pbi", effort: 8)
        };

        _productRepository
            .Setup(repository => repository.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _workItemQuery
            .Setup(query => query.GetByRootIdsAsync(
                It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1000 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _handler.Handle(new GetProductBacklogStateQuery(1), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.ProductId);
        Assert.HasCount(1, result.Epics);
        Assert.AreEqual("Epic", result.Epics[0].Title);
    }

    private static ProductDto CreateProduct(int id, IReadOnlyList<int> backlogRoots) =>
        new(
            Id: id,
            ProductOwnerId: 1,
            Name: $"Product {id}",
            BacklogRootWorkItemIds: backlogRoots.ToList(),
            Order: 0,
            PictureType: ProductPictureType.Default,
            DefaultPictureId: 0,
            CustomPicturePath: null,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow,
            LastSyncedAt: null,
            TeamIds: [],
            Repositories: []);

    private static WorkItemDto CreateWorkItem(
        int tfsId,
        string type,
        string title,
        int? parentId = null,
        string? description = null,
        int? effort = null) =>
        new(
            TfsId: tfsId,
            Type: type,
            Title: title,
            ParentTfsId: parentId,
            AreaPath: "\\Test",
            IterationPath: "\\Sprint 1",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
            Description: description,
            CreatedDate: DateTimeOffset.UtcNow);
}
