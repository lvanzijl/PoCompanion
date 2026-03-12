using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Tests for GetSprintTrendMetricsQueryHandler.
/// </summary>
[TestClass]
public class GetSprintTrendMetricsQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private Mock<SprintTrendProjectionService> _mockProjectionService = null!;
    private Mock<ILogger<GetSprintTrendMetricsQueryHandler>> _mockLogger = null!;
    private GetSprintTrendMetricsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new PoToolDbContext(options);

        _mockProjectionService = new Mock<SprintTrendProjectionService>(
            MockBehavior.Loose,
            null!,  // IServiceScopeFactory
            null!   // ILogger
        );
        _mockLogger = new Mock<ILogger<GetSprintTrendMetricsQueryHandler>>();

        _handler = new GetSprintTrendMetricsQueryHandler(
            _context,
            _mockProjectionService.Object,
            _mockLogger.Object);

        _mockProjectionService
            .Setup(p => p.ComputeFeatureProgressAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .ReturnsAsync(Array.Empty<FeatureProgressDto>());

        _mockProjectionService
            .Setup(p => p.ComputeEpicProgressAsync(
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<FeatureProgressDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EpicProgressDto>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    [Description("Should return empty metrics when no projections exist")]
    public async Task Handle_NoProjections_ReturnsEmptyMetrics()
    {
        // Arrange
        var query = new GetSprintTrendMetricsQuery(1, new[] { 1, 2, 3 }, false);
        
        _mockProjectionService
            .Setup(p => p.GetProjectionsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SprintMetricsProjectionEntity>());

        _mockProjectionService
            .Setup(p => p.ComputeProjectionsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SprintMetricsProjectionEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.IsEmpty(result.Metrics);
    }

    [TestMethod]
    [Description("Should return success false when exception occurs")]
    public async Task Handle_Exception_ReturnsFailure()
    {
        // Arrange
        var query = new GetSprintTrendMetricsQuery(1, new[] { 1 }, false);
        
        _mockProjectionService
            .Setup(p => p.GetProjectionsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "Test error", "Error message should contain exception text");
    }

    [TestMethod]
    [Description("Should call ComputeProjectionsAsync when recompute is true")]
    public async Task Handle_RecomputeTrue_CallsComputeProjections()
    {
        // Arrange
        var sprintIds = new[] { 1, 2 };
        var query = new GetSprintTrendMetricsQuery(1, sprintIds, true);
        
        _mockProjectionService
            .Setup(p => p.ComputeProjectionsAsync(1, sprintIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SprintMetricsProjectionEntity>())
            .Verifiable();

        _mockProjectionService
            .Setup(p => p.GetProjectionsAsync(1, sprintIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SprintMetricsProjectionEntity>());

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockProjectionService.Verify(
            p => p.ComputeProjectionsAsync(1, sprintIds, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockProjectionService.Verify(
            p => p.GetProjectionsAsync(1, sprintIds, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    [Description("Should not call ComputeProjectionsAsync when projections already exist and recompute is false")]
    public async Task Handle_RecomputeFalse_WithExistingProjections_DoesNotCallComputeProjections()
    {
        // Arrange
        var query = new GetSprintTrendMetricsQuery(1, new[] { 1 }, false);
        
        _mockProjectionService
            .Setup(p => p.GetProjectionsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SprintMetricsProjectionEntity>
            {
                new()
                {
                    SprintId = 1,
                    ProductId = 1,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 1
                }
            });

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockProjectionService.Verify(
            p => p.ComputeProjectionsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    [Description("Should recompute projections when cached projections are missing")]
    public async Task Handle_RecomputeFalse_WithEmptyCachedProjections_RecomputesAndReturnsData()
    {
        // Arrange
        var sprintIds = new[] { 1 };
        var query = new GetSprintTrendMetricsQuery(1, sprintIds, false);

        var team = new TeamEntity { Name = "Test Team", TeamAreaPath = "Project\\Team" };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        var sprint = new SprintEntity
        {
            Name = "Sprint 1",
            Path = "Project\\Sprint 1",
            TeamId = team.Id
        };
        _context.Sprints.Add(sprint);

        var product = new ProductEntity
        {
            Name = "Product A",
            BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 100 } },
            ProductOwnerId = 1
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var computedProjections = new List<SprintMetricsProjectionEntity>
        {
            new()
            {
                SprintId = sprint.Id,
                ProductId = product.Id,
                PlannedCount = 3,
                WorkedCount = 2,
                LastComputedAt = DateTimeOffset.UtcNow,
                IncludedUpToRevisionId = 1
            }
        };

        _mockProjectionService
            .Setup(p => p.GetProjectionsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SprintMetricsProjectionEntity>());

        _mockProjectionService
            .Setup(p => p.ComputeProjectionsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(computedProjections);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.HasCount(1, result.Metrics);

        _mockProjectionService.Verify(
            p => p.ComputeProjectionsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockProjectionService.Verify(
            p => p.GetProjectionsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    [Description("Should aggregate metrics across products when multiple products exist")]
    public async Task Handle_MultipleProducts_AggregatesMetricsCorrectly()
    {
        // Arrange - Set up sprint and products in database
        var team = new TeamEntity { Name = "Test Team", TeamAreaPath = "Project\\Team" };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        var sprint = new SprintEntity 
        { 
            Name = "Sprint 1", 
            Path = "Project\\Sprint 1",
            TeamId = team.Id,
            StartUtc = DateTimeOffset.UtcNow.AddDays(-14),
            EndUtc = DateTimeOffset.UtcNow
        };
        _context.Sprints.Add(sprint);
        await _context.SaveChangesAsync();

        var product1 = new ProductEntity 
        { 
            Name = "Product A", 
            BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 100 } },
            ProductOwnerId = 1
        };
        var product2 = new ProductEntity 
        { 
            Name = "Product B", 
            BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 200 } },
            ProductOwnerId = 1
        };
        _context.Products.AddRange(product1, product2);
        await _context.SaveChangesAsync();

        var projections = new List<SprintMetricsProjectionEntity>
        {
            new() 
            { 
                SprintId = sprint.Id, 
                ProductId = product1.Id,
                Sprint = sprint,
                Product = product1,
                PlannedCount = 10, 
                PlannedEffort = 30,
                WorkedCount = 8,
                WorkedEffort = 25,
                BugsPlannedCount = 2,
                BugsWorkedCount = 1,
                LastComputedAt = DateTimeOffset.UtcNow,
                IncludedUpToRevisionId = 100
            },
            new() 
            { 
                SprintId = sprint.Id, 
                ProductId = product2.Id,
                Sprint = sprint,
                Product = product2,
                PlannedCount = 5, 
                PlannedEffort = 15,
                WorkedCount = 4,
                WorkedEffort = 12,
                BugsPlannedCount = 3,
                BugsWorkedCount = 2,
                LastComputedAt = DateTimeOffset.UtcNow,
                IncludedUpToRevisionId = 100
            }
        };

        var query = new GetSprintTrendMetricsQuery(1, new[] { sprint.Id }, false);
        
        _mockProjectionService
            .Setup(p => p.GetProjectionsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projections);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.HasCount(1, result.Metrics);

        var sprintMetrics = result.Metrics[0];
        Assert.AreEqual(sprint.Id, sprintMetrics.SprintId);
        Assert.AreEqual("Sprint 1", sprintMetrics.SprintName);
        
        // Check aggregated totals
        Assert.AreEqual(15, sprintMetrics.TotalPlannedCount, "Total planned count should be 10 + 5");
        Assert.AreEqual(45, sprintMetrics.TotalPlannedEffort, "Total planned effort should be 30 + 15");
        Assert.AreEqual(12, sprintMetrics.TotalWorkedCount, "Total worked count should be 8 + 4");
        Assert.AreEqual(37, sprintMetrics.TotalWorkedEffort, "Total worked effort should be 25 + 12");
        Assert.AreEqual(5, sprintMetrics.TotalBugsPlannedCount, "Total bugs planned should be 2 + 3");
        Assert.AreEqual(3, sprintMetrics.TotalBugsWorkedCount, "Total bugs worked should be 1 + 2");

        // Check product-level metrics
        Assert.HasCount(2, sprintMetrics.ProductMetrics);
    }

    [TestMethod]
    [Description("Should return ordered metrics by sprint start date")]
    public async Task Handle_MultipleSprints_ReturnsOrderedByStartDate()
    {
        // Arrange - Set up sprints in database
        var team = new TeamEntity { Name = "Test Team", TeamAreaPath = "Project\\Team" };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        var sprint1 = new SprintEntity 
        { 
            Name = "Sprint 1", 
            Path = "Project\\Sprint 1",
            TeamId = team.Id,
            StartUtc = DateTimeOffset.UtcNow.AddDays(-28),
            EndUtc = DateTimeOffset.UtcNow.AddDays(-14)
        };
        var sprint2 = new SprintEntity 
        { 
            Name = "Sprint 2", 
            Path = "Project\\Sprint 2",
            TeamId = team.Id,
            StartUtc = DateTimeOffset.UtcNow.AddDays(-14),
            EndUtc = DateTimeOffset.UtcNow
        };
        _context.Sprints.AddRange(sprint1, sprint2);
        await _context.SaveChangesAsync();

        var product = new ProductEntity 
        { 
            Name = "Product", 
            BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 100 } },
            ProductOwnerId = 1
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Return sprint2 first (wrong order) to verify handler sorts them
        var projections = new List<SprintMetricsProjectionEntity>
        {
            new() 
            { 
                SprintId = sprint2.Id, 
                ProductId = product.Id,
                Sprint = sprint2,
                Product = product,
                PlannedCount = 5,
                LastComputedAt = DateTimeOffset.UtcNow,
                IncludedUpToRevisionId = 100
            },
            new() 
            { 
                SprintId = sprint1.Id, 
                ProductId = product.Id,
                Sprint = sprint1,
                Product = product,
                PlannedCount = 10,
                LastComputedAt = DateTimeOffset.UtcNow,
                IncludedUpToRevisionId = 50
            }
        };

        var query = new GetSprintTrendMetricsQuery(1, new[] { sprint1.Id, sprint2.Id }, false);
        
        _mockProjectionService
            .Setup(p => p.GetProjectionsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projections);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.HasCount(2, result.Metrics);
        
        // Verify ordering by start date (sprint1 should come first)
        Assert.AreEqual(sprint1.Id, result.Metrics[0].SprintId, "Sprint 1 should be first (earlier start date)");
        Assert.AreEqual(sprint2.Id, result.Metrics[1].SprintId, "Sprint 2 should be second (later start date)");
    }

    [TestMethod]
    [Description("Should populate summary delivery rollups without returning drilldown detail when includeDetails is false")]
    public async Task Handle_IncludeDetailsFalse_ReturnsSummaryRollupsOnly()
    {
        var team = new TeamEntity { Name = "Test Team", TeamAreaPath = "Project\\Team" };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        var sprint = new SprintEntity
        {
            Name = "Sprint 1",
            Path = "Project\\Sprint 1",
            TeamId = team.Id,
            StartUtc = DateTimeOffset.UtcNow.AddDays(-14),
            StartDateUtc = DateTime.UtcNow.AddDays(-14),
            EndUtc = DateTimeOffset.UtcNow,
            EndDateUtc = DateTime.UtcNow
        };
        _context.Sprints.Add(sprint);

        var product = new ProductEntity
        {
            Name = "Product A",
            BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 100 } },
            ProductOwnerId = 1
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _mockProjectionService
            .Setup(p => p.GetProjectionsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SprintMetricsProjectionEntity>
            {
                new()
                {
                    SprintId = sprint.Id,
                    ProductId = product.Id,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 1
                }
            });

        _mockProjectionService
            .Setup(p => p.ComputeFeatureProgressAsync(
                1,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>(),
                sprint.Id))
            .ReturnsAsync(new List<FeatureProgressDto>
            {
                new()
                {
                    FeatureId = 200,
                    FeatureTitle = "Feature A",
                    ProductId = product.Id
                }
            });

        _mockProjectionService
            .Setup(p => p.ComputeEpicProgressAsync(
                1,
                It.IsAny<IReadOnlyList<FeatureProgressDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EpicProgressDto>
            {
                new()
                {
                    EpicId = 300,
                    EpicTitle = "Epic A",
                    ProductId = product.Id,
                    SprintEffortDelta = 8,
                    SprintCompletedFeatureCount = 2
                }
            });

        var query = new GetSprintTrendMetricsQuery(1, new[] { sprint.Id }, false, false);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.HasCount(1, result.Metrics);
        Assert.IsNotNull(result.FeatureProgress);
        Assert.IsNotNull(result.EpicProgress);
        Assert.IsEmpty(result.FeatureProgress);
        Assert.IsEmpty(result.EpicProgress);
        Assert.AreEqual(8, result.Metrics[0].ProductMetrics[0].ScopeChangeEffort);
        Assert.AreEqual(2, result.Metrics[0].ProductMetrics[0].CompletedFeatureCount);
    }

    [TestMethod]
    [Description("Should return drilldown detail when includeDetails is true")]
    public async Task Handle_IncludeDetailsTrue_ReturnsDrilldownDetail()
    {
        var team = new TeamEntity { Name = "Test Team", TeamAreaPath = "Project\\Team" };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        var sprint = new SprintEntity
        {
            Name = "Sprint 1",
            Path = "Project\\Sprint 1",
            TeamId = team.Id,
            StartUtc = DateTimeOffset.UtcNow.AddDays(-14),
            StartDateUtc = DateTime.UtcNow.AddDays(-14),
            EndUtc = DateTimeOffset.UtcNow,
            EndDateUtc = DateTime.UtcNow
        };
        _context.Sprints.Add(sprint);

        var product = new ProductEntity
        {
            Name = "Product A",
            BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 100 } },
            ProductOwnerId = 1
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var featureProgress = new List<FeatureProgressDto>
        {
            new()
            {
                FeatureId = 200,
                FeatureTitle = "Feature A",
                ProductId = product.Id
            }
        };

        var epicProgress = new List<EpicProgressDto>
        {
            new()
            {
                EpicId = 300,
                EpicTitle = "Epic A",
                ProductId = product.Id
            }
        };

        _mockProjectionService
            .Setup(p => p.GetProjectionsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SprintMetricsProjectionEntity>
            {
                new()
                {
                    SprintId = sprint.Id,
                    ProductId = product.Id,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 1
                }
            });

        _mockProjectionService
            .Setup(p => p.ComputeFeatureProgressAsync(
                1,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>(),
                sprint.Id))
            .ReturnsAsync(featureProgress);

        _mockProjectionService
            .Setup(p => p.ComputeEpicProgressAsync(
                1,
                It.IsAny<IReadOnlyList<FeatureProgressDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(epicProgress);

        var query = new GetSprintTrendMetricsQuery(1, new[] { sprint.Id }, false, true);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.FeatureProgress);
        Assert.IsNotNull(result.EpicProgress);
        Assert.HasCount(1, result.FeatureProgress);
        Assert.HasCount(1, result.EpicProgress);
    }
}
