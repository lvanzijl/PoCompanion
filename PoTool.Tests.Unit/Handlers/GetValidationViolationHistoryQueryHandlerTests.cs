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
public class GetValidationViolationHistoryQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IWorkItemReadProvider> _mockReadProvider = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IWorkItemValidator> _mockValidator = null!;
    private Mock<ILogger<GetValidationViolationHistoryQueryHandler>> _mockLogger = null!;
    private GetValidationViolationHistoryQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockReadProvider = new Mock<IWorkItemReadProvider>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockValidator = new Mock<IWorkItemValidator>();
        _mockLogger = new Mock<ILogger<GetValidationViolationHistoryQueryHandler>>();

        // Setup default mock behaviors
        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());

        _handler = new GetValidationViolationHistoryQueryHandler(
            _mockRepository.Object,
            _mockReadProvider.Object,
            _mockProductRepository.Object,
            _mockValidator.Object,
            _mockLogger.Object
        );
    }

    [TestMethod]
    public async Task Handle_WithNoWorkItems_ReturnsEmptyHistory()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());

        var query = new GetValidationViolationHistoryQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_WithViolations_ReturnsHistoryRecords()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null, "TestArea", "Iteration1"),
            CreateWorkItem(2, "Epic", "In Progress", 1, "TestArea", "Iteration1")
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

        var query = new GetValidationViolationHistoryQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var historyList = result.ToList();
        Assert.HasCount(1, historyList);
        Assert.AreEqual(1, historyList[0].WorkItemId);
        Assert.AreEqual("Goal", historyList[0].WorkItemType);
        Assert.AreEqual("Error", historyList[0].Severity);
        Assert.AreEqual("ParentProgress", historyList[0].ValidationType);
    }

    [TestMethod]
    public async Task Handle_WithAreaPathFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "In Progress", null, "AreaA", "Iteration1"),
            CreateWorkItem(2, "Epic", "In Progress", 1, "AreaB", "Iteration1")
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            [1] = new List<ValidationIssue>
            {
                new ValidationIssue("Warning", "Some issue")
            },
            [2] = new List<ValidationIssue>
            {
                new ValidationIssue("Error", "Some issue")
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);

        var query = new GetValidationViolationHistoryQuery(AreaPathFilter: "AreaA");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var historyList = result.ToList();
        Assert.HasCount(1, historyList);
        Assert.AreEqual(1, historyList[0].WorkItemId);
        Assert.StartsWith(historyList[0].AreaPath, "AreaA");
    }

    [TestMethod]
    public async Task Handle_WithDateFilter_FiltersCorrectly()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var oldDate = now.AddDays(-10);
        var recentDate = now.AddDays(-2);

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItemWithDate(1, "Goal", "New", null, "TestArea", "Iteration1", oldDate),
            CreateWorkItemWithDate(2, "Epic", "In Progress", 1, "TestArea", "Iteration1", recentDate)
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            [1] = new List<ValidationIssue> { new ValidationIssue("Error", "Issue 1") },
            [2] = new List<ValidationIssue> { new ValidationIssue("Error", "Issue 2") }
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);

        var query = new GetValidationViolationHistoryQuery(StartDate: now.AddDays(-5));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var historyList = result.ToList();
        Assert.HasCount(1, historyList);
        Assert.AreEqual(2, historyList[0].WorkItemId);
    }

    private static WorkItemDto CreateWorkItem(int id, string type, string state, int? parentId, string areaPath, string iterationPath)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Test {type} {id}",
            ParentTfsId: parentId,
            AreaPath: areaPath,
            IterationPath: iterationPath,
            State: state,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
                    Description: null,
                    Tags: null
        );
    }

    private static WorkItemDto CreateWorkItemWithDate(int id, string type, string state, int? parentId, string areaPath, string iterationPath, DateTimeOffset retrievedAt)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Test {type} {id}",
            ParentTfsId: parentId,
            AreaPath: areaPath,
            IterationPath: iterationPath,
            State: state,
            RetrievedAt: retrievedAt,
            Effort: null,
                    Description: null,
                    Tags: null
        );
    }
}
