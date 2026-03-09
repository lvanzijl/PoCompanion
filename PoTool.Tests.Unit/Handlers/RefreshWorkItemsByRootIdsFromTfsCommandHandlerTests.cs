using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class RefreshWorkItemsByRootIdsFromTfsCommandHandlerTests
{
    private Mock<ITfsClient> _mockTfsClient = null!;
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<ILogger<RefreshWorkItemsByRootIdsFromTfsCommandHandler>> _mockLogger = null!;
    private RefreshWorkItemsByRootIdsFromTfsCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTfsClient = new Mock<ITfsClient>();
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockLogger = new Mock<ILogger<RefreshWorkItemsByRootIdsFromTfsCommandHandler>>();

        _handler = new RefreshWorkItemsByRootIdsFromTfsCommandHandler(
            _mockTfsClient.Object,
            _mockRepository.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WhenHierarchyReturned_UpsertsAllWorkItemsAndReturnsCount()
    {
        var workItems = new[]
        {
            CreateWorkItem(100, "Epic", "Board Epic"),
            CreateWorkItem(101, "Feature", "Board Feature", parentId: 100),
            CreateWorkItem(102, "Product Backlog Item", "Board PBI", parentId: 101)
        };

        _mockTfsClient
            .Setup(t => t.GetWorkItemsByRootIdsAsync(new[] { 100 }, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        IEnumerable<WorkItemDto>? upsertedItems = null;
        _mockRepository
            .Setup(r => r.UpsertManyAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<WorkItemDto>, CancellationToken>((items, _) => upsertedItems = items.ToList())
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(new RefreshWorkItemsByRootIdsFromTfsCommand(new[] { 100 }), CancellationToken.None);

        Assert.AreEqual(3, result);
        Assert.IsNotNull(upsertedItems);
        CollectionAssert.AreEquivalent(workItems.Select(w => w.TfsId).ToList(), upsertedItems!.Select(w => w.TfsId).ToList());
    }

    [TestMethod]
    public async Task Handle_WhenTfsReturnsNoWorkItems_DoesNotUpsertAndReturnsZero()
    {
        _mockTfsClient
            .Setup(t => t.GetWorkItemsByRootIdsAsync(new[] { 100 }, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WorkItemDto>());

        var result = await _handler.Handle(new RefreshWorkItemsByRootIdsFromTfsCommand(new[] { 100 }), CancellationToken.None);

        Assert.AreEqual(0, result);
        _mockRepository.Verify(r => r.UpsertManyAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static WorkItemDto CreateWorkItem(int tfsId, string type, string title, int? parentId = null)
    {
        return new WorkItemDto(
            TfsId: tfsId,
            Type: type,
            Title: title,
            ParentTfsId: parentId,
            AreaPath: "Project\\Team",
            IterationPath: "Project",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null,
            BacklogPriority: null);
    }
}
