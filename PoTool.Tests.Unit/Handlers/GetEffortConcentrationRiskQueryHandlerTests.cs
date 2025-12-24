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
public class GetEffortConcentrationRiskQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<ILogger<GetEffortConcentrationRiskQueryHandler>> _mockLogger = null!;
    private GetEffortConcentrationRiskQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockLogger = new Mock<ILogger<GetEffortConcentrationRiskQueryHandler>>();
        _handler = new GetEffortConcentrationRiskQueryHandler(
            _mockRepository.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoWorkItems_ReturnsNoRisk()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        var query = new GetEffortConcentrationRiskQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ConcentrationRiskLevel.None, result.OverallRiskLevel);
        Assert.AreEqual(0, result.ConcentrationIndex);
        Assert.IsEmpty(result.AreaPathRisks);
        Assert.IsEmpty(result.IterationRisks);
        Assert.IsEmpty(result.Recommendations);
    }

    [TestMethod]
    public async Task Handle_WithWellDistributedEffort_ReturnsLowRisk()
    {
        // Arrange - evenly distributed across 5 areas and 5 sprints (10% each)
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 10),
            CreateWorkItem(2, "Area2", "Sprint 2", 10),
            CreateWorkItem(3, "Area3", "Sprint 3", 10),
            CreateWorkItem(4, "Area4", "Sprint 4", 10),
            CreateWorkItem(5, "Area5", "Sprint 5", 10)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortConcentrationRiskQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        // Well distributed - no single entity has > 25% of total
#pragma warning disable MSTEST0037 // Enum comparison not supported by Assert.IsLessThanOrEqualTo
        Assert.IsTrue(result.OverallRiskLevel <= ConcentrationRiskLevel.Medium);
#pragma warning restore MSTEST0037
        Assert.IsLessThan(result.ConcentrationIndex, 70);
    }

    [TestMethod]
    public async Task Handle_WithHighAreaPathConcentration_DetectsCriticalRisk()
    {
        // Arrange - 90% of effort in one area
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "DominantArea", "Sprint 1", 90),
            CreateWorkItem(2, "SmallArea1", "Sprint 1", 5),
            CreateWorkItem(3, "SmallArea2", "Sprint 1", 5)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortConcentrationRiskQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ConcentrationRiskLevel.Critical, result.OverallRiskLevel);
        Assert.IsNotEmpty(result.AreaPathRisks);
        var dominantArea = result.AreaPathRisks.First();
        Assert.AreEqual(ConcentrationRiskLevel.Critical, dominantArea.RiskLevel);
        Assert.IsGreaterThan(dominantArea.PercentageOfTotal, 80);
    }

    [TestMethod]
    public async Task Handle_WithHighIterationConcentration_DetectsHighRisk()
    {
        // Arrange - 70% of effort in one sprint
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 70),
            CreateWorkItem(2, "Team1", "Sprint 2", 20),
            CreateWorkItem(3, "Team1", "Sprint 3", 10)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortConcentrationRiskQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
#pragma warning disable MSTEST0037 // Enum comparison
        Assert.IsTrue(result.OverallRiskLevel >= ConcentrationRiskLevel.High);
#pragma warning restore MSTEST0037
        Assert.IsNotEmpty(result.IterationRisks);
        var highRiskSprint = result.IterationRisks.First();
#pragma warning disable MSTEST0037 // Enum comparison
        Assert.IsTrue(highRiskSprint.RiskLevel >= ConcentrationRiskLevel.High);
#pragma warning restore MSTEST0037
    }

    [TestMethod]
    public async Task Handle_WithMediumConcentration_DetectsMediumRisk()
    {
        // Arrange - 50% in one area, 50% split among others
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "MainArea", "Sprint 1", 50),
            CreateWorkItem(2, "Area2", "Sprint 1", 20),
            CreateWorkItem(3, "Area3", "Sprint 1", 20),
            CreateWorkItem(4, "Area4", "Sprint 1", 10)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortConcentrationRiskQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
#pragma warning disable MSTEST0037 // Enum comparison
        Assert.IsTrue(result.OverallRiskLevel >= ConcentrationRiskLevel.Medium);
#pragma warning restore MSTEST0037
        var mainArea = result.AreaPathRisks.FirstOrDefault(r => r.Path == "MainArea");
        Assert.IsNotNull(mainArea);
        Assert.AreEqual(ConcentrationRiskLevel.Medium, mainArea.RiskLevel);
    }

    [TestMethod]
    public async Task Handle_GeneratesMitigationRecommendations()
    {
        // Arrange - concentrated effort
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "ConcentratedArea", "Sprint 1", 80),
            CreateWorkItem(2, "OtherArea", "Sprint 1", 20)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortConcentrationRiskQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Recommendations);
        Assert.IsTrue(result.Recommendations.Any(r => 
            r.Strategy == MitigationStrategy.DiversifyAcrossAreas));
    }

    [TestMethod]
    public async Task Handle_IncludesTopWorkItemsInRisk()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "HighRiskArea", "Sprint 1", 30),
            CreateWorkItem(2, "HighRiskArea", "Sprint 1", 25),
            CreateWorkItem(3, "HighRiskArea", "Sprint 1", 20),
            CreateWorkItem(4, "OtherArea", "Sprint 1", 5)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortConcentrationRiskQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var highRiskArea = result.AreaPathRisks.First();
        Assert.IsNotEmpty(highRiskArea.TopWorkItems);
        Assert.IsLessThanOrEqualTo(highRiskArea.TopWorkItems.Count, 5);
    }

    [TestMethod]
    public async Task Handle_CalculatesConcentrationIndex()
    {
        // Arrange - two scenarios: concentrated vs distributed
        
        // Scenario 1: Concentrated (should have high HHI) - 90% in one area, one sprint
        var concentratedWorkItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 90),
            CreateWorkItem(2, "Area2", "Sprint 2", 10)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(concentratedWorkItems);
        var query1 = new GetEffortConcentrationRiskQuery();

        var result1 = await _handler.Handle(query1, CancellationToken.None);

        // Scenario 2: Distributed (should have low HHI) - 20% in each area/sprint
        var distributedWorkItems = new List<WorkItemDto>
        {
            CreateWorkItem(3, "Area3", "Sprint 3", 20),
            CreateWorkItem(4, "Area4", "Sprint 4", 20),
            CreateWorkItem(5, "Area5", "Sprint 5", 20),
            CreateWorkItem(6, "Area6", "Sprint 6", 20),
            CreateWorkItem(7, "Area7", "Sprint 7", 20)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(distributedWorkItems);
        var query2 = new GetEffortConcentrationRiskQuery();

        var result2 = await _handler.Handle(query2, CancellationToken.None);

        // Assert - concentrated should have higher risk than distributed
#pragma warning disable MSTEST0037 // Enum comparison not supported by Assert.IsGreaterThan
        Assert.IsTrue(result1.OverallRiskLevel > result2.OverallRiskLevel);
#pragma warning restore MSTEST0037
        Assert.IsGreaterThanOrEqualTo(result1.ConcentrationIndex, result2.ConcentrationIndex);
    }

    [TestMethod]
    public async Task Handle_WithAreaPathFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Project\\TeamA", "Sprint 1", 50),
            CreateWorkItem(2, "Project\\TeamB", "Sprint 1", 30),
            CreateWorkItem(3, "OtherProject\\TeamC", "Sprint 1", 80)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortConcentrationRiskQuery(AreaPathFilter: "Project");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        // Should only analyze Project teams, not OtherProject
        Assert.IsTrue(result.AreaPathRisks.All(r => r.Path.StartsWith("Project")));
    }

    [TestMethod]
    public async Task Handle_CriticalRisk_IncludesUrgentRecommendation()
    {
        // Arrange - extremely concentrated (95%+ in one area)
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "SingleArea", "Sprint 1", 95),
            CreateWorkItem(2, "TinyArea", "Sprint 1", 5)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortConcentrationRiskQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(ConcentrationRiskLevel.Critical, result.OverallRiskLevel);
        Assert.IsTrue(result.Recommendations.Any(r => 
            r.Priority == ConcentrationRiskLevel.Critical));
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
