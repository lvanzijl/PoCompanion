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
public class FixValidationViolationBatchCommandHandlerTests
{
    private Mock<ITfsClient> _mockTfsClient = null!;
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<ILogger<FixValidationViolationBatchCommandHandler>> _mockLogger = null!;
    private FixValidationViolationBatchCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTfsClient = new Mock<ITfsClient>();
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockLogger = new Mock<ILogger<FixValidationViolationBatchCommandHandler>>();
        
        _handler = new FixValidationViolationBatchCommandHandler(
            _mockTfsClient.Object,
            _mockRepository.Object,
            _mockLogger.Object
        );
    }

    [TestMethod]
    public async Task Handle_WithSuccessfulFix_ReturnsSuccess()
    {
        // Arrange
        var workItem = CreateWorkItem(1, "Goal", "New", null);
        var fixes = new List<FixValidationViolationDto>
        {
            new FixValidationViolationDto(
                WorkItemId: 1,
                FixType: "SetToInProgress",
                Description: "Set Goal to In Progress",
                NewState: "In Progress",
                Justification: "Required to unblock children"
            )
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);
        
        // Mock the bulk update method - uses new bulk API to prevent N+1
        _mockTfsClient.Setup(t => t.UpdateWorkItemsStateAsync(It.IsAny<IEnumerable<WorkItemStateUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUpdateResult(
                TotalRequested: 1,
                SuccessfulUpdates: 1,
                FailedUpdates: 0,
                Results: new List<BulkUpdateItemResult> { new BulkUpdateItemResult(1, true) },
                TfsCallCount: 1));

        var command = new FixValidationViolationBatchCommand(fixes);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalAttempted);
        Assert.AreEqual(1, result.SuccessfulFixes);
        Assert.AreEqual(0, result.FailedFixes);
        Assert.HasCount(1, result.Results);
        Assert.IsTrue(result.Results[0].Success);
    }

    [TestMethod]
    public async Task Handle_WithFailedTfsUpdate_ReturnsFailure()
    {
        // Arrange
        var workItem = CreateWorkItem(1, "Goal", "New", null);
        var fixes = new List<FixValidationViolationDto>
        {
            new FixValidationViolationDto(
                WorkItemId: 1,
                FixType: "SetToInProgress",
                Description: "Set Goal to In Progress",
                NewState: "In Progress",
                Justification: "Required to unblock children"
            )
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);
        
        // Mock the bulk update method returning failure
        _mockTfsClient.Setup(t => t.UpdateWorkItemsStateAsync(It.IsAny<IEnumerable<WorkItemStateUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUpdateResult(
                TotalRequested: 1,
                SuccessfulUpdates: 0,
                FailedUpdates: 1,
                Results: new List<BulkUpdateItemResult> { new BulkUpdateItemResult(1, false, "TFS update failed") },
                TfsCallCount: 1));

        var command = new FixValidationViolationBatchCommand(fixes);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalAttempted);
        Assert.AreEqual(0, result.SuccessfulFixes);
        Assert.AreEqual(1, result.FailedFixes);
        Assert.IsFalse(result.Results[0].Success);
    }

    [TestMethod]
    public async Task Handle_WithNonExistentWorkItem_ReturnsFailure()
    {
        // Arrange
        var fixes = new List<FixValidationViolationDto>
        {
            new FixValidationViolationDto(
                WorkItemId: 999,
                FixType: "SetToInProgress",
                Description: "Set Goal to In Progress",
                NewState: "In Progress",
                Justification: "Required to unblock children"
            )
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);
        
        // Mock bulk update returning empty result since nothing is valid
        _mockTfsClient.Setup(t => t.UpdateWorkItemsStateAsync(It.IsAny<IEnumerable<WorkItemStateUpdate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkUpdateResult(
                TotalRequested: 0,
                SuccessfulUpdates: 0,
                FailedUpdates: 0,
                Results: new List<BulkUpdateItemResult>(),
                TfsCallCount: 1));

        var command = new FixValidationViolationBatchCommand(fixes);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalAttempted);
        Assert.AreEqual(0, result.SuccessfulFixes);
        Assert.AreEqual(1, result.FailedFixes);
        Assert.IsFalse(result.Results[0].Success);
        Assert.Contains("not found", result.Results[0].Message);
    }

    [TestMethod]
    public async Task Handle_WithMultipleFixes_ProcessesAll()
    {
        // Arrange
        var workItem1 = CreateWorkItem(1, "Goal", "New", null);
        var workItem2 = CreateWorkItem(2, "Epic", "New", 1);
        
        var fixes = new List<FixValidationViolationDto>
        {
            new FixValidationViolationDto(
                WorkItemId: 1,
                FixType: "SetToInProgress",
                Description: "Set Goal to In Progress",
                NewState: "In Progress",
                Justification: "Required to unblock children"
            ),
            new FixValidationViolationDto(
                WorkItemId: 2,
                FixType: "SetToInProgress",
                Description: "Set Epic to In Progress",
                NewState: "In Progress",
                Justification: "Required to unblock children"
            )
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem1);
        _mockRepository.Setup(r => r.GetByTfsIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem2);
        
        // Mock the bulk update method - uses new bulk API to prevent N+1
        _mockTfsClient.Setup(t => t.UpdateWorkItemsStateAsync(It.IsAny<IEnumerable<WorkItemStateUpdate>>(), It.IsAny<CancellationToken>()))
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

        var command = new FixValidationViolationBatchCommand(fixes);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalAttempted);
        Assert.AreEqual(2, result.SuccessfulFixes);
        Assert.AreEqual(0, result.FailedFixes);
        Assert.HasCount(2, result.Results);
    }

    [TestMethod]
    public async Task Handle_WithMixedResults_ReturnsCorrectCounts()
    {
        // Arrange
        var workItem1 = CreateWorkItem(1, "Goal", "New", null);
        var workItem2 = CreateWorkItem(2, "Epic", "New", 1);
        
        var fixes = new List<FixValidationViolationDto>
        {
            new FixValidationViolationDto(1, "SetToInProgress", "Fix 1", "In Progress", "Reason 1"),
            new FixValidationViolationDto(2, "SetToInProgress", "Fix 2", "In Progress", "Reason 2")
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem1);
        _mockRepository.Setup(r => r.GetByTfsIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem2);
        
        // Mock the bulk update method returning mixed results
        _mockTfsClient.Setup(t => t.UpdateWorkItemsStateAsync(It.IsAny<IEnumerable<WorkItemStateUpdate>>(), It.IsAny<CancellationToken>()))
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

        var command = new FixValidationViolationBatchCommand(fixes);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalAttempted);
        Assert.AreEqual(1, result.SuccessfulFixes);
        Assert.AreEqual(1, result.FailedFixes);
    }

    [TestMethod]
    public async Task Handle_WithBulkUpdateException_HandlesGracefully()
    {
        // Arrange
        var workItem1 = CreateWorkItem(1, "Goal", "New", null);
        var workItem2 = CreateWorkItem(2, "Epic", "New", 1);
        
        var fixes = new List<FixValidationViolationDto>
        {
            new FixValidationViolationDto(1, "SetToInProgress", "Fix 1", "In Progress", "Reason 1"),
            new FixValidationViolationDto(2, "SetToInProgress", "Fix 2", "In Progress", "Reason 2")
        };

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem1);
        _mockRepository.Setup(r => r.GetByTfsIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem2);
        
        // Mock the bulk update method throwing an exception
        _mockTfsClient.Setup(t => t.UpdateWorkItemsStateAsync(It.IsAny<IEnumerable<WorkItemStateUpdate>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("TFS bulk update error"));

        var command = new FixValidationViolationBatchCommand(fixes);

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

    private static WorkItemDto CreateWorkItem(int id, string type, string state, int? parentId)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Test {type} {id}",
            ParentTfsId: parentId,
            AreaPath: "TestArea",
            IterationPath: "TestIteration",
            State: state,
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null
        );
    }
}
