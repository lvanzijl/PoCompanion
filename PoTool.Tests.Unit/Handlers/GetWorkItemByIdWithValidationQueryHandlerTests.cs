using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetWorkItemByIdWithValidationQueryHandlerTests
{
    private Mock<IWorkItemReadProvider> _mockReadProvider = null!;
    private Mock<IWorkItemValidator> _mockValidator = null!;
    private Mock<ILogger<GetWorkItemByIdWithValidationQueryHandler>> _mockLogger = null!;
    private GetWorkItemByIdWithValidationQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockReadProvider = new Mock<IWorkItemReadProvider>();
        _mockValidator = new Mock<IWorkItemValidator>();
        _mockLogger = new Mock<ILogger<GetWorkItemByIdWithValidationQueryHandler>>();
        
        _handler = new GetWorkItemByIdWithValidationQueryHandler(
            _mockReadProvider.Object,
            _mockValidator.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WorkItemExists_ReturnsWorkItemWithValidation()
    {
        // Arrange
        var workItem = new WorkItemDto(
            TfsId: 123,
            Type: "Task",
            Title: "Test Work Item",
            ParentTfsId: null,
            AreaPath: "Area1",
            IterationPath: "Iteration1",
            State: "Active",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null
        );

        var validationIssues = new Dictionary<int, List<ValidationIssue>>
        {
            { 123, new List<ValidationIssue> { new ValidationIssue("Warning", "Effort is required", "EFFORT_MISSING") } }
        };

        _mockReadProvider
            .Setup(x => x.GetByTfsIdAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        _mockValidator
            .Setup(x => x.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationIssues);

        var query = new GetWorkItemByIdWithValidationQuery(123);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(123, result.TfsId);
        Assert.AreEqual("Test Work Item", result.Title);
        Assert.HasCount(1, result.ValidationIssues);
        Assert.AreEqual("Effort is required", result.ValidationIssues[0].Message);
    }

    [TestMethod]
    public async Task Handle_WorkItemDoesNotExist_ReturnsNull()
    {
        // Arrange
        _mockReadProvider
            .Setup(x => x.GetByTfsIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);

        var query = new GetWorkItemByIdWithValidationQuery(999);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task Handle_WorkItemExistsWithNoValidationIssues_ReturnsWorkItemWithEmptyIssues()
    {
        // Arrange
        var workItem = new WorkItemDto(
            TfsId: 456,
            Type: "Bug",
            Title: "Valid Work Item",
            ParentTfsId: null,
            AreaPath: "Area2",
            IterationPath: "Iteration2",
            State: "Done",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: 5,
            Description: null
        );

        var validationIssues = new Dictionary<int, List<ValidationIssue>>();

        _mockReadProvider
            .Setup(x => x.GetByTfsIdAsync(456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        _mockValidator
            .Setup(x => x.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationIssues);

        var query = new GetWorkItemByIdWithValidationQuery(456);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(456, result.TfsId);
        Assert.AreEqual("Valid Work Item", result.Title);
        Assert.IsEmpty(result.ValidationIssues);
    }

    [TestMethod]
    public async Task Handle_WithProductIds_StillReturnsWorkItem()
    {
        // Arrange
        var workItem = new WorkItemDto(
            TfsId: 789,
            Type: "Feature",
            Title: "Feature Work Item",
            ParentTfsId: null,
            AreaPath: "Area3",
            IterationPath: "Iteration3",
            State: "New",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null
        );

        _mockReadProvider
            .Setup(x => x.GetByTfsIdAsync(789, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        _mockValidator
            .Setup(x => x.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());

        // Note: productIds parameter is currently not enforced, just accepted for future use
        var query = new GetWorkItemByIdWithValidationQuery(789, new[] { 1, 2 });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(789, result.TfsId);
    }
}
