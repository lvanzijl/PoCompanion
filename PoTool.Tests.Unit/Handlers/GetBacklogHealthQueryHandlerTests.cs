using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Validators;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetBacklogHealthQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IWorkItemValidator> _mockValidator = null!;
    private Mock<ILogger<GetBacklogHealthQueryHandler>> _mockLogger = null!;
    private GetBacklogHealthQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockValidator = new Mock<IWorkItemValidator>();
        _mockLogger = new Mock<ILogger<GetBacklogHealthQueryHandler>>();
        _handler = new GetBacklogHealthQueryHandler(
            _mockRepository.Object,
            _mockValidator.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoWorkItems_ReturnsNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task Handle_WithValidWorkItems_CalculatesCorrectMetrics()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "In Progress", "Sprint 1", null),
            CreateWorkItem(3, "Bug", "New", "Sprint 1", null),
            CreateWorkItem(4, "Task", "Done", "Sprint 1", 3)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());
        
        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Sprint 1", result.IterationPath);
        Assert.AreEqual("Sprint 1", result.SprintName);
        Assert.AreEqual(4, result.TotalWorkItems);
        Assert.AreEqual(2, result.WorkItemsWithoutEffort); // Items 2 and 3
        Assert.AreEqual(1, result.WorkItemsInProgressWithoutEffort); // Item 2
    }

    [TestMethod]
    public async Task Handle_WithBlockedItems_CountsCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Blocked", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "On Hold", "Sprint 1", 8),
            CreateWorkItem(3, "Bug", "In Progress", "Sprint 1", 3)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());
        
        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.BlockedItems); // Items with "Blocked" or "On Hold"
    }

    [TestMethod]
    public async Task Handle_WithValidationIssues_GroupsByType()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "In Progress", "Sprint 1", null),
            CreateWorkItem(2, "PBI", "Done", "Sprint 1", 5)
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            { 1, new List<ValidationIssue> 
                { 
                    new ValidationIssue("Error", "Work item in progress without effort")
                } 
            },
            { 2, new List<ValidationIssue> 
                { 
                    new ValidationIssue("Warning", "Parent progress issue"),
                    new ValidationIssue("Warning", "Another parent issue")
                } 
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);
        
        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ValidationIssues.Count > 0);
    }

    [TestMethod]
    public async Task Handle_WithParentProgressIssues_CountsCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", 3)
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            { 1, new List<ValidationIssue> 
                { 
                    new ValidationIssue("Warning", "Parent work item progress mismatch")
                } 
            },
            { 2, new List<ValidationIssue> 
                { 
                    new ValidationIssue("Warning", "Ancestor progress inconsistency")
                } 
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);
        
        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.ParentProgressIssues); // Both issues contain "Parent" or "Ancestor"
    }

    [TestMethod]
    public async Task Handle_WithAllItemsHavingEffort_ReturnsZeroWithoutEffort()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "In Progress", "Sprint 1", 8),
            CreateWorkItem(3, "Bug", "New", "Sprint 1", 3)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());
        
        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.WorkItemsWithoutEffort);
        Assert.AreEqual(0, result.WorkItemsInProgressWithoutEffort);
    }

    [TestMethod]
    public async Task Handle_WithCaseInsensitiveIterationPath_MatchesCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "SPRINT 1", 5),
            CreateWorkItem(2, "PBI", "Done", "Sprint 1", 8)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());
        
        var query = new GetBacklogHealthQuery("sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalWorkItems);
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
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());
        
        var query = new GetBacklogHealthQuery("Project\\Team A\\2024\\Sprint 5");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Sprint 5", result.SprintName);
        Assert.AreEqual("Project\\Team A\\2024\\Sprint 5", result.IterationPath);
    }

    [TestMethod]
    public async Task Handle_WithZeroEffortItems_CountsAsHavingEffort()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 0),
            CreateWorkItem(2, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(3, "Bug", "New", "Sprint 1", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());
        
        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.WorkItemsWithoutEffort); // Only item 3
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
