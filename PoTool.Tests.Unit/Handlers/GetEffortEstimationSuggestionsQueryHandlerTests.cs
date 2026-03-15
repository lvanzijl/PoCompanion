using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetEffortEstimationSuggestionsQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<IWorkItemStateClassificationService> _mockStateService = null!;
    private Mock<ILogger<GetEffortEstimationSuggestionsQueryHandler>> _mockLogger = null!;
    private GetEffortEstimationSuggestionsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockStateService = new Mock<IWorkItemStateClassificationService>();
        _mockLogger = new Mock<ILogger<GetEffortEstimationSuggestionsQueryHandler>>();

        // Setup default effort estimation settings
        _mockMediator.Setup(m => m.Send(It.IsAny<GetEffortEstimationSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EffortEstimationSettingsDto.Default);

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

        _handler = new GetEffortEstimationSuggestionsQueryHandler(
            _mockRepository.Object,
            _mockProductRepository.Object,
            _mockMediator.Object,
            _mockStateService.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoWorkItemsWithoutEffort_ReturnsEmptyList()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", 3)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationSuggestionsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task Handle_WithWorkItemsWithoutEffort_GeneratesSuggestions()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            // Historical completed items with effort
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", 3),
            CreateWorkItem(3, "Task", "Done", "Sprint 2", 4),
            // Items without effort
            CreateWorkItem(4, "Task", "In Progress", "Sprint 3", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationSuggestionsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result);
        Assert.AreEqual(4, result[0].WorkItemId);
        Assert.IsGreaterThan(result[0].SuggestedEffort, 0);
        Assert.IsTrue(result[0].Confidence >= 0 && result[0].Confidence <= 1);
        Assert.IsFalse(string.IsNullOrEmpty(result[0].Rationale));
    }

    [TestMethod]
    public async Task Handle_WithIterationPathFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "In Progress", "Sprint 1", null),
            CreateWorkItem(2, "Task", "In Progress", "Sprint 2", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationSuggestionsQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result);
        Assert.AreEqual(1, result[0].WorkItemId);
    }

    [TestMethod]
    public async Task Handle_WithOnlyInProgressFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "In Progress", "Sprint 1", null),
            CreateWorkItem(2, "Task", "New", "Sprint 1", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationSuggestionsQuery(null, null, true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result);
        Assert.AreEqual(1, result[0].WorkItemId);
    }

    [TestMethod]
    public async Task Handle_WithNoHistoricalData_UsesDefaultEstimates()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "In Progress", "Sprint 1", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationSuggestionsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result);
        Assert.AreEqual(3, result[0].SuggestedEffort); // Default for Task
        Assert.IsLessThan(result[0].Confidence, 0.5); // Low confidence without history
        Assert.Contains(result[0].Rationale, "No historical data");
    }

    [TestMethod]
    public async Task Handle_WithHistoricalData_CalculatesMedian()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            // Historical data
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 3),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(3, "Task", "Done", "Sprint 1", 5),
            CreateWorkItem(4, "Task", "Done", "Sprint 1", 8),
            // Item without effort
            CreateWorkItem(5, "Task", "In Progress", "Sprint 2", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationSuggestionsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result);
        Assert.AreEqual(5, result[0].SuggestedEffort); // Median of 3,5,5,8 is 5
        Assert.IsNotEmpty(result[0].SimilarWorkItems);
    }

    [TestMethod]
    public async Task Handle_WithVariableHistoricalEfforts_UsesPopulationVarianceForConfidence()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 2),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", 4),
            CreateWorkItem(3, "Task", "Done", "Sprint 1", 8),
            CreateWorkItem(4, "Task", "In Progress", "Sprint 2", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationSuggestionsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var expectedVariance = ((2d - (14d / 3d)) * (2d - (14d / 3d)) +
            (4d - (14d / 3d)) * (4d - (14d / 3d)) +
            (8d - (14d / 3d)) * (8d - (14d / 3d))) / 3d;
        var expectedVarianceConfidence = Math.Max(0.3d, 1d - (expectedVariance / 100d));
        var expectedConfidence = (Math.Min(1d, 3d / 10d) + expectedVarianceConfidence) / 2d;

        Assert.AreEqual(4, result[0].SuggestedEffort);
        Assert.AreEqual(expectedConfidence, result[0].Confidence, 0.0001);
    }

    [TestMethod]
    public async Task Handle_WithAreaPathFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItemWithArea(1, "Task", "In Progress", "Sprint 1", "Team\\A", null),
            CreateWorkItemWithArea(2, "Task", "In Progress", "Sprint 1", "Team\\B", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationSuggestionsQuery(null, "Team\\A");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result);
        Assert.AreEqual(1, result[0].WorkItemId);
    }

    [TestMethod]
    public async Task Handle_WithDifferentWorkItemTypes_ProvidesTypeSpecificSuggestions()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            // Historical data for different types
            CreateWorkItem(1, "Task", "Done", "Sprint 1", 3),
            CreateWorkItem(2, "Bug", "Done", "Sprint 1", 2),
            CreateWorkItem(3, "User Story", "Done", "Sprint 1", 8),
            // Items without effort
            CreateWorkItem(4, "Task", "In Progress", "Sprint 2", null),
            CreateWorkItem(5, "User Story", "In Progress", "Sprint 2", null)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortEstimationSuggestionsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result);
        var taskSuggestion = result.First(s => s.WorkItemType == "Task");
        var storySuggestion = result.First(s => s.WorkItemType == "User Story");

        Assert.AreEqual(3, taskSuggestion.SuggestedEffort);
        Assert.AreEqual(8, storySuggestion.SuggestedEffort);
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
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
                    Description: null,
                    Tags: null
        );
    }
}
