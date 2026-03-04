using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetEpicCompletionForecastQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<IWorkItemStateClassificationService> _mockStateService = null!;
    private Mock<ILogger<GetEpicCompletionForecastQueryHandler>> _mockLogger = null!;
    private GetEpicCompletionForecastQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockStateService = new Mock<IWorkItemStateClassificationService>();
        _mockLogger = new Mock<ILogger<GetEpicCompletionForecastQueryHandler>>();
        
        // Setup default state classification
        _mockStateService.Setup(s => s.IsDoneStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string type, string state, CancellationToken ct) => 
                state.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Removed", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Completed", StringComparison.OrdinalIgnoreCase));

        // Setup default mock behaviors
        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());

        // Default: return sprint metrics with 10 completed story points for any iteration path
        _mockMediator.Setup(m => m.Send(It.IsAny<GetSprintMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetSprintMetricsQuery q, CancellationToken ct) =>
                new SprintMetricsDto(
                    IterationPath: q.IterationPath,
                    SprintName: q.IterationPath,
                    StartDate: DateTimeOffset.UtcNow.AddDays(-28),
                    EndDate: DateTimeOffset.UtcNow.AddDays(-14),
                    CompletedStoryPoints: 10,
                    PlannedStoryPoints: 12,
                    CompletedWorkItemCount: 5,
                    TotalWorkItemCount: 8,
                    CompletedPBIs: 3,
                    CompletedBugs: 1,
                    CompletedTasks: 1
                ));
        
        _handler = new GetEpicCompletionForecastQueryHandler(
            _mockRepository.Object,
            _mockProductRepository.Object,
            _mockMediator.Object,
            _mockStateService.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNonExistentEpic_ReturnsNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByTfsIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);
        var query = new GetEpicCompletionForecastQuery(999);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task Handle_WithEpicWithNoChildren_CalculatesCorrectly()
    {
        // Arrange
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic });

        var query = new GetEpicCompletionForecastQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.EpicId);
        Assert.AreEqual(0, result.TotalEffort);
        Assert.AreEqual(0, result.CompletedEffort);
        Assert.AreEqual(0, result.RemainingEffort);
        Assert.AreEqual(0, result.SprintsRemaining);
    }

    [TestMethod]
    public async Task Handle_WithEpicAndChildren_CalculatesCorrectly()
    {
        // Arrange
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var child1 = CreateWorkItem(2, "PBI", "Done", "TestArea", "Sprint 1", 1, 5);
        var child2 = CreateWorkItem(3, "PBI", "In Progress", "TestArea", "Sprint 2", 1, 8);
        var child3 = CreateWorkItem(4, "PBI", "New", "TestArea", "Sprint 3", 1, 13);

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, child1, child2, child3 });

        var query = new GetEpicCompletionForecastQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(26, result.TotalEffort); // 5 + 8 + 13
        Assert.AreEqual(5, result.CompletedEffort); // Only child1 is done
        Assert.AreEqual(21, result.RemainingEffort); // 26 - 5
        Assert.AreEqual(10.0, result.EstimatedVelocity);
        Assert.AreEqual(3, result.SprintsRemaining); // Ceiling(21 / 10)
    }

    [TestMethod]
    public async Task Handle_WithNestedChildren_IncludesAllDescendants()
    {
        // Arrange
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var feature = CreateWorkItem(2, "Feature", "In Progress", "TestArea", "Sprint 1", 1, null);
        var pbi1 = CreateWorkItem(3, "PBI", "Done", "TestArea", "Sprint 1", 2, 5);
        var pbi2 = CreateWorkItem(4, "PBI", "In Progress", "TestArea", "Sprint 2", 2, 8);
        var task = CreateWorkItem(5, "Task", "Done", "TestArea", "Sprint 1", 3, 2);

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, feature, pbi1, pbi2, task });

        var query = new GetEpicCompletionForecastQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(15, result.TotalEffort); // 5 + 8 + 2 (all descendants)
        Assert.AreEqual(7, result.CompletedEffort); // 5 + 2 (pbi1 and task)
        Assert.AreEqual(8, result.RemainingEffort);
    }

    [TestMethod]
    public async Task Handle_WithZeroVelocity_ReturnsZeroSprintsRemaining()
    {
        // Arrange
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var child = CreateWorkItem(2, "PBI", "New", "TestArea", "Sprint 1", 1, 20);

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, child });

        _mockMediator.Setup(m => m.Send(It.IsAny<GetSprintMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetSprintMetricsQuery q, CancellationToken ct) =>
                new SprintMetricsDto(
                    IterationPath: q.IterationPath,
                    SprintName: q.IterationPath,
                    StartDate: DateTimeOffset.UtcNow.AddDays(-28),
                    EndDate: DateTimeOffset.UtcNow.AddDays(-14),
                    CompletedStoryPoints: 0,
                    PlannedStoryPoints: 12,
                    CompletedWorkItemCount: 0,
                    TotalWorkItemCount: 5,
                    CompletedPBIs: 0,
                    CompletedBugs: 0,
                    CompletedTasks: 0
                ));

        var query = new GetEpicCompletionForecastQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(20, result.RemainingEffort);
        Assert.AreEqual(0, result.SprintsRemaining); // Can't forecast with zero velocity
    }

    [TestMethod]
    public async Task Handle_WithChildrenWithoutEffort_IgnoresThemInCalculation()
    {
        // Arrange
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var child1 = CreateWorkItem(2, "PBI", "Done", "TestArea", "Sprint 1", 1, 5);
        var child2 = CreateWorkItem(3, "PBI", "New", "TestArea", "Sprint 2", 1, null);
        var child3 = CreateWorkItem(4, "PBI", "New", "TestArea", "Sprint 3", 1, 10);

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, child1, child2, child3 });

        var query = new GetEpicCompletionForecastQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(15, result.TotalEffort); // 5 + 10 (null ignored)
        Assert.AreEqual(5, result.CompletedEffort);
        Assert.AreEqual(10, result.RemainingEffort);
    }

    [TestMethod]
    public async Task Handle_WithAllChildrenCompleted_ReturnsZeroRemaining()
    {
        // Arrange
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var child1 = CreateWorkItem(2, "PBI", "Done", "TestArea", "Sprint 1", 1, 5);
        var child2 = CreateWorkItem(3, "PBI", "Closed", "TestArea", "Sprint 2", 1, 8);

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, child1, child2 });

        var query = new GetEpicCompletionForecastQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(13, result.TotalEffort);
        Assert.AreEqual(13, result.CompletedEffort);
        Assert.AreEqual(0, result.RemainingEffort);
        Assert.AreEqual(0, result.SprintsRemaining);
    }

    [TestMethod]
    public async Task Handle_WithLowHistoricalData_ReturnsLowConfidence()
    {
        // Arrange
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var child = CreateWorkItem(2, "PBI", "New", "TestArea", "Sprint 1", 1, 10);

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, child });

        var query = new GetEpicCompletionForecastQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ForecastConfidence.Low, result.Confidence);
    }

    [TestMethod]
    public async Task Handle_WithMediumHistoricalData_ReturnsMediumConfidence()
    {
        // Arrange: 3 distinct iteration paths → count = 3 → Medium confidence
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var child1 = CreateWorkItem(2, "PBI", "New", "TestArea", "Sprint 1", 1, 10);
        var child2 = CreateWorkItem(3, "PBI", "New", "TestArea", "Sprint 2", 1, 10);
        var child3 = CreateWorkItem(4, "PBI", "New", "TestArea", "Sprint 3", 1, 10);

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, child1, child2, child3 });

        var query = new GetEpicCompletionForecastQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ForecastConfidence.Medium, result.Confidence);
    }

    [TestMethod]
    public async Task Handle_WithHighHistoricalData_ReturnsHighConfidence()
    {
        // Arrange: 5 distinct iteration paths → count = 5 → High confidence
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var child1 = CreateWorkItem(2, "PBI", "New", "TestArea", "Sprint 1", 1, 10);
        var child2 = CreateWorkItem(3, "PBI", "New", "TestArea", "Sprint 2", 1, 10);
        var child3 = CreateWorkItem(4, "PBI", "New", "TestArea", "Sprint 3", 1, 10);
        var child4 = CreateWorkItem(5, "PBI", "New", "TestArea", "Sprint 4", 1, 10);
        var child5 = CreateWorkItem(6, "PBI", "New", "TestArea", "Sprint 5", 1, 10);

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, child1, child2, child3, child4, child5 });

        var query = new GetEpicCompletionForecastQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ForecastConfidence.High, result.Confidence);
    }

    [TestMethod]
    public async Task Handle_WithVariousCompletedStates_RecognizesAll()
    {
        // Arrange
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var child1 = CreateWorkItem(2, "PBI", "Done", "TestArea", "Sprint 1", 1, 5);
        var child2 = CreateWorkItem(3, "PBI", "Closed", "TestArea", "Sprint 1", 1, 8);
        var child3 = CreateWorkItem(4, "PBI", "Completed", "TestArea", "Sprint 1", 1, 3);
        var child4 = CreateWorkItem(5, "PBI", "Removed", "TestArea", "Sprint 1", 1, 2);
        var child5 = CreateWorkItem(6, "PBI", "In Progress", "TestArea", "Sprint 1", 1, 10);

        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, child1, child2, child3, child4, child5 });

        var query = new GetEpicCompletionForecastQuery(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(28, result.TotalEffort);
        Assert.AreEqual(18, result.CompletedEffort); // Done, Closed, Completed, Removed
        Assert.AreEqual(10, result.RemainingEffort);
    }

    private static WorkItemDto CreateWorkItem(
        int id,
        string type,
        string state,
        string areaPath,
        string iterationPath,
        int? parentTfsId,
        int? effort)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Work Item {id}",
            ParentTfsId: parentTfsId,
            AreaPath: areaPath,
            IterationPath: iterationPath,
            State: state,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
                    Description: null,
                    Tags: null
        );
    }
}
