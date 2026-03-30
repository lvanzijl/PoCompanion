using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetValidationImpactAnalysisQueryHandlerTests
{
    private Mock<IWorkItemQuery> _mockWorkItemQuery = null!;
    private Mock<IWorkItemValidator> _mockValidator = null!;
    private Mock<IWorkItemStateClassificationService> _mockStateClassificationService = null!;
    private Mock<ILogger<GetValidationImpactAnalysisQueryHandler>> _mockLogger = null!;
    private GetValidationImpactAnalysisQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkItemQuery = new Mock<IWorkItemQuery>();
        _mockValidator = new Mock<IWorkItemValidator>();
        _mockStateClassificationService = new Mock<IWorkItemStateClassificationService>();
        _mockLogger = new Mock<ILogger<GetValidationImpactAnalysisQueryHandler>>();

        SetupStateClassifications();

        _handler = new GetValidationImpactAnalysisQueryHandler(
            _mockWorkItemQuery.Object,
            _mockValidator.Object,
            _mockStateClassificationService.Object,
            _mockLogger.Object
        );
    }

    private void SetupStateClassifications()
    {
        _mockStateClassificationService
            .Setup(s => s.IsInProgressStateAsync(It.IsAny<string>(), "In Progress", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockStateClassificationService
            .Setup(s => s.IsInProgressStateAsync(It.IsAny<string>(), "New", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockStateClassificationService
            .Setup(s => s.IsInProgressStateAsync(It.IsAny<string>(), "Done", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    [TestMethod]
    public async Task Handle_WithNoViolations_ReturnsEmptyAnalysis()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "In Progress", null),
            CreateWorkItem(2, "Epic", "In Progress", 1)
        };

        _mockWorkItemQuery
            .Setup(query => query.GetValidationImpactSourceAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSource(workItems));
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());

        var result = await _handler.Handle(new GetValidationImpactAnalysisQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsEmpty(result.Violations);
        Assert.AreEqual(0, result.TotalBlockedItems);
        Assert.AreEqual(0, result.TotalAffectedHierarchies);
    }

    [TestMethod]
    public async Task Handle_WithViolations_ReturnsImpactAnalysis()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null),
            CreateWorkItem(2, "Epic", "In Progress", 1),
            CreateWorkItem(3, "Feature", "In Progress", 2)
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            [1] =
            [
                new ValidationIssue("Error", "Has children in progress but is not in progress (state: New). Children: #2 (Epic)", "SI-1")
            ]
        };

        _mockWorkItemQuery
            .Setup(query => query.GetValidationImpactSourceAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSource(workItems));
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);

        var result = await _handler.Handle(new GetValidationImpactAnalysisQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Violations);
        Assert.AreEqual(1, result.Violations[0].WorkItemId);
        Assert.AreEqual("Goal", result.Violations[0].WorkItemType);
        Assert.AreEqual("SI-1", result.Violations[0].ViolationType);
        Assert.AreEqual("Error", result.Violations[0].Severity);
        Assert.IsGreaterThanOrEqualTo(0, result.TotalBlockedItems);
    }

    [TestMethod]
    public async Task Handle_WithBlockedChildren_IdentifiesBlockedItems()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null),
            CreateWorkItem(2, "Epic", "In Progress", 1),
            CreateWorkItem(3, "Feature", "In Progress", 2),
            CreateWorkItem(4, "Task", "In Progress", 3)
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            [1] =
            [
                new ValidationIssue("Error", "Has children in progress but is not in progress (state: New). Children: #2 (Epic)", "SI-1")
            ]
        };

        _mockWorkItemQuery
            .Setup(query => query.GetValidationImpactSourceAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSource(workItems));
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);

        var result = await _handler.Handle(new GetValidationImpactAnalysisQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsGreaterThan(0, result.TotalBlockedItems);
        Assert.HasCount(1, result.Violations);
        Assert.IsNotEmpty(result.Violations[0].BlockedDescendantIds);
    }

    [TestMethod]
    public async Task Handle_WithErrorViolations_GeneratesRecommendations()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null),
            CreateWorkItem(2, "Epic", "In Progress", 1),
            CreateWorkItem(3, "Feature", "In Progress", 2)
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            [1] =
            [
                new ValidationIssue("Error", "Has children in progress but is not in progress (state: New). Children: #2 (Epic)", "SI-1")
            ]
        };

        _mockWorkItemQuery
            .Setup(query => query.GetValidationImpactSourceAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSource(workItems));
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);

        var result = await _handler.Handle(new GetValidationImpactAnalysisQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Recommendations);
        Assert.IsNotNull(result.Recommendations.FirstOrDefault(r => r.RecommendationType == "SetParentsToInProgress"));
    }

    [TestMethod]
    public async Task Handle_UsesFilteredValidationImpactSource()
    {
        var filteredWorkItems = new List<WorkItemDto>
        {
            CreateWorkItemWithArea(1, "Goal", "New", null, "AreaA"),
            CreateWorkItemWithArea(2, "Epic", "In Progress", 1, "AreaA")
        };

        var validationResults = new Dictionary<int, List<ValidationIssue>>
        {
            [2] = [new ValidationIssue("Error", "Parent not in progress")]
        };

        _mockWorkItemQuery
            .Setup(query => query.GetValidationImpactSourceAsync("AreaA", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSource(filteredWorkItems));
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationResults);

        var result = await _handler.Handle(new GetValidationImpactAnalysisQuery(AreaPathFilter: "AreaA"), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Violations);
        Assert.AreEqual(2, result.Violations[0].WorkItemId);
    }

    private static ValidationImpactQuerySource CreateSource(IReadOnlyList<WorkItemDto> workItems)
    {
        var childrenLookup = workItems
            .Where(workItem => workItem.ParentTfsId.HasValue)
            .GroupBy(workItem => workItem.ParentTfsId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(workItem => workItem.TfsId).ToList());

        return new ValidationImpactQuerySource(workItems, childrenLookup);
    }

    private static WorkItemDto CreateWorkItem(int id, string type, string state, int? parentId)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"{type} {id}",
            ParentTfsId: parentId,
            AreaPath: "DefaultArea",
            IterationPath: "Sprint 1",
            State: state,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null);
    }

    private static WorkItemDto CreateWorkItemWithArea(int id, string type, string state, int? parentId, string areaPath)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"{type} {id}",
            ParentTfsId: parentId,
            AreaPath: areaPath,
            IterationPath: "Sprint 1",
            State: state,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null);
    }
}
