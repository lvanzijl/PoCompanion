using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Shared.Exceptions;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Tests for ValidateWorkItemQueryHandler.
/// These tests prove that validation calls TFS directly and does NOT use the cache.
/// </summary>
[TestClass]
public class ValidateWorkItemQueryHandlerTests
{
    private Mock<ITfsClient> _mockTfsClient = null!;
    private Mock<ILogger<ValidateWorkItemQueryHandler>> _mockLogger = null!;
    private ValidateWorkItemQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTfsClient = new Mock<ITfsClient>();
        _mockLogger = new Mock<ILogger<ValidateWorkItemQueryHandler>>();
        _handler = new ValidateWorkItemQueryHandler(_mockTfsClient.Object, _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithValidWorkItem_ReturnsSuccess()
    {
        // Arrange
        var workItemId = 12345;
        var expectedWorkItem = new WorkItemDto(
            TfsId: workItemId,
            Type: "Feature",
            Title: "Test Work Item",
            ParentTfsId: null,
            AreaPath: "Project\\Area",
            IterationPath: "Project\\Sprint 1",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null
        );

        _mockTfsClient.Setup(c => c.GetWorkItemByIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedWorkItem);

        var query = new ValidateWorkItemQuery(workItemId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Exists);
        Assert.AreEqual(workItemId, result.Id);
        Assert.AreEqual("Test Work Item", result.Title);
        Assert.AreEqual("Feature", result.Type);
        Assert.IsNull(result.ErrorMessage);

        // CRITICAL: Verify that TFS client was called directly (not cache)
        _mockTfsClient.Verify(
            c => c.GetWorkItemByIdAsync(workItemId, It.IsAny<CancellationToken>()),
            Times.Once,
            "ValidateWorkItemQueryHandler MUST call TFS client directly, not use cache");
    }

    [TestMethod]
    public async Task Handle_WithNonExistentWorkItem_ReturnsNotFound()
    {
        // Arrange
        var workItemId = 99999;
        _mockTfsClient.Setup(c => c.GetWorkItemByIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);

        var query = new ValidateWorkItemQuery(workItemId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Exists);
        Assert.AreEqual(workItemId, result.Id);
        Assert.IsNull(result.Title);
        Assert.IsNull(result.Type);
        Assert.IsNull(result.ErrorMessage);

        // CRITICAL: Verify that TFS client was called (proves we're not using stale cache)
        _mockTfsClient.Verify(
            c => c.GetWorkItemByIdAsync(workItemId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithTfsAuthenticationException_ReturnsAuthError()
    {
        // Arrange
        var workItemId = 12345;
        _mockTfsClient.Setup(c => c.GetWorkItemByIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TfsAuthenticationException("Not authorized to access TFS", errorContent: null));

        var query = new ValidateWorkItemQuery(workItemId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Exists);
        Assert.AreEqual(workItemId, result.Id);
        Assert.AreEqual("Not authorized to access TFS", result.ErrorMessage);

        // Verify that TFS client was called
        _mockTfsClient.Verify(
            c => c.GetWorkItemByIdAsync(workItemId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithHttpRequestException_ReturnsConnectionError()
    {
        // Arrange
        var workItemId = 12345;
        _mockTfsClient.Setup(c => c.GetWorkItemByIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unable to connect to TFS server"));

        var query = new ValidateWorkItemQuery(workItemId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Exists);
        Assert.AreEqual(workItemId, result.Id);
        Assert.AreEqual("Unable to connect to TFS server", result.ErrorMessage);

        // Verify that TFS client was called
        _mockTfsClient.Verify(
            c => c.GetWorkItemByIdAsync(workItemId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithGenericException_ReturnsGenericError()
    {
        // Arrange
        var workItemId = 12345;
        _mockTfsClient.Setup(c => c.GetWorkItemByIdAsync(workItemId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        var query = new ValidateWorkItemQuery(workItemId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Exists);
        Assert.AreEqual(workItemId, result.Id);
        Assert.AreEqual("Error validating work item: Unexpected error", result.ErrorMessage);

        // Verify that TFS client was called
        _mockTfsClient.Verify(
            c => c.GetWorkItemByIdAsync(workItemId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithMultipleValidations_CallsTfsDirectlyEachTime()
    {
        // This test proves that the handler doesn't cache results between calls
        // Arrange
        var workItemId1 = 111;
        var workItemId2 = 222;

        var workItem1 = new WorkItemDto(
            TfsId: workItemId1,
            Type: "Epic",
            Title: "Work Item 1",
            ParentTfsId: null,
            AreaPath: "Project\\Area",
            IterationPath: "Project\\Sprint 1",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null
        );

        var workItem2 = new WorkItemDto(
            TfsId: workItemId2,
            Type: "Feature",
            Title: "Work Item 2",
            ParentTfsId: null,
            AreaPath: "Project\\Area",
            IterationPath: "Project\\Sprint 1",
            State: "Active",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null
        );

        _mockTfsClient.Setup(c => c.GetWorkItemByIdAsync(workItemId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem1);
        _mockTfsClient.Setup(c => c.GetWorkItemByIdAsync(workItemId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem2);

        var query1 = new ValidateWorkItemQuery(workItemId1);
        var query2 = new ValidateWorkItemQuery(workItemId2);

        // Act
        var result1 = await _handler.Handle(query1, CancellationToken.None);
        var result2 = await _handler.Handle(query2, CancellationToken.None);

        // Assert
        Assert.IsTrue(result1.Exists);
        Assert.AreEqual("Work Item 1", result1.Title);
        Assert.IsTrue(result2.Exists);
        Assert.AreEqual("Work Item 2", result2.Title);

        // CRITICAL: Verify that TFS client was called for each validation (no caching)
        _mockTfsClient.Verify(
            c => c.GetWorkItemByIdAsync(workItemId1, It.IsAny<CancellationToken>()),
            Times.Once,
            "Each validation must call TFS directly");
        _mockTfsClient.Verify(
            c => c.GetWorkItemByIdAsync(workItemId2, It.IsAny<CancellationToken>()),
            Times.Once,
            "Each validation must call TFS directly");
    }
}
