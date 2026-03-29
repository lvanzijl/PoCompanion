using Mediator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Filters;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class SprintScopedWorkItemLoaderTests
{
    [TestMethod]
    public async Task LoadAsync_WithSelectedProducts_UsesBatchProductLookup()
    {
        var workItemReadProvider = new Mock<IWorkItemReadProvider>(MockBehavior.Strict);
        var productRepository = new Mock<IProductRepository>(MockBehavior.Strict);
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var loader = new SprintScopedWorkItemLoader(workItemReadProvider.Object, productRepository.Object, mediator.Object);

        var productA = CreateProduct(1, [100, 101]);
        var productB = CreateProduct(2, [200]);
        var expectedWorkItems = new[]
        {
            CreateWorkItem(100),
            CreateWorkItem(101),
            CreateWorkItem(200)
        };

        productRepository.Setup(repository => repository.GetProductsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1, 2 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([productA, productB]);
        mediator.Setup(m => m.Send(
                It.Is<GetWorkItemsByRootIdsQuery>(query => query.RootIds.SequenceEqual(new[] { 100, 101, 200 })),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<IEnumerable<WorkItemDto>>(expectedWorkItems));

        var result = await loader.LoadAsync(
            SprintFilterFactory.ForProductAndArea([1, 2], null),
            CancellationToken.None);

        Assert.HasCount(3, result);
        CollectionAssert.AreEqual(new[] { 100, 101, 200 }, result.Select(item => item.TfsId).ToArray());
        productRepository.Verify(repository => repository.GetProductsByIdsAsync(
            It.IsAny<IEnumerable<int>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        productRepository.Verify(repository => repository.GetProductByIdAsync(
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ProductDto CreateProduct(int id, List<int> rootIds)
        => new(
            Id: id,
            ProductOwnerId: 1,
            Name: $"Product {id}",
            BacklogRootWorkItemIds: rootIds,
            Order: id,
            PictureType: ProductPictureType.Default,
            DefaultPictureId: 0,
            CustomPicturePath: null,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow,
            LastSyncedAt: null,
            TeamIds: [],
            Repositories: [],
            EstimationMode: EstimationMode.StoryPoints);

    private static WorkItemDto CreateWorkItem(int tfsId)
        => new(
            TfsId: tfsId,
            Type: "Feature",
            Title: $"Work Item {tfsId}",
            ParentTfsId: null,
            AreaPath: "Area",
            IterationPath: "Sprint",
            State: "New",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null);
}
