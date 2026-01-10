using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetValidationImpactAnalysisQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IWorkItemValidator> _mockValidator = null!;
    private Mock<ILogger<GetValidationImpactAnalysisQueryHandler>> _mockLogger = null!;
    private GetValidationImpactAnalysisQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockValidator = new Mock<IWorkItemValidator>();
        _mockLogger = new Mock<ILogger<GetValidationImpactAnalysisQueryHandler>>();

        _handler = new GetValidationImpactAnalysisQueryHandler(
            _mockRepository.Object,
            _mockValidator.Object,
            _mockLogger.Object
        );
    }

    [TestMethod]
    public async Task Handle_WithNoViolations_ReturnsEmptyAnalysis()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "In Progress", null),
            CreateWorkItem(2, "Epic", "In Progress", 1)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());

        var query = new GetValidationImpactAnalysisQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsEmpty(result.Violations);
        Assert.AreEqual(0, result.TotalBlockedItems);
        Assert.AreEqual(0, result.TotalAffectedHierarchies);
    }

    [TestMethod]
    public async Task Handle_WithViolations_ReturnsImpactAnalysis()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null),
            CreateWorkItem(2, "Epic", "In Progress", 1),
            CreateWorkItem(3, "Feature", "In Progress", 2)
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            [2] = new List<ValidationIssue>
            {
                new ValidationIssue("Error", "Parent 'Goal' is not in progress")
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);

        var query = new GetValidationImpactAnalysisQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Violations);
        Assert.AreEqual(2, result.Violations[0].WorkItemId);
        Assert.AreEqual("Epic", result.Violations[0].WorkItemType);
        Assert.AreEqual("Error", result.Violations[0].Severity);
        Assert.IsGreaterThanOrEqualTo(result.TotalBlockedItems, 0);
    }

    [TestMethod]
    public async Task Handle_WithBlockedChildren_IdentifiesBlockedItems()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null),
            CreateWorkItem(2, "Epic", "In Progress", 1),
            CreateWorkItem(3, "Feature", "In Progress", 2),
            CreateWorkItem(4, "Task", "In Progress", 3)
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            [2] = new List<ValidationIssue>
            {
                new ValidationIssue("Error", "Parent 'Goal' is not in progress")
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);

        var query = new GetValidationImpactAnalysisQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsGreaterThan(result.TotalBlockedItems, 0);
        Assert.HasCount(1, result.Violations);

        // The violation should have descendants
        var violation = result.Violations[0];
        Assert.IsNotEmpty(violation.BlockedDescendantIds);
    }

    [TestMethod]
    public async Task Handle_WithErrorViolations_GeneratesRecommendations()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null),
            CreateWorkItem(2, "Epic", "In Progress", 1),
            CreateWorkItem(3, "Feature", "In Progress", 2)
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            [2] = new List<ValidationIssue>
            {
                new ValidationIssue("Error", "Parent 'Goal' is not in progress")
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);

        var query = new GetValidationImpactAnalysisQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Recommendations);
        Assert.IsNotEmpty(result.Recommendations);

        // Should have a recommendation to set parents to in progress
        var setParentsRecommendation = result.Recommendations
            .FirstOrDefault(r => r.RecommendationType == "SetParentsToInProgress");
        Assert.IsNotNull(setParentsRecommendation);
        Assert.IsGreaterThan(setParentsRecommendation.Priority, 0);
    }

    [TestMethod]
    public async Task Handle_WithAreaPathFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItemWithArea(1, "Goal", "New", null, "AreaA"),
            CreateWorkItemWithArea(2, "Epic", "In Progress", 1, "AreaA"),
            CreateWorkItemWithArea(3, "Goal", "New", null, "AreaB"),
            CreateWorkItemWithArea(4, "Epic", "In Progress", 3, "AreaB")
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            [2] = new List<ValidationIssue>
            {
                new ValidationIssue("Error", "Parent not in progress")
            },
            [4] = new List<ValidationIssue>
            {
                new ValidationIssue("Error", "Parent not in progress")
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);

        var query = new GetValidationImpactAnalysisQuery(AreaPathFilter: "AreaA");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Violations);
        Assert.AreEqual(2, result.Violations[0].WorkItemId);
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

    private static WorkItemDto CreateWorkItemWithArea(int id, string type, string state, int? parentId, string areaPath)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Test {type} {id}",
            ParentTfsId: parentId,
            AreaPath: areaPath,
            IterationPath: "TestIteration",
            State: state,
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null
        );
    }
}
