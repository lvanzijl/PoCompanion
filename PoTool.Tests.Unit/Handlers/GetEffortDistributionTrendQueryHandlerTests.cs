using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetEffortDistributionTrendQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<ILogger<GetEffortDistributionTrendQueryHandler>> _mockLogger = null!;
    private IEffortTrendForecastService _effortTrendForecastService = null!;
    private GetEffortDistributionTrendQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<GetEffortDistributionTrendQueryHandler>>();
        _effortTrendForecastService = new EffortTrendForecastService();

        // Setup default mock behaviors
        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());

        _handler = new GetEffortDistributionTrendQueryHandler(
            _mockRepository.Object,
            _mockProductRepository.Object,
            _mockMediator.Object,
            _effortTrendForecastService,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoWorkItems_ReturnsEmptyTrend()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        var query = new GetEffortDistributionTrendQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(EffortTrendDirection.Stable, result.OverallTrend);
        Assert.AreEqual(0, result.TrendSlope);
        Assert.IsEmpty(result.TrendBySprint);
        Assert.IsEmpty(result.TrendByAreaPath);
        Assert.IsEmpty(result.Forecasts);
    }

    [TestMethod]
    public async Task Handle_WithIncreasingEffort_DetectsIncreasingTrend()
    {
        // Arrange - effort increasing over sprints
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 10),
            CreateWorkItem(2, "Team1", "Sprint 2", 20),
            CreateWorkItem(3, "Team1", "Sprint 3", 30),
            CreateWorkItem(4, "Team1", "Sprint 4", 40)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionTrendQuery(MaxIterations: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(EffortTrendDirection.Increasing, result.OverallTrend);
        Assert.IsGreaterThan(0d, result.TrendSlope);
        Assert.HasCount(4, result.TrendBySprint);
    }

    [TestMethod]
    public async Task Handle_WithDecreasingEffort_DetectsDecreasingTrend()
    {
        // Arrange - effort decreasing over sprints
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 40),
            CreateWorkItem(2, "Team1", "Sprint 2", 30),
            CreateWorkItem(3, "Team1", "Sprint 3", 20),
            CreateWorkItem(4, "Team1", "Sprint 4", 10)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionTrendQuery(MaxIterations: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(EffortTrendDirection.Decreasing, result.OverallTrend);
        Assert.IsLessThan(0d, result.TrendSlope);
    }

    [TestMethod]
    public async Task Handle_WithStableEffort_DetectsStableTrend()
    {
        // Arrange - consistent effort over sprints
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 25),
            CreateWorkItem(2, "Team1", "Sprint 2", 25),
            CreateWorkItem(3, "Team1", "Sprint 3", 25),
            CreateWorkItem(4, "Team1", "Sprint 4", 25)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionTrendQuery(MaxIterations: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(EffortTrendDirection.Stable, result.OverallTrend);
        Assert.IsLessThan(1d, Math.Abs(result.TrendSlope)); // Near zero
    }

    [TestMethod]
    public async Task Handle_WithVolatileEffort_DetectsVolatileTrend()
    {
        // Arrange - highly variable effort
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 10),
            CreateWorkItem(2, "Team1", "Sprint 2", 50),
            CreateWorkItem(3, "Team1", "Sprint 3", 5),
            CreateWorkItem(4, "Team1", "Sprint 4", 60),
            CreateWorkItem(5, "Team1", "Sprint 5", 15)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionTrendQuery(MaxIterations: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(EffortTrendDirection.Volatile, result.OverallTrend);
    }

    [TestMethod]
    public async Task Handle_WithSufficientHistory_GeneratesForecasts()
    {
        // Arrange - enough sprints for forecasting
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 20),
            CreateWorkItem(2, "Team1", "Sprint 2", 25),
            CreateWorkItem(3, "Team1", "Sprint 3", 30)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionTrendQuery(MaxIterations: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Forecasts);
        Assert.IsLessThanOrEqualTo(result.Forecasts.Count, 3); // Should forecast 3 future sprints
        Assert.IsTrue(result.Forecasts.All(f => f.ForecastedEffort > 0));
        Assert.IsTrue(result.Forecasts.All(f => f.ConfidenceLevel > 0 && f.ConfidenceLevel <= 1));
    }

    [TestMethod]
    public async Task Handle_WithInsufficientHistory_NoForecasts()
    {
        // Arrange - only 1 sprint, not enough for forecasting
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 20)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionTrendQuery(MaxIterations: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsEmpty(result.Forecasts);
    }

    [TestMethod]
    public async Task Handle_CalculatesChangeFromPrevious()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 20),
            CreateWorkItem(2, "Team1", "Sprint 2", 25), // 25% increase
            CreateWorkItem(3, "Team1", "Sprint 3", 20)  // 20% decrease
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionTrendQuery(MaxIterations: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(3, result.TrendBySprint);

        var sprint2 = result.TrendBySprint[1];
        Assert.IsTrue(sprint2.ChangeFromPrevious > 20 && sprint2.ChangeFromPrevious < 30);

        var sprint3 = result.TrendBySprint[2];
        Assert.IsTrue(sprint3.ChangeFromPrevious < -15 && sprint3.ChangeFromPrevious > -25);
    }

    [TestMethod]
    public async Task Handle_AnalyzesAreaPathTrends()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "TeamA", "Sprint 1", 10),
            CreateWorkItem(2, "TeamA", "Sprint 2", 20),
            CreateWorkItem(3, "TeamB", "Sprint 1", 30),
            CreateWorkItem(4, "TeamB", "Sprint 2", 30)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionTrendQuery(MaxIterations: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsGreaterThanOrEqualTo(result.TrendByAreaPath.Count, 2);

        var teamATrend = result.TrendByAreaPath.FirstOrDefault(t => t.AreaPath == "TeamA");
        Assert.IsNotNull(teamATrend);
        Assert.AreEqual(EffortTrendDirection.Increasing, teamATrend.Direction);

        var teamBTrend = result.TrendByAreaPath.FirstOrDefault(t => t.AreaPath == "TeamB");
        Assert.IsNotNull(teamBTrend);
        Assert.AreEqual(EffortTrendDirection.Stable, teamBTrend.Direction);
    }

    private static WorkItemDto CreateWorkItem(int id, string areaPath, string iterationPath, int effort)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: "Task",
            Title: $"Work Item {id}",
            ParentTfsId: null,
            AreaPath: areaPath,
            IterationPath: iterationPath,
            State: "New",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
                    Description: null,
                    Tags: null
        );
    }
}
