using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Metrics.Queries;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Tests for GetCapacityCalibrationQueryHandler.
/// Validates percentile computation, predictability ratios, and outlier detection.
/// </summary>
[TestClass]
public class GetCapacityCalibrationQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private Mock<ILogger<GetCapacityCalibrationQueryHandler>> _mockLogger = null!;
    private GetCapacityCalibrationQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"TestDb_Calibration_{Guid.NewGuid()}")
            .Options;
        _context = new PoToolDbContext(options);
        _mockLogger = new Mock<ILogger<GetCapacityCalibrationQueryHandler>>();
        _handler = new GetCapacityCalibrationQueryHandler(_context, _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ─── helpers ────────────────────────────────────────────────────────────────

    private ProductEntity SeedProduct(int id, int ownerId = 1)
    {
        var p = new ProductEntity { Id = id, Name = $"Product {id}", ProductOwnerId = ownerId };
        _context.Products.Add(p);
        return p;
    }

    private SprintEntity SeedSprint(int id, string name)
    {
        var s = new SprintEntity { Id = id, Name = name, TeamId = 1 };
        _context.Sprints.Add(s);
        return s;
    }

    private void SeedProjection(int sprintId, int productId, int plannedEffort, int completedPbiEffort)
    {
        _context.SprintMetricsProjections.Add(new SprintMetricsProjectionEntity
        {
            SprintId = sprintId,
            ProductId = productId,
            PlannedEffort = plannedEffort,
            CompletedPbiEffort = completedPbiEffort,
            PlannedCount = 0,
            WorkedCount = 0,
            WorkedEffort = 0,
            BugsPlannedCount = 0,
            BugsWorkedCount = 0,
            IncludedUpToRevisionId = 0,
            LastComputedAt = DateTimeOffset.UtcNow
        });
    }

    // ─── tests ──────────────────────────────────────────────────────────────────

    [TestMethod]
    [Description("Returns empty result when no products exist for the owner")]
    public async Task Handle_NoProducts_ReturnsEmpty()
    {
        var query = new GetCapacityCalibrationQuery(ProductOwnerId: 99, SprintIds: new[] { 1, 2 });

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsEmpty(result.Sprints);
        Assert.AreEqual(0, result.MedianVelocity);
    }

    [TestMethod]
    [Description("Returns empty result when sprint IDs are not found in the database")]
    public async Task Handle_NoMatchingSprints_ReturnsEmpty()
    {
        SeedProduct(1, ownerId: 1);
        await _context.SaveChangesAsync();

        var query = new GetCapacityCalibrationQuery(
            ProductOwnerId: 1, SprintIds: new[] { 99, 100 });

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsEmpty(result.Sprints);
    }

    [TestMethod]
    [Description("Computes median velocity correctly from three sprint velocities")]
    public async Task Handle_ThreeSprints_ComputesMedianVelocity()
    {
        SeedProduct(1, ownerId: 1);
        SeedSprint(1, "Sprint 1");
        SeedSprint(2, "Sprint 2");
        SeedSprint(3, "Sprint 3");
        // Velocities: 10, 20, 30 → median = 20
        SeedProjection(sprintId: 1, productId: 1, plannedEffort: 20, completedPbiEffort: 10);
        SeedProjection(sprintId: 2, productId: 1, plannedEffort: 20, completedPbiEffort: 20);
        SeedProjection(sprintId: 3, productId: 1, plannedEffort: 20, completedPbiEffort: 30);
        await _context.SaveChangesAsync();

        var query = new GetCapacityCalibrationQuery(
            ProductOwnerId: 1, SprintIds: new[] { 1, 2, 3 });

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(20.0, result.MedianVelocity, "Median of [10,20,30] should be 20");
        Assert.HasCount(3, result.Sprints);
    }

    [TestMethod]
    [Description("P25/P75 band separates conservative and optimistic velocities")]
    public async Task Handle_FiveSprints_ComputesP25P75Band()
    {
        SeedProduct(1, ownerId: 1);
        for (int i = 1; i <= 5; i++) SeedSprint(i, $"Sprint {i}");
        // Velocities sorted: 10, 20, 30, 40, 50 → P25=20, P50=30, P75=40
        SeedProjection(1, 1, 30, 10);
        SeedProjection(2, 1, 30, 20);
        SeedProjection(3, 1, 30, 30);
        SeedProjection(4, 1, 30, 40);
        SeedProjection(5, 1, 30, 50);
        await _context.SaveChangesAsync();

        var query = new GetCapacityCalibrationQuery(
            ProductOwnerId: 1, SprintIds: new[] { 1, 2, 3, 4, 5 });

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(30.0, result.MedianVelocity, "Median of [10,20,30,40,50] = 30");
        Assert.AreEqual(20.0, result.P25Velocity, "P25 of [10,20,30,40,50] = 20");
        Assert.AreEqual(40.0, result.P75Velocity, "P75 of [10,20,30,40,50] = 40");
    }

    [TestMethod]
    [Description("Predictability ratio is Done/Committed and excludes uncommitted sprints")]
    public async Task Handle_WithPredictabilityData_ComputesCorrectRatio()
    {
        SeedProduct(1, ownerId: 1);
        SeedSprint(1, "Sprint 1");
        SeedSprint(2, "Sprint 2");
        SeedSprint(3, "Sprint 3");
        // Sprint 1: committed=20, done=20 → ratio=1.0
        // Sprint 2: committed=20, done=10 → ratio=0.5
        // Sprint 3: committed=0,  done=5  → excluded from predictability aggregate
        SeedProjection(1, 1, 20, 20);
        SeedProjection(2, 1, 20, 10);
        SeedProjection(3, 1, 0,  5);
        await _context.SaveChangesAsync();

        var query = new GetCapacityCalibrationQuery(
            ProductOwnerId: 1, SprintIds: new[] { 1, 2, 3 });

        var result = await _handler.Handle(query, CancellationToken.None);

        // Median predictability from [0.5, 1.0] = 0.75
        Assert.AreEqual(0.75, result.MedianPredictability, delta: 0.001,
            "Median predictability of [0.5, 1.0] should be 0.75");
        Assert.HasCount(3, result.Sprints);
        // Sprint 3 has no commitment, so its PredictabilityRatio is 0 in the entry
        Assert.AreEqual(0.0, result.Sprints.First(s => s.SprintName == "Sprint 3").PredictabilityRatio);
    }

    [TestMethod]
    [Description("Outlier detection flags sprints below P10 or above P90")]
    public async Task Handle_WithOutliers_FlagsOutlierSprints()
    {
        SeedProduct(1, ownerId: 1);
        // 10 sprints with one very low and one very high to trigger P10/P90 outliers
        for (int i = 1; i <= 10; i++) SeedSprint(i, $"Sprint {i}");
        // Velocities: 1, 10, 10, 10, 10, 10, 10, 10, 10, 100
        SeedProjection(1, 1, 10, 1);   // outlier low
        for (int i = 2; i <= 9; i++) SeedProjection(i, 1, 10, 10);
        SeedProjection(10, 1, 10, 100); // outlier high
        await _context.SaveChangesAsync();

        var sprintIds = Enumerable.Range(1, 10).ToArray();
        var query = new GetCapacityCalibrationQuery(
            ProductOwnerId: 1, SprintIds: sprintIds);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.OutlierSprintNames.Contains("Sprint 1"), "Sprint 1 (velocity=1) should be a low outlier");
        Assert.IsTrue(result.OutlierSprintNames.Contains("Sprint 10"), "Sprint 10 (velocity=100) should be a high outlier");
    }

    [TestMethod]
    [Description("Product filter restricts data to specified product IDs only")]
    public async Task Handle_WithProductFilter_RestrictsToRequestedProduct()
    {
        SeedProduct(1, ownerId: 1);
        SeedProduct(2, ownerId: 1);
        SeedSprint(1, "Sprint 1");
        // Product 1: velocity=10; Product 2: velocity=90
        SeedProjection(1, 1, 20, 10);
        SeedProjection(1, 2, 20, 90);
        await _context.SaveChangesAsync();

        var query = new GetCapacityCalibrationQuery(
            ProductOwnerId: 1,
            SprintIds: new[] { 1 },
            ProductIds: new[] { 1 });   // only product 1

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(10.0, result.MedianVelocity, "Should only include velocity from product 1");
    }

    [TestMethod]
    [Description("Product filter cannot cross ownership boundaries (security test)")]
    public async Task Handle_WithCrossOwnerProductFilter_ReturnsEmpty()
    {
        SeedProduct(1, ownerId: 2); // belongs to owner 2
        SeedSprint(1, "Sprint 1");
        SeedProjection(1, 1, 20, 15);
        await _context.SaveChangesAsync();

        // Owner 1 tries to access product belonging to owner 2
        var query = new GetCapacityCalibrationQuery(
            ProductOwnerId: 1,
            SprintIds: new[] { 1 },
            ProductIds: new[] { 1 });

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsEmpty(result.Sprints, "Cross-owner product access must return empty");
    }
}
