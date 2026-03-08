using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Commands;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class UpdateWorkItemIterationPathCommandHandlerTests
{
    private Mock<ITfsClient> _mockTfsClient = null!;
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<ILogger<UpdateWorkItemIterationPathCommandHandler>> _mockLogger = null!;
    private UpdateWorkItemIterationPathCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTfsClient = new Mock<ITfsClient>();
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockLogger = new Mock<ILogger<UpdateWorkItemIterationPathCommandHandler>>();

        _handler = new UpdateWorkItemIterationPathCommandHandler(
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
            .Setup(t => t.UpdateWorkItemIterationPathAsync(1, "Project\\Sprint 1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(new UpdateWorkItemIterationPathCommand(1, "Project\\Sprint 1"), CancellationToken.None);

        // Assert
        Assert.IsFalse(result);
        _mockRepository.Verify(r => r.UpsertManyAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_WhenTfsSucceeds_UpsertsCacheWithWrittenIterationPath()
    {
        // Arrange — TFS returns the original work item (with old IterationPath)
        var staleRefreshed = new WorkItemDto(
            TfsId: 10,
            Type: "Feature",
            Title: "My Feature",
            ParentTfsId: 5,
            AreaPath: "Project\\Team",
            IterationPath: "Project",  // stale — TFS has not yet reflected the new value
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: 8,
            Description: null,
            BacklogPriority: null
        );

        _mockTfsClient
            .Setup(t => t.UpdateWorkItemIterationPathAsync(10, "Project\\Sprint 2", It.IsAny<CancellationToken>()))
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
        var result = await _handler.Handle(new UpdateWorkItemIterationPathCommand(10, "Project\\Sprint 2"), CancellationToken.None);

        // Assert
        Assert.IsTrue(result);
        Assert.IsNotNull(upsertedItem);
        Assert.AreEqual("Project\\Sprint 2", upsertedItem!.IterationPath,
            "Cache must always contain the written IterationPath, even when TFS re-fetch returns stale data");
    }

    [TestMethod]
    public async Task Handle_WhenTfsRefetchReturnsNull_FallsBackToCachedEntity()
    {
        // Arrange — TFS write succeeds, but re-fetch returns null (e.g. transient failure)
        var cachedItem = new WorkItemDto(
            TfsId: 20,
            Type: "Feature",
            Title: "Another Feature",
            ParentTfsId: 6,
            AreaPath: "Project\\Team",
            IterationPath: "Project\\Sprint 1",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: 5,
            Description: null,
            BacklogPriority: null
        );

        _mockTfsClient
            .Setup(t => t.UpdateWorkItemIterationPathAsync(20, "Project\\Sprint 3", It.IsAny<CancellationToken>()))
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
        var result = await _handler.Handle(new UpdateWorkItemIterationPathCommand(20, "Project\\Sprint 3"), CancellationToken.None);

        // Assert — still returns true (TFS write succeeded) and cache is updated via fallback
        Assert.IsTrue(result);
        Assert.IsNotNull(upsertedItem);
        Assert.AreEqual("Project\\Sprint 3", upsertedItem!.IterationPath,
            "Fallback path must update IterationPath in cache when TFS re-fetch is unavailable");
    }

    [TestMethod]
    public async Task Handle_WhenTfsRefetchNullAndNoCachedEntity_ReturnsTrue()
    {
        // Arrange — TFS write succeeds, re-fetch returns null, no cached entity either
        _mockTfsClient
            .Setup(t => t.UpdateWorkItemIterationPathAsync(30, "Project\\Sprint 1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockTfsClient
            .Setup(t => t.GetWorkItemByIdAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);
        _mockRepository
            .Setup(r => r.GetByTfsIdAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);

        // Act
        var result = await _handler.Handle(new UpdateWorkItemIterationPathCommand(30, "Project\\Sprint 1"), CancellationToken.None);

        // Assert — TFS write succeeded so we return true even if cache refresh is incomplete
        Assert.IsTrue(result);
        _mockRepository.Verify(r => r.UpsertManyAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
