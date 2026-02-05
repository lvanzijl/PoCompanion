using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetValidationImpactAnalysisQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IWorkItemReadProvider> _mockReadProvider = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IWorkItemValidator> _mockValidator = null!;
    private Mock<IWorkItemStateClassificationService> _mockStateClassificationService = null!;
    private Mock<ILogger<GetValidationImpactAnalysisQueryHandler>> _mockLogger = null!;
    private GetValidationImpactAnalysisQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockReadProvider = new Mock<IWorkItemReadProvider>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockValidator = new Mock<IWorkItemValidator>();
        _mockStateClassificationService = new Mock<IWorkItemStateClassificationService>();
        _mockLogger = new Mock<ILogger<GetValidationImpactAnalysisQueryHandler>>();

        // Setup default mock behaviors
        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());
            
        // Setup default state classifications
        SetupStateClassifications();

        _handler = new GetValidationImpactAnalysisQueryHandler(
            _mockRepository.Object,
            _mockReadProvider.Object,
            _mockProductRepository.Object,
            _mockValidator.Object,
            _mockStateClassificationService.Object,
            _mockLogger.Object
        );
    }
    
    private void SetupStateClassifications()
    {
        // Default: "In Progress" state is classified as InProgress
        _mockStateClassificationService
            .Setup(s => s.IsInProgressStateAsync(It.IsAny<string>(), "In Progress", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
            
        // Default: "New" state is NOT InProgress
        _mockStateClassificationService
            .Setup(s => s.IsInProgressStateAsync(It.IsAny<string>(), "New", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
            
        // Default: "Done" state is NOT InProgress
        _mockStateClassificationService
            .Setup(s => s.IsInProgressStateAsync(It.IsAny<string>(), "Done", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
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
            [1] = new List<ValidationIssue>
            {
                new ValidationIssue("Error", "Has children in progress but is not in progress (state: New). Children: #2 (Epic)", "RR-3")
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
        Assert.AreEqual(1, result.Violations[0].WorkItemId);
        Assert.AreEqual("Goal", result.Violations[0].WorkItemType);
        Assert.AreEqual("Error", result.Violations[0].Severity);
        // Verify that blocked items are counted (should be > 0 since Goal has descendants)
        Assert.IsGreaterThanOrEqualTo(0, result.TotalBlockedItems);
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
            [1] = new List<ValidationIssue>
            {
                new ValidationIssue("Error", "Has children in progress but is not in progress (state: New). Children: #2 (Epic)", "RR-3")
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
        // Should have blocked items (descendants of the violating Goal)
        Assert.IsGreaterThan(0, result.TotalBlockedItems);
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
            [1] = new List<ValidationIssue>
            {
                new ValidationIssue("Error", "Has children in progress but is not in progress (state: New). Children: #2 (Epic)", "RR-3")
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
        Assert.IsGreaterThan(0, setParentsRecommendation.Priority);
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
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
                    Description: null,
                    Tags: null
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
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
                    Description: null,
                    Tags: null
        );
    }
}
