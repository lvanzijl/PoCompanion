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
public class GetEffortImbalanceQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<ILogger<GetEffortImbalanceQueryHandler>> _mockLogger = null!;
    private GetEffortImbalanceQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockLogger = new Mock<ILogger<GetEffortImbalanceQueryHandler>>();
        _handler = new GetEffortImbalanceQueryHandler(
            _mockRepository.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoWorkItems_ReturnsEmptyImbalance()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        var query = new GetEffortImbalanceQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ImbalanceRiskLevel.Low, result.OverallRiskLevel);
        Assert.AreEqual(0, result.ImbalanceScore);
        Assert.AreEqual(0, result.TeamImbalances.Count);
        Assert.AreEqual(0, result.SprintImbalances.Count);
        Assert.AreEqual(0, result.Recommendations.Count);
    }

    [TestMethod]
    public async Task Handle_WithBalancedDistribution_ReturnsLowRisk()
    {
        // Arrange - evenly distributed effort
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 10),
            CreateWorkItem(2, "Area2", "Sprint 1", 10),
            CreateWorkItem(3, "Area3", "Sprint 1", 10),
            CreateWorkItem(4, "Area1", "Sprint 2", 10),
            CreateWorkItem(5, "Area2", "Sprint 2", 10),
            CreateWorkItem(6, "Area3", "Sprint 2", 10)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortImbalanceQuery(ImbalanceThreshold: 0.3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ImbalanceRiskLevel.Low, result.OverallRiskLevel);
        Assert.IsTrue(result.ImbalanceScore < 30); // Less than 30% imbalance
    }

    [TestMethod]
    public async Task Handle_WithImbalancedTeams_DetectsHighRisk()
    {
        // Arrange - Team1 has 5x more effort than others
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 50),
            CreateWorkItem(2, "Team1", "Sprint 2", 50),
            CreateWorkItem(3, "Team2", "Sprint 1", 10),
            CreateWorkItem(4, "Team3", "Sprint 1", 10)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortImbalanceQuery(ImbalanceThreshold: 0.3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.OverallRiskLevel >= ImbalanceRiskLevel.Medium);
        Assert.IsTrue(result.TeamImbalances.Count > 0);
        Assert.IsTrue(result.TeamImbalances.Any(t => t.RiskLevel >= ImbalanceRiskLevel.High));
    }

    [TestMethod]
    public async Task Handle_WithImbalancedSprints_DetectsHighRisk()
    {
        // Arrange - Sprint1 has much more effort than Sprint2
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 40),
            CreateWorkItem(2, "Team2", "Sprint 1", 40),
            CreateWorkItem(3, "Team1", "Sprint 2", 5),
            CreateWorkItem(4, "Team2", "Sprint 2", 5)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortImbalanceQuery(ImbalanceThreshold: 0.3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.SprintImbalances.Count > 0);
        Assert.IsTrue(result.SprintImbalances.Any(s => s.RiskLevel >= ImbalanceRiskLevel.High));
    }

    [TestMethod]
    public async Task Handle_WithImbalance_GeneratesRecommendations()
    {
        // Arrange - heavily imbalanced
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "TeamOverloaded", "Sprint 1", 80),
            CreateWorkItem(2, "TeamNormal", "Sprint 1", 20),
            CreateWorkItem(3, "TeamUnderloaded", "Sprint 1", 5)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortImbalanceQuery(ImbalanceThreshold: 0.3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Recommendations.Count > 0);
        Assert.IsTrue(result.Recommendations.Any(r => r.Type == RecommendationType.ReduceTeamLoad));
        Assert.IsTrue(result.Recommendations.Any(r => r.Type == RecommendationType.IncreaseTeamLoad));
    }

    [TestMethod]
    public async Task Handle_WithAreaPathFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Project\\TeamA", "Sprint 1", 30),
            CreateWorkItem(2, "Project\\TeamB", "Sprint 1", 10),
            CreateWorkItem(3, "OtherProject\\TeamC", "Sprint 1", 50)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortImbalanceQuery(AreaPathFilter: "Project");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        // Should only analyze TeamA and TeamB, not TeamC
        Assert.IsTrue(result.TeamImbalances.All(t => t.AreaPath.StartsWith("Project")));
    }

    [TestMethod]
    public async Task Handle_WithCapacity_CalculatesUtilization()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 50),
            CreateWorkItem(2, "Team1", "Sprint 2", 20)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortImbalanceQuery(DefaultCapacityPerIteration: 40);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        // Sprint 1 has 50 points with 40 capacity = over capacity
        Assert.IsTrue(result.SprintImbalances.Any(s => 
            s.IterationPath == "Sprint 1" && s.TotalEffort > s.AverageEffortAcrossSprints));
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
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort
        );
    }
}
