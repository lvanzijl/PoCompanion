using Mediator;
using EffortDiagnosticsAnalyzer = PoTool.Core.Metrics.EffortDiagnostics.EffortDiagnosticsAnalyzer;
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
public class GetEffortImbalanceQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<ILogger<GetEffortImbalanceQueryHandler>> _mockLogger = null!;
    private GetEffortImbalanceQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<GetEffortImbalanceQueryHandler>>();

        // Setup default mock behaviors
        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());

        _handler = new GetEffortImbalanceQueryHandler(
            _mockRepository.Object,
            _mockProductRepository.Object,
            _mockMediator.Object,
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
        Assert.AreEqual(PoTool.Shared.Metrics.ImbalanceRiskLevel.Low, result.OverallRiskLevel);
        Assert.AreEqual(0, result.ImbalanceScore);
        Assert.IsEmpty(result.TeamImbalances);
        Assert.IsEmpty(result.SprintImbalances);
        Assert.IsEmpty(result.Recommendations);
    }

    [TestMethod]
    public async Task Handle_UsesAnalyzerDerivedBucketValues_ForImbalanceOutput()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area A", "Sprint 1", 15),
            CreateWorkItem(2, "Area A", "Sprint 2", 15),
            CreateWorkItem(3, "Area B", "Sprint 1", 10),
            CreateWorkItem(4, "Area C", "Sprint 2", 10)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _handler.Handle(
            new GetEffortImbalanceQuery(ImbalanceThreshold: 0.3),
            CancellationToken.None);
        var analyzer = new EffortDiagnosticsAnalyzer();
        var expectedAnalysis = analyzer.AnalyzeImbalance(
            new Dictionary<string, int>
            {
                ["Area A"] = 30,
                ["Area B"] = 10,
                ["Area C"] = 10
            },
            new Dictionary<string, int>
            {
                ["Sprint 1"] = 25,
                ["Sprint 2"] = 25
            },
            0.3);

        Assert.AreEqual(MapRisk(expectedAnalysis.OverallRiskLevel), result.OverallRiskLevel);
        Assert.AreEqual(expectedAnalysis.ImbalanceScore, result.ImbalanceScore, 0.001);
        Assert.IsEmpty(result.SprintImbalances);

        var expectedDominantTeam = expectedAnalysis.AreaBuckets.Single(bucket => bucket.BucketKey == "Area A");
        var dominantTeam = result.TeamImbalances.Single(team => team.AreaPath == "Area A");
        Assert.AreEqual((int)expectedDominantTeam.EffortAmount, dominantTeam.TotalEffort);
        Assert.AreEqual((int)expectedDominantTeam.MeanEffort, dominantTeam.AverageEffortAcrossTeams);
        Assert.AreEqual(expectedDominantTeam.DeviationFromMean * 100, dominantTeam.DeviationPercentage, 0.001);
        Assert.AreEqual(MapRisk(expectedDominantTeam.RiskLevel), dominantTeam.RiskLevel);
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
        Assert.IsNotEmpty(result.Recommendations);
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
        var sprint = result.SprintImbalances.Single(s => s.IterationPath == "Sprint 1");
        StringAssert.Contains(sprint.Description, "capacity utilization");
    }

    [TestMethod]
    public async Task Handle_DefaultCapacityOnlyAddsDescriptionContext()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Team1", "Sprint 1", 50),
            CreateWorkItem(2, "Team1", "Sprint 2", 20)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        // Act
        var withoutCapacity = await _handler.Handle(
            new GetEffortImbalanceQuery(DefaultCapacityPerIteration: null),
            CancellationToken.None);
        var withCapacity = await _handler.Handle(
            new GetEffortImbalanceQuery(DefaultCapacityPerIteration: 40),
            CancellationToken.None);

        // Assert
        Assert.AreEqual(withoutCapacity.OverallRiskLevel, withCapacity.OverallRiskLevel);
        Assert.AreEqual(withoutCapacity.ImbalanceScore, withCapacity.ImbalanceScore, 0.001);

        var sprintWithoutCapacity = withoutCapacity.SprintImbalances.Single(s => s.IterationPath == "Sprint 1");
        var sprintWithCapacity = withCapacity.SprintImbalances.Single(s => s.IterationPath == "Sprint 1");

        Assert.AreEqual(sprintWithoutCapacity.RiskLevel, sprintWithCapacity.RiskLevel);
        Assert.IsFalse(
            sprintWithoutCapacity.Description.Contains("capacity utilization", StringComparison.Ordinal),
            "Descriptions without default capacity should not include utilization context.");
        StringAssert.Contains(sprintWithCapacity.Description, "capacity utilization");
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

    private static PoTool.Shared.Metrics.ImbalanceRiskLevel MapRisk(PoTool.Core.Metrics.EffortDiagnostics.ImbalanceRiskLevel riskLevel)
    {
        return riskLevel switch
        {
            PoTool.Core.Metrics.EffortDiagnostics.ImbalanceRiskLevel.Low => PoTool.Shared.Metrics.ImbalanceRiskLevel.Low,
            PoTool.Core.Metrics.EffortDiagnostics.ImbalanceRiskLevel.Medium => PoTool.Shared.Metrics.ImbalanceRiskLevel.Medium,
            PoTool.Core.Metrics.EffortDiagnostics.ImbalanceRiskLevel.High => PoTool.Shared.Metrics.ImbalanceRiskLevel.High,
            _ => PoTool.Shared.Metrics.ImbalanceRiskLevel.Critical
        };
    }
}
