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
public class GetEffortDistributionQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<ILogger<GetEffortDistributionQueryHandler>> _mockLogger = null!;
    private GetEffortDistributionQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockLogger = new Mock<ILogger<GetEffortDistributionQueryHandler>>();
        _handler = new GetEffortDistributionQueryHandler(
            _mockRepository.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoWorkItems_ReturnsEmptyDistribution()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        var query = new GetEffortDistributionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.TotalEffort);
        Assert.IsEmpty(result.EffortByArea);
        Assert.IsEmpty(result.EffortByIteration);
        Assert.IsEmpty(result.HeatMapData);
    }

    [TestMethod]
    public async Task Handle_WithValidWorkItems_CalculatesDistributionCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 5),
            CreateWorkItem(2, "Area1", "Sprint 1", 8),
            CreateWorkItem(3, "Area2", "Sprint 1", 3),
            CreateWorkItem(4, "Area1", "Sprint 2", 13)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(29, result.TotalEffort); // 5 + 8 + 3 + 13
        Assert.IsNotEmpty(result.EffortByArea);
        Assert.IsNotEmpty(result.EffortByIteration);
    }

    [TestMethod]
    public async Task Handle_WithAreaPathFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Project\\TeamA", "Sprint 1", 5),
            CreateWorkItem(2, "Project\\TeamA\\SubTeam", "Sprint 1", 8),
            CreateWorkItem(3, "Project\\TeamB", "Sprint 1", 3),
            CreateWorkItem(4, "OtherProject", "Sprint 1", 13)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery(AreaPathFilter: "Project\\TeamA");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(13, result.TotalEffort); // 5 + 8 (TeamA and subteam)
    }

    [TestMethod]
    public async Task Handle_WithMaxIterationsLimit_LimitsCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>();
        for (int i = 1; i <= 15; i++)
        {
            workItems.Add(CreateWorkItem(i, "Area1", $"Sprint {i}", 5));
        }

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery(MaxIterations: 5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsLessThanOrEqualTo(result.EffortByIteration.Count, 5);
    }

    [TestMethod]
    public async Task Handle_WithDefaultCapacity_CalculatesUtilization()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 40),  // 40 effort
            CreateWorkItem(2, "Area1", "Sprint 1", 45),  // 45 effort
            CreateWorkItem(3, "Area1", "Sprint 2", 25)   // 25 effort
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery(DefaultCapacityPerIteration: 50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var sprint1 = result.EffortByIteration.First(e => e.IterationPath == "Sprint 1");
        Assert.AreEqual(85, sprint1.TotalEffort);
        Assert.AreEqual(50, sprint1.Capacity);
        Assert.AreEqual(170.0, sprint1.UtilizationPercentage); // (85/50)*100
    }

    [TestMethod]
    public async Task Handle_WithoutDefaultCapacity_ReturnsZeroUtilization()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 40)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var sprint1 = result.EffortByIteration.First();
        Assert.AreEqual(0.0, sprint1.UtilizationPercentage);
    }

    [TestMethod]
    public async Task Handle_CalculatesHeatMapCells()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 30),
            CreateWorkItem(2, "Area1", "Sprint 2", 50),
            CreateWorkItem(3, "Area2", "Sprint 1", 40),
            CreateWorkItem(4, "Area2", "Sprint 2", 60)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery(DefaultCapacityPerIteration: 50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.HeatMapData);
        
        // Check specific cell
        var area1Sprint1 = result.HeatMapData.FirstOrDefault(c => 
            c.AreaPath == "Area1" && c.IterationPath == "Sprint 1");
        Assert.IsNotNull(area1Sprint1);
        Assert.AreEqual(30, area1Sprint1.Effort);
    }

    [TestMethod]
    public async Task Handle_DeterminesCapacityStatusCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 20),  // 40% - Underutilized
            CreateWorkItem(2, "Area2", "Sprint 1", 35),  // 70% - Normal
            CreateWorkItem(3, "Area3", "Sprint 1", 45),  // 90% - NearCapacity
            CreateWorkItem(4, "Area4", "Sprint 1", 55)   // 110% - OverCapacity
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery(DefaultCapacityPerIteration: 50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var area1Cell = result.HeatMapData.First(c => c.AreaPath == "Area1" && c.IterationPath == "Sprint 1");
        Assert.AreEqual(CapacityStatus.Underutilized, area1Cell.Status);
        
        var area2Cell = result.HeatMapData.First(c => c.AreaPath == "Area2" && c.IterationPath == "Sprint 1");
        Assert.AreEqual(CapacityStatus.Normal, area2Cell.Status);
        
        var area3Cell = result.HeatMapData.First(c => c.AreaPath == "Area3" && c.IterationPath == "Sprint 1");
        Assert.AreEqual(CapacityStatus.NearCapacity, area3Cell.Status);
        
        var area4Cell = result.HeatMapData.First(c => c.AreaPath == "Area4" && c.IterationPath == "Sprint 1");
        Assert.AreEqual(CapacityStatus.OverCapacity, area4Cell.Status);
    }

    [TestMethod]
    public async Task Handle_WithNoCapacity_ReturnsUnknownStatus()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 50)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var cell = result.HeatMapData.First();
        Assert.AreEqual(CapacityStatus.Unknown, cell.Status);
    }

    [TestMethod]
    public async Task Handle_WithZeroEffortItems_ExcludesFromCalculation()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 5),
            CreateWorkItem(2, "Area1", "Sprint 1", 0),  // Zero effort - excluded
            CreateWorkItem(3, "Area2", "Sprint 1", 8)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(13, result.TotalEffort); // 5 + 8 (0 excluded)
    }

    [TestMethod]
    public async Task Handle_WithNullEffortItems_ExcludesFromCalculation()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 5),
            CreateWorkItem(2, "Area1", "Sprint 1", null),  // Null effort - excluded
            CreateWorkItem(3, "Area2", "Sprint 1", 8)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(13, result.TotalEffort); // 5 + 8 (null excluded)
    }

    [TestMethod]
    public async Task Handle_LimitsToTop10AreaPaths()
    {
        // Arrange
        var workItems = new List<WorkItemDto>();
        for (int i = 1; i <= 15; i++)
        {
            // Create work items with varying counts per area
            for (int j = 0; j < i; j++)
            {
                workItems.Add(CreateWorkItem(i * 100 + j, $"Area{i}", "Sprint 1", 5));
            }
        }

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsLessThanOrEqualTo(result.EffortByArea.Count, 10);
    }

    [TestMethod]
    public async Task Handle_CalculatesAverageEffortPerItem()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Sprint 1", 10),
            CreateWorkItem(2, "Area1", "Sprint 1", 20),
            CreateWorkItem(3, "Area1", "Sprint 1", 30)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var area1 = result.EffortByArea.First(a => a.AreaPath == "Area1");
        Assert.AreEqual(60, area1.TotalEffort);
        Assert.AreEqual(3, area1.WorkItemCount);
        Assert.AreEqual(20.0, area1.AverageEffortPerItem);
    }

    [TestMethod]
    public async Task Handle_ExtractsSprintNameFromIterationPath()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Area1", "Project\\Team\\2024\\Sprint 5", 10)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var iteration = result.EffortByIteration.First();
        Assert.AreEqual("Sprint 5", iteration.SprintName);
        Assert.AreEqual("Project\\Team\\2024\\Sprint 5", iteration.IterationPath);
    }

    [TestMethod]
    public async Task Handle_WithCaseInsensitiveAreaPath_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PROJECT\\TEAM", "Sprint 1", 5),
            CreateWorkItem(2, "Project\\Team", "Sprint 1", 8),
            CreateWorkItem(3, "project\\team", "Sprint 1", 3)
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetEffortDistributionQuery(AreaPathFilter: "Project\\Team");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(16, result.TotalEffort); // All three items match case-insensitively
    }

    private static WorkItemDto CreateWorkItem(
        int id,
        string areaPath,
        string iterationPath,
        int? effort)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: "PBI",
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
