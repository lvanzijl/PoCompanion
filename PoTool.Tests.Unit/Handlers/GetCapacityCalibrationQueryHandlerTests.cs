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
/// Validates canonical story-point velocity, predictability ratios, and diagnostic effort metrics.
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

    private void SeedProjection(
        int sprintId,
        int productId,
        int plannedEffort,
        int completedPbiEffort,
        double plannedStoryPoints = 0,
        double completedPbiStoryPoints = 0,
        double derivedStoryPoints = 0)
    {
        _context.SprintMetricsProjections.Add(new SprintMetricsProjectionEntity
        {
            SprintId = sprintId,
            ProductId = productId,
            PlannedEffort = plannedEffort,
            PlannedStoryPoints = plannedStoryPoints,
            CompletedPbiEffort = completedPbiEffort,
            CompletedPbiStoryPoints = completedPbiStoryPoints,
            PlannedCount = 0,
            WorkedCount = 0,
            WorkedEffort = 0,
            BugsPlannedCount = 0,
            BugsWorkedCount = 0,
            DerivedStoryPoints = derivedStoryPoints,
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
    [Description("Computes median velocity from delivered story points instead of effort-hours")]
    public async Task Handle_ThreeSprints_ComputesMedianVelocity()
    {
        SeedProduct(1, ownerId: 1);
        SeedSprint(1, "Sprint 1");
        SeedSprint(2, "Sprint 2");
        SeedSprint(3, "Sprint 3");
        // Delivered story-point velocities: 3, 5, 8 → median = 5
        SeedProjection(sprintId: 1, productId: 1, plannedEffort: 20, completedPbiEffort: 100, plannedStoryPoints: 8, completedPbiStoryPoints: 3);
        SeedProjection(sprintId: 2, productId: 1, plannedEffort: 20, completedPbiEffort: 10, plannedStoryPoints: 8, completedPbiStoryPoints: 5);
        SeedProjection(sprintId: 3, productId: 1, plannedEffort: 20, completedPbiEffort: 1, plannedStoryPoints: 8, completedPbiStoryPoints: 8);
        await _context.SaveChangesAsync();

        var query = new GetCapacityCalibrationQuery(
            ProductOwnerId: 1, SprintIds: new[] { 1, 2, 3 });

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(5.0, result.MedianVelocity, "Median velocity should come from delivered story points, not effort.");
        Assert.HasCount(3, result.Sprints);
    }

    [TestMethod]
    [Description("P25/P75 band separates conservative and optimistic velocities")]
    public async Task Handle_FiveSprints_ComputesP25P75Band()
    {
        SeedProduct(1, ownerId: 1);
        for (int i = 1; i <= 5; i++) SeedSprint(i, $"Sprint {i}");
        // Story-point velocities sorted: 10, 20, 30, 40, 50 → P25=20, P50=30, P75=40
        SeedProjection(1, 1, 30, 100, plannedStoryPoints: 60, completedPbiStoryPoints: 10);
        SeedProjection(2, 1, 30, 90, plannedStoryPoints: 60, completedPbiStoryPoints: 20);
        SeedProjection(3, 1, 30, 80, plannedStoryPoints: 60, completedPbiStoryPoints: 30);
        SeedProjection(4, 1, 30, 70, plannedStoryPoints: 60, completedPbiStoryPoints: 40);
        SeedProjection(5, 1, 60, 60, plannedStoryPoints: 60, completedPbiStoryPoints: 50);
        await _context.SaveChangesAsync();

        var query = new GetCapacityCalibrationQuery(
            ProductOwnerId: 1, SprintIds: new[] { 1, 2, 3, 4, 5 });

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(30.0, result.MedianVelocity, "Median of [10,20,30,40,50] = 30");
        Assert.AreEqual(20.0, result.P25Velocity, "P25 of [10,20,30,40,50] = 20");
        Assert.AreEqual(40.0, result.P75Velocity, "P75 of [10,20,30,40,50] = 40");
    }

    [TestMethod]
    [Description("Predictability ratio uses delivered and committed story points while excluding derived commitment")]
    public async Task Handle_WithPredictabilityData_ComputesCorrectRatio()
    {
        SeedProduct(1, ownerId: 1);
        SeedSprint(1, "Sprint 1");
        SeedSprint(2, "Sprint 2");
        SeedSprint(3, "Sprint 3");
        // Sprint 1: committed=(10-4)=6, delivered=6 → ratio=1.0
        // Sprint 2: committed=8, delivered=4        → ratio=0.5
        // Sprint 3: committed=0, delivered=5        → excluded from predictability aggregate
        SeedProjection(1, 1, 20, 20, plannedStoryPoints: 10, completedPbiStoryPoints: 6, derivedStoryPoints: 4);
        SeedProjection(2, 1, 20, 10, plannedStoryPoints: 8, completedPbiStoryPoints: 4);
        SeedProjection(3, 1, 0,  5, plannedStoryPoints: 0, completedPbiStoryPoints: 5);
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
        Assert.AreEqual(6.0, result.Sprints.First(s => s.SprintName == "Sprint 1").CommittedStoryPoints, 0.001,
            "Derived story points must be excluded from committed scope.");
    }

    [TestMethod]
    [Description("Outlier detection flags sprints below P10 or above P90")]
    public async Task Handle_WithOutliers_FlagsOutlierSprints()
    {
        SeedProduct(1, ownerId: 1);
        // 10 sprints with one very low and one very high to trigger P10/P90 outliers
        for (int i = 1; i <= 10; i++) SeedSprint(i, $"Sprint {i}");
        // Story-point velocities: 1, 10, 10, 10, 10, 10, 10, 10, 10, 100
        SeedProjection(1, 1, 10, 50, plannedStoryPoints: 10, completedPbiStoryPoints: 1);   // outlier low
        for (int i = 2; i <= 9; i++) SeedProjection(i, 1, 10, 10, plannedStoryPoints: 10, completedPbiStoryPoints: 10);
        SeedProjection(10, 1, 10, 5, plannedStoryPoints: 10, completedPbiStoryPoints: 100); // outlier high
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
        // Product 1: velocity=10; Product 2: velocity=90 (in story points)
        SeedProjection(1, 1, 20, 100, plannedStoryPoints: 20, completedPbiStoryPoints: 10);
        SeedProjection(1, 2, 20, 1, plannedStoryPoints: 20, completedPbiStoryPoints: 90);
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
        SeedProjection(1, 1, 20, 15, plannedStoryPoints: 8, completedPbiStoryPoints: 5);
        await _context.SaveChangesAsync();

        // Owner 1 tries to access product belonging to owner 2
        var query = new GetCapacityCalibrationQuery(
            ProductOwnerId: 1,
            SprintIds: new[] { 1 },
            ProductIds: new[] { 1 });

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsEmpty(result.Sprints, "Cross-owner product access must return empty");
    }

    [TestMethod]
    [Description("Excludes effort-only delivery from velocity and exposes hours per story point diagnostically")]
    public async Task Handle_WithEffortOnlyDelivery_KeepsVelocityAtZeroAndComputesHoursPerSpSafely()
    {
        SeedProduct(1, ownerId: 1);
        SeedSprint(1, "Sprint 1");
        SeedSprint(2, "Sprint 2");
        SeedProjection(1, 1, plannedEffort: 30, completedPbiEffort: 24, plannedStoryPoints: 8, completedPbiStoryPoints: 6);
        SeedProjection(2, 1, plannedEffort: 30, completedPbiEffort: 12, plannedStoryPoints: 5, completedPbiStoryPoints: 0);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetCapacityCalibrationQuery(ProductOwnerId: 1, SprintIds: new[] { 1, 2 }),
            CancellationToken.None);

        var deliveredSprint = result.Sprints.First(s => s.SprintName == "Sprint 1");
        var effortOnlySprint = result.Sprints.First(s => s.SprintName == "Sprint 2");

        Assert.AreEqual(6.0, deliveredSprint.DeliveredStoryPoints, 0.001);
        Assert.AreEqual(24, deliveredSprint.DeliveredEffort);
        Assert.AreEqual(4.0, deliveredSprint.HoursPerSP, 0.001, "HoursPerSP should use delivered effort divided by delivered story points.");

        Assert.AreEqual(0.0, effortOnlySprint.DeliveredStoryPoints, 0.001,
            "Effort without delivered story points must not be treated as velocity.");
        Assert.AreEqual(12, effortOnlySprint.DeliveredEffort);
        Assert.AreEqual(0.0, effortOnlySprint.HoursPerSP, 0.001,
            "HoursPerSP must avoid division by zero when no delivered story points exist.");
    }
}
