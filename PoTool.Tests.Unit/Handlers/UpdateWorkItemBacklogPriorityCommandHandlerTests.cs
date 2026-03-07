using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class UpdateWorkItemBacklogPriorityCommandHandlerTests
{
    private Mock<ITfsClient> _mockTfsClient = null!;
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<ILogger<UpdateWorkItemBacklogPriorityCommandHandler>> _mockLogger = null!;
    private UpdateWorkItemBacklogPriorityCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTfsClient = new Mock<ITfsClient>();
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockLogger = new Mock<ILogger<UpdateWorkItemBacklogPriorityCommandHandler>>();

        _handler = new UpdateWorkItemBacklogPriorityCommandHandler(
            _mockTfsClient.Object,
            _mockRepository.Object,
            _mockLogger.Object
        );
    }

    [TestMethod]
    public async Task Handle_WhenTfsFails_ReturnsFalse()
    {
        // Arrange
        _mockTfsClient
            .Setup(t => t.UpdateWorkItemBacklogPriorityAsync(1, 500.0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(new UpdateWorkItemBacklogPriorityCommand(1, 500.0), CancellationToken.None);

        // Assert
        Assert.IsFalse(result);
        _mockRepository.Verify(r => r.UpsertManyAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_WhenTfsSucceeds_UpsertsCacheWithWrittenPriority()
    {
        // Arrange — TFS returns the original work item (with old BacklogPriority)
        var staleRefreshed = new WorkItemDto(
            TfsId: 10,
            Type: "Objective",
            Title: "Product A",
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null,
            BacklogPriority: null   // stale — TFS has not yet reflected the new value
        );

        _mockTfsClient
            .Setup(t => t.UpdateWorkItemBacklogPriorityAsync(10, 750.0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockTfsClient
            .Setup(t => t.GetWorkItemByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleRefreshed);

        WorkItemDto? upsertedItem = null;
        _mockRepository
            .Setup(r => r.UpsertManyAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<WorkItemDto>, CancellationToken>((items, _) => upsertedItem = items.First())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(new UpdateWorkItemBacklogPriorityCommand(10, 750.0), CancellationToken.None);

        // Assert
        Assert.IsTrue(result);
        Assert.IsNotNull(upsertedItem);
        Assert.AreEqual(750.0, upsertedItem!.BacklogPriority,
            "Cache must always contain the written BacklogPriority, even when TFS re-fetch returns stale data");
    }

    [TestMethod]
    public async Task Handle_WhenTfsRefetchReturnsNull_FallsBackToCachedEntity()
    {
        // Arrange — TFS write succeeds, but re-fetch returns null (e.g. transient failure)
        var cachedItem = new WorkItemDto(
            TfsId: 20,
            Type: "Objective",
            Title: "Product B",
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null,
            BacklogPriority: 250.0
        );

        _mockTfsClient
            .Setup(t => t.UpdateWorkItemBacklogPriorityAsync(20, 500.0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockTfsClient
            .Setup(t => t.GetWorkItemByIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);

        _mockRepository
            .Setup(r => r.GetByTfsIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedItem);

        WorkItemDto? upsertedItem = null;
        _mockRepository
            .Setup(r => r.UpsertManyAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<WorkItemDto>, CancellationToken>((items, _) => upsertedItem = items.First())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(new UpdateWorkItemBacklogPriorityCommand(20, 500.0), CancellationToken.None);

        // Assert — still returns true (TFS write succeeded) and cache is updated via fallback
        Assert.IsTrue(result);
        Assert.IsNotNull(upsertedItem);
        Assert.AreEqual(500.0, upsertedItem!.BacklogPriority,
            "Fallback path must update BacklogPriority in cache when TFS re-fetch is unavailable");
    }

    [TestMethod]
    public async Task Handle_WhenTfsRefetchNullAndNoCachedEntity_ReturnsTrue()
    {
        // Arrange — TFS write succeeds, re-fetch returns null, no cached entity either
        _mockTfsClient
            .Setup(t => t.UpdateWorkItemBacklogPriorityAsync(30, 1000.0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockTfsClient
            .Setup(t => t.GetWorkItemByIdAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);
        _mockRepository
            .Setup(r => r.GetByTfsIdAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);

        // Act
        var result = await _handler.Handle(new UpdateWorkItemBacklogPriorityCommand(30, 1000.0), CancellationToken.None);

        // Assert — TFS write succeeded so we return true even if cache refresh is incomplete
        Assert.IsTrue(result);
        _mockRepository.Verify(r => r.UpsertManyAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
