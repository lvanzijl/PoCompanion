using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Commands;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class BulkAssignEffortCommandHandlerTests
{
    private Mock<ITfsClient> _mockTfsClient = null!;
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<ILogger<BulkAssignEffortCommandHandler>> _mockLogger = null!;
    private BulkAssignEffortCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTfsClient = new Mock<ITfsClient>();
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockLogger = new Mock<ILogger<BulkAssignEffortCommandHandler>>();

        _handler = new BulkAssignEffortCommandHandler(
            _mockTfsClient.Object,
            _mockRepository.Object,
            _mockLogger.Object
        );
    }

    [TestMethod]
    public async Task Handle_WithSuccessfulAssignment_ReturnsSuccess()
    {
        // Arrange
        var workItem = CreateWorkItem(1, "Task", "In Progress", null);
        var assignments = new List<BulkEffortAssignmentDto>
        {
            new BulkEffortAssignmentDto(WorkItemId: 1, EffortValue: 5)
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        // Mock the bulk update method - uses new bulk API to prevent N+1
        _mockTfsClient.Setup(t => t.UpdateWorkItemsEffortAsync(It.IsAny<IEnumerable<WorkItemEffortUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUpdateResult(
                TotalRequested: 1,
                SuccessfulUpdates: 1,
                FailedUpdates: 0,
                Results: new List<BulkUpdateItemResult> { new BulkUpdateItemResult(1, true) },
                TfsCallCount: 1));

        var command = new BulkAssignEffortCommand(assignments);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalRequested);
        Assert.AreEqual(1, result.SuccessfulUpdates);
        Assert.AreEqual(0, result.FailedUpdates);
        Assert.HasCount(1, result.Results);
        Assert.IsTrue(result.Results[0].Success);
    }

    [TestMethod]
    public async Task Handle_WithFailedTfsUpdate_ReturnsFailure()
    {
        // Arrange
        var workItem = CreateWorkItem(1, "Task", "In Progress", null);
        var assignments = new List<BulkEffortAssignmentDto>
        {
            new BulkEffortAssignmentDto(WorkItemId: 1, EffortValue: 5)
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        // Mock the bulk update method returning failure
        _mockTfsClient.Setup(t => t.UpdateWorkItemsEffortAsync(It.IsAny<IEnumerable<WorkItemEffortUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUpdateResult(
                TotalRequested: 1,
                SuccessfulUpdates: 0,
                FailedUpdates: 1,
                Results: new List<BulkUpdateItemResult> { new BulkUpdateItemResult(1, false, "TFS update failed") },
                TfsCallCount: 1));

        var command = new BulkAssignEffortCommand(assignments);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalRequested);
        Assert.AreEqual(0, result.SuccessfulUpdates);
        Assert.AreEqual(1, result.FailedUpdates);
        Assert.IsFalse(result.Results[0].Success);
    }

    [TestMethod]
    public async Task Handle_WithNonExistentWorkItem_ReturnsFailure()
    {
        // Arrange
        var assignments = new List<BulkEffortAssignmentDto>
        {
            new BulkEffortAssignmentDto(WorkItemId: 999, EffortValue: 5)
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);

        // Mock bulk update returning empty result since nothing is valid
        _mockTfsClient.Setup(t => t.UpdateWorkItemsEffortAsync(It.IsAny<IEnumerable<WorkItemEffortUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUpdateResult(
                TotalRequested: 0,
                SuccessfulUpdates: 0,
                FailedUpdates: 0,
                Results: new List<BulkUpdateItemResult>(),
                TfsCallCount: 1));

        var command = new BulkAssignEffortCommand(assignments);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalRequested);
        Assert.AreEqual(0, result.SuccessfulUpdates);
        Assert.AreEqual(1, result.FailedUpdates);
        Assert.IsFalse(result.Results[0].Success);
        Assert.Contains("not found", result.Results[0].ErrorMessage!);
    }

    [TestMethod]
    public async Task Handle_WithNegativeEffort_ReturnsFailure()
    {
        // Arrange
        var workItem = CreateWorkItem(1, "Task", "In Progress", null);
        var assignments = new List<BulkEffortAssignmentDto>
        {
            new BulkEffortAssignmentDto(WorkItemId: 1, EffortValue: -5)
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        // Mock bulk update returning empty result since negative effort is filtered out during validation
        _mockTfsClient.Setup(t => t.UpdateWorkItemsEffortAsync(It.IsAny<IEnumerable<WorkItemEffortUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUpdateResult(
                TotalRequested: 0,
                SuccessfulUpdates: 0,
                FailedUpdates: 0,
                Results: new List<BulkUpdateItemResult>(),
                TfsCallCount: 1));

        var command = new BulkAssignEffortCommand(assignments);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalRequested);
        Assert.AreEqual(0, result.SuccessfulUpdates);
        Assert.AreEqual(1, result.FailedUpdates);
        Assert.IsFalse(result.Results[0].Success);
        Assert.Contains("Invalid effort", result.Results[0].ErrorMessage!);
    }

    [TestMethod]
    public async Task Handle_WithMultipleAssignments_ProcessesAll()
    {
        // Arrange
        var workItem1 = CreateWorkItem(1, "Task", "In Progress", null);
        var workItem2 = CreateWorkItem(2, "Task", "In Progress", null);

        var assignments = new List<BulkEffortAssignmentDto>
        {
            new BulkEffortAssignmentDto(WorkItemId: 1, EffortValue: 3),
            new BulkEffortAssignmentDto(WorkItemId: 2, EffortValue: 5)
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem1);
        _mockRepository.Setup(r => r.GetByTfsIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem2);

        // Mock the bulk update method - uses new bulk API to prevent N+1
        _mockTfsClient.Setup(t => t.UpdateWorkItemsEffortAsync(It.IsAny<IEnumerable<WorkItemEffortUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUpdateResult(
                TotalRequested: 2,
                SuccessfulUpdates: 2,
                FailedUpdates: 0,
                Results: new List<BulkUpdateItemResult>
                {
                    new BulkUpdateItemResult(1, true),
                    new BulkUpdateItemResult(2, true)
                },
                TfsCallCount: 1));

        var command = new BulkAssignEffortCommand(assignments);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalRequested);
        Assert.AreEqual(2, result.SuccessfulUpdates);
        Assert.AreEqual(0, result.FailedUpdates);
        Assert.HasCount(2, result.Results);
    }

    [TestMethod]
    public async Task Handle_WithMixedResults_ReturnsCorrectCounts()
    {
        // Arrange
        var workItem1 = CreateWorkItem(1, "Task", "In Progress", null);
        var workItem2 = CreateWorkItem(2, "Task", "In Progress", null);

        var assignments = new List<BulkEffortAssignmentDto>
        {
            new BulkEffortAssignmentDto(WorkItemId: 1, EffortValue: 3),
            new BulkEffortAssignmentDto(WorkItemId: 2, EffortValue: 5)
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem1);
        _mockRepository.Setup(r => r.GetByTfsIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem2);

        // Mock the bulk update method returning mixed results
        _mockTfsClient.Setup(t => t.UpdateWorkItemsEffortAsync(It.IsAny<IEnumerable<WorkItemEffortUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUpdateResult(
                TotalRequested: 2,
                SuccessfulUpdates: 1,
                FailedUpdates: 1,
                Results: new List<BulkUpdateItemResult>
                {
                    new BulkUpdateItemResult(1, true),
                    new BulkUpdateItemResult(2, false, "TFS update failed")
                },
                TfsCallCount: 1));

        var command = new BulkAssignEffortCommand(assignments);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalRequested);
        Assert.AreEqual(1, result.SuccessfulUpdates);
        Assert.AreEqual(1, result.FailedUpdates);
    }

    [TestMethod]
    public async Task Handle_WithBulkUpdateException_HandlesGracefully()
    {
        // Arrange
        var workItem1 = CreateWorkItem(1, "Task", "In Progress", null);
        var workItem2 = CreateWorkItem(2, "Task", "In Progress", null);

        var assignments = new List<BulkEffortAssignmentDto>
        {
            new BulkEffortAssignmentDto(WorkItemId: 1, EffortValue: 3),
            new BulkEffortAssignmentDto(WorkItemId: 2, EffortValue: 5)
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem1);
        _mockRepository.Setup(r => r.GetByTfsIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem2);

        // Mock the bulk update method throwing an exception
        _mockTfsClient.Setup(t => t.UpdateWorkItemsEffortAsync(It.IsAny<IEnumerable<WorkItemEffortUpdate>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("TFS bulk update error"));

        var command = new BulkAssignEffortCommand(assignments);

        // Act & Assert - should throw since bulk update failure is not recoverable
        var thrown = false;
        try
        {
            await _handler.Handle(command, CancellationToken.None);
        }
        catch (Exception)
        {
            thrown = true;
        }
        Assert.IsTrue(thrown, "Expected exception to be thrown");
    }

    [TestMethod]
    public async Task Handle_WithZeroEffort_Succeeds()
    {
        // Arrange
        var workItem = CreateWorkItem(1, "Task", "In Progress", null);
        var assignments = new List<BulkEffortAssignmentDto>
        {
            new BulkEffortAssignmentDto(WorkItemId: 1, EffortValue: 0)
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        // Mock the bulk update method
        _mockTfsClient.Setup(t => t.UpdateWorkItemsEffortAsync(It.IsAny<IEnumerable<WorkItemEffortUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUpdateResult(
                TotalRequested: 1,
                SuccessfulUpdates: 1,
                FailedUpdates: 0,
                Results: new List<BulkUpdateItemResult> { new BulkUpdateItemResult(1, true) },
                TfsCallCount: 1));

        var command = new BulkAssignEffortCommand(assignments);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalRequested);
        Assert.AreEqual(1, result.SuccessfulUpdates);
        Assert.AreEqual(0, result.FailedUpdates);
        Assert.IsTrue(result.Results[0].Success);
    }

    private static WorkItemDto CreateWorkItem(int id, string type, string state, int? effort)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Test {type} {id}",
            ParentTfsId: null,
            AreaPath: "TestArea",
            IterationPath: "TestIteration",
            State: state,
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort
        );
    }
}
