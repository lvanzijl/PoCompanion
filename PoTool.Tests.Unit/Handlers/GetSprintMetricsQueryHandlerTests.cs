using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetSprintMetricsQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<ILogger<GetSprintMetricsQueryHandler>> _mockLogger = null!;
    private GetSprintMetricsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockLogger = new Mock<ILogger<GetSprintMetricsQueryHandler>>();
        _handler = new GetSprintMetricsQueryHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoWorkItems_ReturnsNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        var query = new GetSprintMetricsQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task Handle_WithMatchingWorkItems_CalculatesMetricsCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "Done", "Sprint 1", 8),
            CreateWorkItem(3, "Bug", "In Progress", "Sprint 1", 3),
            CreateWorkItem(4, "Task", "Done", "Sprint 1", 2),
            CreateWorkItem(5, "PBI", "New", "Sprint 2", 5) // Different sprint
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetSprintMetricsQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Sprint 1", result.IterationPath);
        Assert.AreEqual("Sprint 1", result.SprintName);
        Assert.AreEqual(15, result.CompletedStoryPoints); // 5 + 8 + 2 = 15
        Assert.AreEqual(18, result.PlannedStoryPoints); // 5 + 8 + 3 + 2 = 18
        Assert.AreEqual(3, result.CompletedWorkItemCount); // 3 items done
        Assert.AreEqual(4, result.TotalWorkItemCount); // 4 items in sprint
        Assert.AreEqual(2, result.CompletedPBIs); // 2 PBIs done
        Assert.AreEqual(0, result.CompletedBugs); // No bugs done
        Assert.AreEqual(1, result.CompletedTasks); // 1 task done
    }

    [TestMethod]
    public async Task Handle_WithCaseDifferentIterationPath_MatchesCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "SPRINT 1", 5),
            CreateWorkItem(2, "PBI", "Done", "Sprint 1", 8)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetSprintMetricsQuery("sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.CompletedWorkItemCount);
    }

    [TestMethod]
    public async Task Handle_WithVariousCompletedStates_RecognizesAll()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "Closed", "Sprint 1", 8),
            CreateWorkItem(3, "PBI", "Completed", "Sprint 1", 3),
            CreateWorkItem(4, "PBI", "Resolved", "Sprint 1", 2)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetSprintMetricsQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(4, result.CompletedWorkItemCount);
        Assert.AreEqual(18, result.CompletedStoryPoints);
    }

    [TestMethod]
    public async Task Handle_WithNullEffortValues_HandlesCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "Done", "Sprint 1", null),
            CreateWorkItem(3, "Task", "Done", "Sprint 1", 3)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetSprintMetricsQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(8, result.CompletedStoryPoints); // 5 + 3 = 8 (null ignored)
        Assert.AreEqual(8, result.PlannedStoryPoints);
        Assert.AreEqual(3, result.CompletedWorkItemCount);
    }

    [TestMethod]
    public async Task Handle_WithZeroEffortValues_IncludesInCount()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 0),
            CreateWorkItem(2, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(3, "Task", "In Progress", "Sprint 1", 0)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetSprintMetricsQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.CompletedStoryPoints); // Only item 2 has effort
        Assert.AreEqual(5, result.PlannedStoryPoints);
        Assert.AreEqual(2, result.CompletedWorkItemCount); // Items 1 and 2
        Assert.AreEqual(3, result.TotalWorkItemCount);
    }

    [TestMethod]
    public async Task Handle_WithAllNullEffort_ReturnsZeroStoryPoints()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", null),
            CreateWorkItem(2, "Bug", "Done", "Sprint 1", null),
            CreateWorkItem(3, "Task", "In Progress", "Sprint 1", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetSprintMetricsQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.CompletedStoryPoints);
        Assert.AreEqual(0, result.PlannedStoryPoints);
        Assert.AreEqual(2, result.CompletedWorkItemCount);
        Assert.AreEqual(3, result.TotalWorkItemCount);
    }

    [TestMethod]
    public async Task Handle_WithUserStoryType_CountsAsPBI()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "User Story", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "Product Backlog Item", "Done", "Sprint 1", 8)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetSprintMetricsQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.CompletedPBIs); // Both counted as PBIs
        Assert.AreEqual(0, result.CompletedBugs);
        Assert.AreEqual(0, result.CompletedTasks);
    }

    [TestMethod]
    public async Task Handle_WithComplexIterationPath_ExtractsCorrectSprintName()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Project\\Team A\\2024\\Sprint 5", 5)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetSprintMetricsQuery("Project\\Team A\\2024\\Sprint 5");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Sprint 5", result.SprintName);
        Assert.AreEqual("Project\\Team A\\2024\\Sprint 5", result.IterationPath);
    }

    [TestMethod]
    public async Task Handle_WithForwardSlashSeparator_ExtractsCorrectSprintName()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Project/Team A/2024/Sprint 5", 5)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetSprintMetricsQuery("Project/Team A/2024/Sprint 5");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Sprint 5", result.SprintName);
    }

    [TestMethod]
    public async Task Handle_WithMixedCompletionStates_CountsOnlyCompleted()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "New", "Sprint 1", 8),
            CreateWorkItem(3, "PBI", "Active", "Sprint 1", 3),
            CreateWorkItem(4, "Bug", "Closed", "Sprint 1", 2),
            CreateWorkItem(5, "Task", "Removed", "Sprint 1", 1)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetSprintMetricsQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(7, result.CompletedStoryPoints); // 5 + 2 = 7
        Assert.AreEqual(19, result.PlannedStoryPoints); // All items
        Assert.AreEqual(2, result.CompletedWorkItemCount); // Done and Closed only
        Assert.AreEqual(5, result.TotalWorkItemCount);
    }

    private static WorkItemDto CreateWorkItem(
        int id,
        string type,
        string state,
        string iterationPath,
        int? effort)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Work Item {id}",
            ParentTfsId: null,
            AreaPath: "TestArea",
            IterationPath: iterationPath,
            State: state,
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort
        );
    }
}
