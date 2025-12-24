using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Commands;

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
        _mockTfsClient.Setup(t => t.UpdateWorkItemStateAsync(1, "In Progress", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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
        _mockTfsClient.Setup(t => t.UpdateWorkItemStateAsync(1, "In Progress", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

        var command = new FixValidationViolationBatchCommand(fixes);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalAttempted);
        Assert.AreEqual(0, result.SuccessfulFixes);
        Assert.AreEqual(1, result.FailedFixes);
        Assert.IsFalse(result.Results[0].Success);
        Assert.Contains(result.Results[0].Message, "not found");
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
        _mockTfsClient.Setup(t => t.UpdateWorkItemStateAsync(It.IsAny<int>(), "In Progress", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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
        
        _mockTfsClient.Setup(t => t.UpdateWorkItemStateAsync(1, "In Progress", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockTfsClient.Setup(t => t.UpdateWorkItemStateAsync(2, "In Progress", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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
    public async Task Handle_WithException_ContinuesProcessing()
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
        
        _mockTfsClient.Setup(t => t.UpdateWorkItemStateAsync(1, "In Progress", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("TFS error"));
        _mockTfsClient.Setup(t => t.UpdateWorkItemStateAsync(2, "In Progress", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new FixValidationViolationBatchCommand(fixes);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalAttempted);
        Assert.AreEqual(1, result.SuccessfulFixes);
        Assert.AreEqual(1, result.FailedFixes);
        Assert.IsFalse(result.Results[0].Success);
        Assert.IsTrue(result.Results[1].Success);
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
