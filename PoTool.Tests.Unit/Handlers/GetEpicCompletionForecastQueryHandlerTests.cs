using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetEpicCompletionForecastQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<ILogger<GetEpicCompletionForecastQueryHandler>> _mockLogger = null!;
    private GetEpicCompletionForecastQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<GetEpicCompletionForecastQueryHandler>>();
        _handler = new GetEpicCompletionForecastQueryHandler(
            _mockRepository.Object,
            _mockMediator.Object,
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

        var velocityTrend = CreateVelocityTrend(20.0);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetVelocityTrendQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(velocityTrend);

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

        var velocityTrend = CreateVelocityTrend(10.0);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetVelocityTrendQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(velocityTrend);

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

        var velocityTrend = CreateVelocityTrend(10.0);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetVelocityTrendQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(velocityTrend);

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

        var velocityTrend = CreateVelocityTrend(0.0);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetVelocityTrendQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(velocityTrend);

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

        var velocityTrend = CreateVelocityTrend(10.0);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetVelocityTrendQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(velocityTrend);

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

        var velocityTrend = CreateVelocityTrend(10.0);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetVelocityTrendQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(velocityTrend);

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

        var velocityTrend = CreateVelocityTrend(10.0, sprintCount: 2);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetVelocityTrendQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(velocityTrend);

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
        // Arrange
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var child = CreateWorkItem(2, "PBI", "New", "TestArea", "Sprint 1", 1, 10);
        
        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, child });

        var velocityTrend = CreateVelocityTrend(10.0, sprintCount: 4);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetVelocityTrendQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(velocityTrend);

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
        // Arrange
        var epic = CreateWorkItem(1, "Epic", "In Progress", "TestArea", "Sprint 1", null, null);
        var child = CreateWorkItem(2, "PBI", "New", "TestArea", "Sprint 1", 1, 10);
        
        _mockRepository.Setup(r => r.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { epic, child });

        var velocityTrend = CreateVelocityTrend(10.0, sprintCount: 6);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetVelocityTrendQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(velocityTrend);

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

        var velocityTrend = CreateVelocityTrend(10.0);
        _mockMediator.Setup(m => m.Send(It.IsAny<GetVelocityTrendQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(velocityTrend);

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
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort
        );
    }

    private static VelocityTrendDto CreateVelocityTrend(double averageVelocity, int sprintCount = 5)
    {
        var sprints = new List<SprintMetricsDto>();
        for (int i = 1; i <= sprintCount; i++)
        {
            sprints.Add(new SprintMetricsDto(
                IterationPath: $"Sprint {i}",
                SprintName: $"Sprint {i}",
                StartDate: DateTimeOffset.UtcNow.AddDays(-14 * (sprintCount - i + 1)),
                EndDate: DateTimeOffset.UtcNow.AddDays(-14 * (sprintCount - i)),
                CompletedStoryPoints: (int)averageVelocity,
                PlannedStoryPoints: (int)averageVelocity + 5,
                CompletedWorkItemCount: 5,
                TotalWorkItemCount: 8,
                CompletedPBIs: 3,
                CompletedBugs: 1,
                CompletedTasks: 1
            ));
        }

        return new VelocityTrendDto(
            Sprints: sprints,
            AverageVelocity: averageVelocity,
            ThreeSprintAverage: averageVelocity,
            SixSprintAverage: averageVelocity,
            TotalCompletedStoryPoints: (int)(averageVelocity * sprintCount),
            TotalSprints: sprintCount
        );
    }
}
