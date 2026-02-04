using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetEffortEstimationQualityQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<IWorkItemStateClassificationService> _mockStateService = null!;
    private Mock<ILogger<GetEffortEstimationQualityQueryHandler>> _mockLogger = null!;
    private GetEffortEstimationQualityQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockStateService = new Mock<IWorkItemStateClassificationService>();
        _mockLogger = new Mock<ILogger<GetEffortEstimationQualityQueryHandler>>();
        
        // Setup default state classification
        _mockStateService.Setup(s => s.IsDoneStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string type, string state, CancellationToken ct) => 
                state.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Resolved", StringComparison.OrdinalIgnoreCase));

        // Setup default mock behaviors
        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        
        _handler = new GetEffortEstimationQualityQueryHandler(
            _mockRepository.Object,
            _mockProductRepository.Object,
            _mockMediator.Object,
            _mockStateService.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoCompletedWorkItems_ReturnsEmptyQuality()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "In Progress", "Sprint 1", 5),
            CreateWorkItem(2, "Task", "New", "Sprint 1", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationQualityQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.TotalCompletedWorkItems);
        Assert.AreEqual(0, result.WorkItemsWithEstimates);
        Assert.IsEmpty(result.QualityByType);
    }

    [TestMethod]
    public async Task Handle_WithCompletedWorkItems_CalculatesQuality()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(3, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(4, "Bug", "Closed", "Sprint 1", 3),
            CreateWorkItem(5, "Bug", "Closed", "Sprint 1", 3)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationQualityQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.TotalCompletedWorkItems);
        Assert.AreEqual(5, result.WorkItemsWithEstimates);
        Assert.HasCount(2, result.QualityByType); // Task and Bug
        Assert.IsTrue(result.AverageEstimationAccuracy >= 0 && result.AverageEstimationAccuracy <= 1);
    }

    [TestMethod]
    public async Task Handle_WithConsistentEffortsByType_HighAccuracy()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(3, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(4, "Task", "Done", "Sprint 1", 5)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationQualityQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result.QualityByType);
        var taskQuality = result.QualityByType[0];
        Assert.AreEqual("Task", taskQuality.WorkItemType);
        Assert.AreEqual(4, taskQuality.Count);
        Assert.AreEqual(5, taskQuality.AverageEffort);
        Assert.AreEqual(5, taskQuality.TypicalEffortMin);
        Assert.AreEqual(5, taskQuality.TypicalEffortMax);
        Assert.IsGreaterThan(taskQuality.AverageAccuracy, 0.9); // Very consistent
    }

    [TestMethod]
    public async Task Handle_WithVariableEffortsByType_LowerAccuracy()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 1),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(3, "Task", "Done", "Sprint 1", 10),
            CreateWorkItem(4, "Task", "Done", "Sprint 1", 20)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationQualityQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var taskQuality = result.QualityByType[0];
        Assert.AreEqual("Task", taskQuality.WorkItemType);
        Assert.AreEqual(1, taskQuality.TypicalEffortMin);
        Assert.AreEqual(20, taskQuality.TypicalEffortMax);
        Assert.IsLessThan(taskQuality.AverageAccuracy, 0.9); // Less consistent
    }

    [TestMethod]
    public async Task Handle_WithAreaPathFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItemWithArea(1, "Task", "Done", "Sprint 1", "Team\\A", 5),
            CreateWorkItemWithArea(2, "Task", "Done", "Sprint 1", "Team\\A", 5),
            CreateWorkItemWithArea(3, "Task", "Done", "Sprint 1", "Team\\B", 3)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationQualityQuery("Team\\A");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalCompletedWorkItems);
        Assert.AreEqual(2, result.WorkItemsWithEstimates);
    }

    [TestMethod]
    public async Task Handle_WithMultipleIterations_CalculatesTrend()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(3, "Task", "Done", "Sprint 2", 3),
            CreateWorkItem(4, "Task", "Done", "Sprint 2", 3)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationQualityQuery(null, 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsGreaterThanOrEqualTo(result.TrendOverTime.Count, 2); // At least 2 iterations
    }

    [TestMethod]
    public async Task Handle_WithMaxIterationsLimit_RespectsLimit()
    {
        // Arrange
        var workItems = new List<WorkItemDto>();
        for (int i = 1; i <= 15; i++)
        {
            workItems.Add(CreateWorkItem(i, "Task", "Done", $"Sprint {i}", 5));
        }

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationQualityQuery(null, 5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsLessThanOrEqualTo(result.TrendOverTime.Count, 5);
    }

    [TestMethod]
    public async Task Handle_WithDifferentStates_RecognizesCompletedStates()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "Task", "Closed", "Sprint 1", 5),
            CreateWorkItem(3, "Task", "Resolved", "Sprint 1", 5),
            CreateWorkItem(4, "Task", "In Progress", "Sprint 1", 5) // Not completed
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationQualityQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.TotalCompletedWorkItems); // Only Done, Closed, Resolved
    }

    [TestMethod]
    public async Task Handle_WithWorkItemsWithoutEffort_ExcludesFromAnalysis()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", null), // No effort
            CreateWorkItem(3, "Task", "Done", "Sprint 1", 0) // Zero effort
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationQualityQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalCompletedWorkItems); // Only item with effort > 0
        Assert.AreEqual(1, result.WorkItemsWithEstimates);
    }

    private static WorkItemDto CreateWorkItem(int id, string type, string state, string iterationPath, int? effort)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Test {type} {id}",
            ParentTfsId: null,
            AreaPath: "TestArea",
            IterationPath: iterationPath,
            State: state,
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
                    Description: null,
                    Tags: null
        );
    }

    private static WorkItemDto CreateWorkItemWithArea(int id, string type, string state, string iterationPath, string areaPath, int? effort)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Test {type} {id}",
            ParentTfsId: null,
            AreaPath: areaPath,
            IterationPath: iterationPath,
            State: state,
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
                    Description: null,
                    Tags: null
        );
    }
}
