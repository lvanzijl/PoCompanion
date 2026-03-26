using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
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
    private TestSprintTrendProjectionService _projectionService = null!;
    private Mock<ILogger<GetSprintTrendMetricsQueryHandler>> _mockLogger = null!;
    private GetSprintTrendMetricsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new PoToolDbContext(options);

        _projectionService = new TestSprintTrendProjectionService();
        _mockLogger = new Mock<ILogger<GetSprintTrendMetricsQueryHandler>>();

        _handler = new GetSprintTrendMetricsQueryHandler(
            _context,
            _projectionService,
            new ProductAggregationService(),
            new PlanningQualityService(),
            new SnapshotComparisonService(),
            new InsightService(),
            _mockLogger.Object);
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
        var query = new GetSprintTrendMetricsQuery(1, new[] { 1, 2, 3 }, false);

        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(Array.Empty<SprintMetricsProjectionEntity>());
        _projectionService.ComputeProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(Array.Empty<SprintMetricsProjectionEntity>());

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.IsEmpty(result.Metrics);
    }

    [TestMethod]
    [Description("Should return success false when exception occurs")]
    public async Task Handle_Exception_ReturnsFailure()
    {
        var query = new GetSprintTrendMetricsQuery(1, new[] { 1 }, false);

        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            throw new InvalidOperationException("Test error");

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "Test error", "Error message should contain exception text");
    }

    [TestMethod]
    [Description("Should call ComputeProjectionsAsync when recompute is true")]
    public async Task Handle_RecomputeTrue_CallsComputeProjections()
    {
        var sprintIds = new[] { 1, 2 };
        var query = new GetSprintTrendMetricsQuery(1, sprintIds, true);

        _projectionService.ComputeProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(Array.Empty<SprintMetricsProjectionEntity>());
        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(Array.Empty<SprintMetricsProjectionEntity>());

        await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(1, _projectionService.ComputeProjectionsCallCount);
        Assert.AreEqual(0, _projectionService.GetProjectionsCallCount);
        Assert.AreEqual(1, _projectionService.LastComputeProductOwnerId);
        CollectionAssert.AreEqual(sprintIds, _projectionService.LastComputeSprintIds.ToArray());
    }

    [TestMethod]
    [Description("Should not call ComputeProjectionsAsync when projections already exist and recompute is false")]
    public async Task Handle_RecomputeFalse_WithExistingProjections_DoesNotCallComputeProjections()
    {
        var query = new GetSprintTrendMetricsQuery(1, new[] { 1 }, false);

        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(
            [
                new SprintMetricsProjectionEntity
                {
                    SprintId = 1,
                    ProductId = 1,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 1
                }
            ]);

        await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(0, _projectionService.ComputeProjectionsCallCount);
    }

    [TestMethod]
    [Description("Should recompute projections when cached projections are missing")]
    public async Task Handle_RecomputeFalse_WithEmptyCachedProjections_RecomputesAndReturnsData()
    {
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

        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(Array.Empty<SprintMetricsProjectionEntity>());
        _projectionService.ComputeProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(computedProjections);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.HasCount(1, result.Metrics);
        Assert.AreEqual(1, _projectionService.ComputeProjectionsCallCount);
        Assert.AreEqual(1, _projectionService.GetProjectionsCallCount);
    }

    [TestMethod]
    [Description("Should aggregate metrics across products when multiple products exist")]
    public async Task Handle_MultipleProducts_AggregatesMetricsCorrectly()
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
                PlannedStoryPoints = 13.5d,
                WorkedCount = 8,
                WorkedEffort = 25,
                BugsPlannedCount = 2,
                BugsWorkedCount = 1,
                CompletedPbiCount = 2,
                CompletedPbiEffort = 12,
                CompletedPbiStoryPoints = 8,
                SpilloverCount = 1,
                SpilloverEffort = 8,
                SpilloverStoryPoints = 5.5d,
                MissingStoryPointCount = 1,
                DerivedStoryPointCount = 1,
                DerivedStoryPoints = 3.5d,
                UnestimatedDeliveryCount = 1,
                LastComputedAt = DateTimeOffset.UtcNow,
                IncludedUpToRevisionId = 100,
                IsApproximate = true
            },
            new()
            {
                SprintId = sprint.Id,
                ProductId = product2.Id,
                Sprint = sprint,
                Product = product2,
                PlannedCount = 5,
                PlannedEffort = 15,
                PlannedStoryPoints = 8,
                WorkedCount = 4,
                WorkedEffort = 12,
                BugsPlannedCount = 3,
                BugsWorkedCount = 2,
                CompletedPbiCount = 1,
                CompletedPbiEffort = 7,
                CompletedPbiStoryPoints = 5,
                SpilloverCount = 2,
                SpilloverEffort = 13,
                SpilloverStoryPoints = 3,
                MissingStoryPointCount = 0,
                DerivedStoryPointCount = 0,
                DerivedStoryPoints = 0,
                UnestimatedDeliveryCount = 0,
                LastComputedAt = DateTimeOffset.UtcNow,
                IncludedUpToRevisionId = 100
            }
        };

        var query = new GetSprintTrendMetricsQuery(1, new[] { sprint.Id }, false);

        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(projections);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.HasCount(1, result.Metrics);

        var sprintMetrics = result.Metrics[0];
        Assert.AreEqual(sprint.Id, sprintMetrics.SprintId);
        Assert.AreEqual("Sprint 1", sprintMetrics.SprintName);

        Assert.AreEqual(15, sprintMetrics.TotalPlannedCount, "Total planned count should be 10 + 5");
        Assert.AreEqual(45, sprintMetrics.TotalPlannedEffort, "Total planned effort should be 30 + 15");
        Assert.AreEqual(21.5d, sprintMetrics.TotalPlannedStoryPoints, 0.001d, "Total planned story points should aggregate independently from effort.");
        Assert.AreEqual(12, sprintMetrics.TotalWorkedCount, "Total worked count should be 8 + 4");
        Assert.AreEqual(37, sprintMetrics.TotalWorkedEffort, "Total worked effort should be 25 + 12");
        Assert.AreEqual(5, sprintMetrics.TotalBugsPlannedCount, "Total bugs planned should be 2 + 3");
        Assert.AreEqual(3, sprintMetrics.TotalBugsWorkedCount, "Total bugs worked should be 1 + 2");
        Assert.AreEqual(3, sprintMetrics.TotalCompletedPbiCount, "Total completed PBI count should aggregate across products.");
        Assert.AreEqual(19, sprintMetrics.TotalCompletedPbiEffort, "Total completed effort should aggregate across products.");
        Assert.AreEqual(13d, sprintMetrics.TotalCompletedPbiStoryPoints, 0.001d, "Delivered story points should aggregate independently from effort.");
        Assert.AreEqual(3, sprintMetrics.TotalSpilloverCount, "Total spillover count should be 1 + 2");
        Assert.AreEqual(21, sprintMetrics.TotalSpilloverEffort, "Total spillover effort should be 8 + 13");
        Assert.AreEqual(8.5d, sprintMetrics.TotalSpilloverStoryPoints, 0.001d, "Spillover story points should aggregate independently from effort.");
        Assert.AreEqual(1, sprintMetrics.TotalMissingStoryPointCount, "Missing story-point diagnostics should be preserved.");
        Assert.AreEqual(1, sprintMetrics.TotalDerivedStoryPointCount, "Derived estimate diagnostics should be preserved.");
        Assert.AreEqual(3.5d, sprintMetrics.TotalDerivedStoryPoints, 0.001d, "Derived story-point totals should be preserved.");
        Assert.AreEqual(1, sprintMetrics.TotalUnestimatedDeliveryCount, "Unestimated delivery counts should be preserved.");
        Assert.IsTrue(sprintMetrics.IsApproximate, "Approximation should remain true when any product used derived estimates.");

        Assert.HasCount(2, sprintMetrics.ProductMetrics);
        Assert.AreEqual(1, sprintMetrics.ProductMetrics[0].SpilloverCount);
        Assert.AreEqual(8, sprintMetrics.ProductMetrics[0].SpilloverEffort);
        Assert.AreEqual(13.5d, sprintMetrics.ProductMetrics[0].PlannedStoryPoints, 0.001d);
        Assert.AreEqual(8d, sprintMetrics.ProductMetrics[0].CompletedPbiStoryPoints, 0.001d);
        Assert.AreEqual(1, sprintMetrics.ProductMetrics[0].DerivedStoryPointCount);
        Assert.AreEqual(1, sprintMetrics.ProductMetrics[0].UnestimatedDeliveryCount);
    }

    [TestMethod]
    [Description("Should return ordered metrics by sprint start date")]
    public async Task Handle_MultipleSprints_ReturnsOrderedByStartDate()
    {
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

        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(projections);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.HasCount(2, result.Metrics);
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

        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(
            [
                new SprintMetricsProjectionEntity
                {
                    SprintId = sprint.Id,
                    ProductId = product.Id,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 1
                }
            ]);
        _projectionService.ComputeFeatureProgressAsyncHandler = (_, mode, _, _, _, _) =>
        {
            Assert.AreEqual(FeatureProgressMode.StoryPoints, mode);
            return Task.FromResult<IReadOnlyList<FeatureProgressDto>>(
            [
                new FeatureProgressDto
                {
                    FeatureId = 200,
                    FeatureTitle = "Feature A",
                    ProductId = product.Id
                }
            ]);
        };
        _projectionService.ComputeEpicProgressAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<EpicProgressDto>>(
            [
                new EpicProgressDto
                {
                    EpicId = 300,
                    EpicTitle = "Epic A",
                    ProductId = product.Id,
                    SprintEffortDelta = 8,
                    SprintCompletedFeatureCount = 2
                }
            ]);

        var query = new GetSprintTrendMetricsQuery(1, new[] { sprint.Id }, false, false);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.HasCount(1, result.Metrics);
        Assert.IsNotNull(result.FeatureProgress);
        Assert.IsNotNull(result.EpicProgress);
        Assert.IsNotNull(result.ProductAnalytics);
        Assert.IsEmpty(result.FeatureProgress);
        Assert.IsEmpty(result.EpicProgress);
        Assert.HasCount(1, result.ProductAnalytics);
        Assert.AreEqual(8, result.Metrics[0].ProductMetrics[0].ScopeChangeEffort);
        Assert.AreEqual(2, result.Metrics[0].ProductMetrics[0].CompletedFeatureCount);
    }

    [TestMethod]
    [Description("Should aggregate product delivery summaries from multiple epic progress outputs")]
    public async Task Handle_IncludeDetailsFalse_AggregatesEpicSummariesPerProduct()
    {
        var team = new TeamEntity { Name = "Test Team", TeamAreaPath = "Project\\Team" };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        var olderSprint = new SprintEntity
        {
            Name = "Sprint 0",
            Path = "Project\\Sprint 0",
            TeamId = team.Id,
            StartUtc = DateTimeOffset.UtcNow.AddDays(-28),
            StartDateUtc = DateTime.UtcNow.AddDays(-28),
            EndUtc = DateTimeOffset.UtcNow.AddDays(-14),
            EndDateUtc = DateTime.UtcNow.AddDays(-14)
        };
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
        _context.Sprints.AddRange(olderSprint, sprint);

        var product = new ProductEntity
        {
            Name = "Product A",
            BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 100 } },
            ProductOwnerId = 1
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(
            [
                new SprintMetricsProjectionEntity
                {
                    SprintId = olderSprint.Id,
                    ProductId = product.Id,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 1
                },
                new SprintMetricsProjectionEntity
                {
                    SprintId = sprint.Id,
                    ProductId = product.Id,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 2
                }
            ]);
        _projectionService.ComputeEpicProgressAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<EpicProgressDto>>(
            [
                new EpicProgressDto
                {
                    EpicId = 300,
                    EpicTitle = "Epic A",
                    ProductId = product.Id,
                    ProgressPercent = 60,
                    TotalStoryPoints = 20,
                    DoneStoryPoints = 12,
                    FeatureCount = 2,
                    DoneFeatureCount = 1,
                    DonePbiCount = 3,
                    SprintCompletedEffort = 5,
                    SprintProgressionDelta = 25,
                    SprintEffortDelta = 8,
                    SprintCompletedPbiCount = 1,
                    SprintCompletedFeatureCount = 2
                },
                new EpicProgressDto
                {
                    EpicId = 301,
                    EpicTitle = "Epic B",
                    ProductId = product.Id,
                    ProgressPercent = 40,
                    TotalStoryPoints = 10,
                    DoneStoryPoints = 4,
                    FeatureCount = 1,
                    DoneFeatureCount = 0,
                    DonePbiCount = 1,
                    SprintCompletedEffort = 2,
                    SprintProgressionDelta = 20,
                    SprintEffortDelta = -3,
                    SprintCompletedPbiCount = 1,
                    SprintCompletedFeatureCount = 1
                }
            ]);

        var query = new GetSprintTrendMetricsQuery(1, new[] { olderSprint.Id, sprint.Id }, false, false);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        // Product-level progress summaries should only decorate the most recent sprint in the response.
        Assert.IsNull(result.Metrics[0].ProductMetrics[0].ScopeChangeEffort, "Only the most recent sprint should receive progress summaries.");
        Assert.IsNull(result.Metrics[0].ProductMetrics[0].CompletedFeatureCount, "Only the most recent sprint should receive progress summaries.");
        Assert.AreEqual(5, result.Metrics[1].ProductMetrics[0].ScopeChangeEffort);
        Assert.AreEqual(3, result.Metrics[1].ProductMetrics[0].CompletedFeatureCount);
    }

    [TestMethod]
    [Description("Should expose canonical product analytics with preserved null and negative delta semantics")]
    public async Task Handle_ExposesCanonicalProductAnalytics()
    {
        var team = new TeamEntity { Name = "Test Team", TeamAreaPath = "Project\\Team" };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        var previousSprint = new SprintEntity
        {
            Name = "Sprint 0",
            Path = "Project\\Sprint 0",
            TeamId = team.Id,
            StartUtc = DateTimeOffset.UtcNow.AddDays(-28),
            StartDateUtc = DateTime.UtcNow.AddDays(-28),
            EndUtc = DateTimeOffset.UtcNow.AddDays(-14),
            EndDateUtc = DateTime.UtcNow.AddDays(-14)
        };
        var currentSprint = new SprintEntity
        {
            Name = "Sprint 1",
            Path = "Project\\Sprint 1",
            TeamId = team.Id,
            StartUtc = DateTimeOffset.UtcNow.AddDays(-14),
            StartDateUtc = DateTime.UtcNow.AddDays(-14),
            EndUtc = DateTimeOffset.UtcNow,
            EndDateUtc = DateTime.UtcNow
        };
        _context.Sprints.AddRange(previousSprint, currentSprint);

        var product = new ProductEntity
        {
            Name = "Product A",
            BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 100 } },
            ProductOwnerId = 1
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(
            [
                new SprintMetricsProjectionEntity
                {
                    SprintId = previousSprint.Id,
                    ProductId = product.Id,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 1
                },
                new SprintMetricsProjectionEntity
                {
                    SprintId = currentSprint.Id,
                    ProductId = product.Id,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 2
                }
            ]);
        _projectionService.ComputeFeatureProgressAsyncHandler = (_, _, _, _, _, sprintId) =>
            Task.FromResult<IReadOnlyList<FeatureProgressDto>>(
                sprintId == currentSprint.Id
                    ?
                    [
                        new FeatureProgressDto
                        {
                            FeatureId = 200,
                            FeatureTitle = "Feature A",
                            ProductId = product.Id,
                            EpicId = 300,
                            EpicTitle = "Epic A",
                            ProgressPercent = 50,
                            CalculatedProgress = 50d,
                            EffectiveProgress = 50d,
                            ForecastConsumedEffort = 8d,
                            ForecastRemainingEffort = 8d,
                            Effort = 16d,
                            Weight = 8d,
                            IsExcluded = false
                        }
                    ]
                    :
                    [
                        new FeatureProgressDto
                        {
                            FeatureId = 201,
                            FeatureTitle = "Feature A",
                            ProductId = product.Id,
                            EpicId = 300,
                            EpicTitle = "Epic A",
                            ProgressPercent = 75,
                            CalculatedProgress = 75d,
                            EffectiveProgress = 75d,
                            ForecastConsumedEffort = 12d,
                            ForecastRemainingEffort = 4d,
                            Effort = 16d,
                            Weight = 8d,
                            IsExcluded = false
                        }
                    ]);
        _projectionService.ComputeEpicProgressAsyncHandler = (_, features, _) =>
            Task.FromResult<IReadOnlyList<EpicProgressDto>>(
                features[0].FeatureId == 200
                    ?
                    [
                        new EpicProgressDto
                        {
                            EpicId = 300,
                            EpicTitle = "Epic A",
                            ProductId = product.Id,
                            ProgressPercent = 50,
                            AggregatedProgress = 50d,
                            ForecastConsumedEffort = 8d,
                            ForecastRemainingEffort = 8d,
                            IncludedFeaturesCount = 1,
                            ExcludedFeaturesCount = 0,
                            TotalWeight = 8d
                        }
                    ]
                    :
                    [
                        new EpicProgressDto
                        {
                            EpicId = 300,
                            EpicTitle = "Epic A",
                            ProductId = product.Id,
                            ProgressPercent = 75,
                            AggregatedProgress = 75d,
                            ForecastConsumedEffort = 12d,
                            ForecastRemainingEffort = 4d,
                            IncludedFeaturesCount = 1,
                            ExcludedFeaturesCount = 0,
                            TotalWeight = 8d
                        }
                    ]);

        var result = await _handler.Handle(
            new GetSprintTrendMetricsQuery(1, new[] { previousSprint.Id, currentSprint.Id }, false, true),
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.ProductAnalytics);
        Assert.HasCount(1, result.ProductAnalytics);

        var analytics = result.ProductAnalytics[0];
        Assert.AreEqual(product.Id, analytics.ProductId);
        Assert.AreEqual("Product A", analytics.ProductName);
        Assert.AreEqual(50d, analytics.Progress.ProductProgress!.Value, 0.001d);
        Assert.AreEqual(8d, analytics.Progress.ProductForecastConsumed!.Value, 0.001d);
        Assert.AreEqual(8d, analytics.Progress.ProductForecastRemaining!.Value, 0.001d);
        Assert.AreEqual(-25d, analytics.Comparison.ProgressDelta!.Value, 0.001d);
        Assert.AreEqual(-4d, analytics.Comparison.ForecastConsumedDelta!.Value, 0.001d);
        Assert.AreEqual(4d, analytics.Comparison.ForecastRemainingDelta!.Value, 0.001d);
        Assert.AreEqual(100, analytics.PlanningQuality.PlanningQualityScore);
        Assert.IsEmpty(analytics.PlanningQuality.PlanningQualitySignals);
        Assert.AreEqual("IN-2", analytics.Insights[0].Code);
        Assert.AreEqual("Critical", analytics.Insights[0].Severity);
        Assert.AreEqual(-25d, analytics.Insights[0].Context.ProgressDelta!.Value, 0.001d);
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

        _projectionService.GetProjectionsAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(
            [
                new SprintMetricsProjectionEntity
                {
                    SprintId = sprint.Id,
                    ProductId = product.Id,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 1
                }
            ]);
        _projectionService.ComputeFeatureProgressAsyncHandler = (_, mode, _, _, _, _) =>
        {
            Assert.AreEqual(FeatureProgressMode.StoryPoints, mode);
            return Task.FromResult<IReadOnlyList<FeatureProgressDto>>(featureProgress);
        };
        _projectionService.ComputeEpicProgressAsyncHandler = (_, _, _) =>
            Task.FromResult<IReadOnlyList<EpicProgressDto>>(epicProgress);

        var query = new GetSprintTrendMetricsQuery(1, new[] { sprint.Id }, false, true);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.FeatureProgress);
        Assert.IsNotNull(result.EpicProgress);
        Assert.IsNotNull(result.ProductAnalytics);
        Assert.HasCount(1, result.FeatureProgress);
        Assert.HasCount(1, result.EpicProgress);
        Assert.HasCount(1, result.ProductAnalytics);
    }

    private sealed class TestSprintTrendProjectionService : SprintTrendProjectionService
    {
        private static readonly CanonicalStoryPointResolutionService StoryPointResolutionService = new();
        private static readonly HierarchyRollupService HierarchyRollupService = new(StoryPointResolutionService);
        private static readonly DeliveryProgressRollupService DeliveryProgressRollupService = new(StoryPointResolutionService, HierarchyRollupService);
        private static readonly SprintCommitmentService SprintCommitmentService = new();
        private static readonly SprintCompletionService SprintCompletionService = new();
        private static readonly SprintSpilloverService SprintSpilloverService = new();
        private static readonly SprintDeliveryProjectionService SprintDeliveryProjectionService = new(
            StoryPointResolutionService,
            HierarchyRollupService,
            DeliveryProgressRollupService,
            SprintCompletionService,
            SprintSpilloverService);

        public TestSprintTrendProjectionService()
            : base(
                new Mock<IServiceScopeFactory>().Object,
                Mock.Of<ILogger<SprintTrendProjectionService>>(),
                stateClassificationService: null,
                StoryPointResolutionService,
                HierarchyRollupService,
                DeliveryProgressRollupService,
                SprintCommitmentService,
                SprintCompletionService,
                SprintSpilloverService,
                SprintDeliveryProjectionService)
        {
        }

        public Func<int, IEnumerable<int>, CancellationToken, Task<IReadOnlyList<SprintMetricsProjectionEntity>>> GetProjectionsAsyncHandler { get; set; }
            = (_, _, _) => Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(Array.Empty<SprintMetricsProjectionEntity>());

        public Func<int, IEnumerable<int>, CancellationToken, Task<IReadOnlyList<SprintMetricsProjectionEntity>>> ComputeProjectionsAsyncHandler { get; set; }
            = (_, _, _) => Task.FromResult<IReadOnlyList<SprintMetricsProjectionEntity>>(Array.Empty<SprintMetricsProjectionEntity>());

        public Func<int, FeatureProgressMode, DateTime?, DateTime?, CancellationToken, int?, Task<IReadOnlyList<FeatureProgressDto>>> ComputeFeatureProgressAsyncHandler { get; set; }
            = (_, _, _, _, _, _) => Task.FromResult<IReadOnlyList<FeatureProgressDto>>(Array.Empty<FeatureProgressDto>());

        public Func<int, IReadOnlyList<FeatureProgressDto>, CancellationToken, Task<IReadOnlyList<EpicProgressDto>>> ComputeEpicProgressAsyncHandler { get; set; }
            = (_, _, _) => Task.FromResult<IReadOnlyList<EpicProgressDto>>(Array.Empty<EpicProgressDto>());

        public int GetProjectionsCallCount { get; private set; }

        public int ComputeProjectionsCallCount { get; private set; }

        public int? LastComputeProductOwnerId { get; private set; }

        public IReadOnlyList<int> LastComputeSprintIds { get; private set; } = Array.Empty<int>();

        public override Task<IReadOnlyList<SprintMetricsProjectionEntity>> GetProjectionsAsync(
            int productOwnerId,
            IEnumerable<int> sprintIds,
            CancellationToken cancellationToken = default)
        {
            GetProjectionsCallCount++;
            return GetProjectionsAsyncHandler(productOwnerId, sprintIds, cancellationToken);
        }

        public override Task<IReadOnlyList<SprintMetricsProjectionEntity>> ComputeProjectionsAsync(
            int productOwnerId,
            IEnumerable<int> sprintIds,
            CancellationToken cancellationToken = default)
        {
            ComputeProjectionsCallCount++;
            LastComputeProductOwnerId = productOwnerId;
            LastComputeSprintIds = sprintIds.ToList();
            return ComputeProjectionsAsyncHandler(productOwnerId, sprintIds, cancellationToken);
        }

        public override Task<IReadOnlyList<FeatureProgressDto>> ComputeFeatureProgressAsync(
            int productOwnerId,
            FeatureProgressMode progressMode,
            DateTime? sprintStartUtc = null,
            DateTime? sprintEndUtc = null,
            CancellationToken cancellationToken = default,
            int? sprintId = null)
        {
            return ComputeFeatureProgressAsyncHandler(productOwnerId, progressMode, sprintStartUtc, sprintEndUtc, cancellationToken, sprintId);
        }

        public override Task<IReadOnlyList<EpicProgressDto>> ComputeEpicProgressAsync(
            int productOwnerId,
            IReadOnlyList<FeatureProgressDto> featureProgress,
            CancellationToken cancellationToken = default)
        {
            return ComputeEpicProgressAsyncHandler(productOwnerId, featureProgress, cancellationToken);
        }
    }
}
