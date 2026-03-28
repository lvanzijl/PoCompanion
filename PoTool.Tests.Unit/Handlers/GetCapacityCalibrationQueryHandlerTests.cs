using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetCapacityCalibrationQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private Mock<ILogger<GetCapacityCalibrationQueryHandler>> _mockLogger = null!;
    private Mock<IVelocityCalibrationService> _mockVelocityCalibrationService = null!;
    private GetCapacityCalibrationQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"TestDb_Calibration_{Guid.NewGuid()}")
            .Options;
        _context = new PoToolDbContext(options);
        _mockLogger = new Mock<ILogger<GetCapacityCalibrationQueryHandler>>();
        _mockVelocityCalibrationService = new Mock<IVelocityCalibrationService>(MockBehavior.Strict);
        _handler = new GetCapacityCalibrationQueryHandler(_context, _mockVelocityCalibrationService.Object, _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    public async Task Handle_NoProducts_ReturnsEmpty()
    {
        var result = await _handler.Handle(
            new GetCapacityCalibrationQuery(DeliveryFilterTestFactory.MultiSprint([1], [1, 2])),
            CancellationToken.None);

        Assert.IsEmpty(result.Sprints);
        Assert.AreEqual(0, result.MedianVelocity);
        _mockVelocityCalibrationService.Verify(
            service => service.Calibrate(It.IsAny<IReadOnlyList<VelocityCalibrationSample>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Handle_NoMatchingSprints_ReturnsEmpty()
    {
        SeedProduct(1, ownerId: 1);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetCapacityCalibrationQuery(DeliveryFilterTestFactory.MultiSprint([1], [99, 100])),
            CancellationToken.None);

        Assert.IsEmpty(result.Sprints);
        _mockVelocityCalibrationService.Verify(
            service => service.Calibrate(It.IsAny<IReadOnlyList<VelocityCalibrationSample>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Handle_WithProductFilter_RestrictsSamplesToRequestedProducts()
    {
        SeedProduct(1, ownerId: 1);
        SeedProduct(2, ownerId: 1);
        SeedSprint(1, "Sprint 1");
        SeedProjection(1, 1, plannedEffort: 20, completedPbiEffort: 24, plannedStoryPoints: 13, completedPbiStoryPoints: 8, derivedStoryPoints: 3);
        SeedProjection(1, 2, plannedEffort: 20, completedPbiEffort: 99, plannedStoryPoints: 55, completedPbiStoryPoints: 34, derivedStoryPoints: 5);
        await _context.SaveChangesAsync();

        IReadOnlyList<VelocityCalibrationSample>? capturedSamples = null;
        _mockVelocityCalibrationService
            .Setup(service => service.Calibrate(It.IsAny<IReadOnlyList<VelocityCalibrationSample>>()))
            .Callback<IReadOnlyList<VelocityCalibrationSample>>(samples => capturedSamples = samples)
            .Returns(new VelocityCalibration(
                entries:
                [
                    new VelocityCalibrationEntry("Sprint 1", committedStoryPoints: 10, deliveredStoryPoints: 8, deliveredEffort: 24, hoursPerStoryPoint: 3, predictabilityRatio: 0.8)
                ],
                medianVelocity: 8,
                p25Velocity: 8,
                p75Velocity: 8,
                medianPredictability: 0.8,
                outlierSprintNames: Array.Empty<string>()));

        var result = await _handler.Handle(
            new GetCapacityCalibrationQuery(DeliveryFilterTestFactory.MultiSprint([1], [1])),
            CancellationToken.None);

        Assert.IsNotNull(capturedSamples);
        Assert.HasCount(1, capturedSamples);
        Assert.AreEqual("Sprint 1", capturedSamples[0].SprintName);
        Assert.AreEqual(13d, capturedSamples[0].PlannedStoryPoints, 0.001);
        Assert.AreEqual(3d, capturedSamples[0].DerivedStoryPoints, 0.001);
        Assert.AreEqual(8d, capturedSamples[0].CompletedStoryPoints, 0.001);
        Assert.AreEqual(24, capturedSamples[0].CompletedEffort);
        Assert.AreEqual(8d, result.MedianVelocity, 0.001);
        Assert.HasCount(1, result.Sprints);
    }

    [TestMethod]
    public async Task Handle_WithEmptyEffectiveProductScope_ReturnsEmpty()
    {
        SeedProduct(1, ownerId: 2);
        SeedSprint(1, "Sprint 1");
        SeedProjection(1, 1, plannedEffort: 20, completedPbiEffort: 15, plannedStoryPoints: 8, completedPbiStoryPoints: 5);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetCapacityCalibrationQuery(DeliveryFilterTestFactory.MultiSprint([], [1])),
            CancellationToken.None);

        Assert.IsEmpty(result.Sprints);
        _mockVelocityCalibrationService.Verify(
            service => service.Calibrate(It.IsAny<IReadOnlyList<VelocityCalibrationSample>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Handle_MapsVelocityCalibrationResultIntoDto()
    {
        SeedProduct(1, ownerId: 1);
        SeedSprint(1, "Sprint 1");
        await _context.SaveChangesAsync();

        _mockVelocityCalibrationService
            .Setup(service => service.Calibrate(It.IsAny<IReadOnlyList<VelocityCalibrationSample>>()))
            .Returns(new VelocityCalibration(
                entries:
                [
                    new VelocityCalibrationEntry("Sprint 1", committedStoryPoints: 10, deliveredStoryPoints: 6, deliveredEffort: 24, hoursPerStoryPoint: 4, predictabilityRatio: 0.6)
                ],
                medianVelocity: 6,
                p25Velocity: 5,
                p75Velocity: 7,
                medianPredictability: 0.6,
                outlierSprintNames: ["Sprint 1"]));

        var result = await _handler.Handle(
            new GetCapacityCalibrationQuery(DeliveryFilterTestFactory.MultiSprint([1], [1])),
            CancellationToken.None);

        Assert.HasCount(1, result.Sprints);
        Assert.AreEqual("Sprint 1", result.Sprints[0].SprintName);
        Assert.AreEqual(10d, result.Sprints[0].CommittedStoryPoints, 0.001);
        Assert.AreEqual(6d, result.Sprints[0].DeliveredStoryPoints, 0.001);
        Assert.AreEqual(24, result.Sprints[0].DeliveredEffort);
        Assert.AreEqual(4d, result.Sprints[0].HoursPerSP, 0.001);
        Assert.AreEqual(0.6d, result.Sprints[0].PredictabilityRatio, 0.001);
        Assert.AreEqual(6d, result.MedianVelocity, 0.001);
        Assert.AreEqual(5d, result.P25Velocity, 0.001);
        Assert.AreEqual(7d, result.P75Velocity, 0.001);
        Assert.AreEqual(0.6d, result.MedianPredictability, 0.001);
        CollectionAssert.AreEqual(new[] { "Sprint 1" }, result.OutlierSprintNames.ToArray());
    }

    private ProductEntity SeedProduct(int id, int ownerId)
    {
        var product = new ProductEntity { Id = id, Name = $"Product {id}", ProductOwnerId = ownerId };
        _context.Products.Add(product);
        return product;
    }

    private SprintEntity SeedSprint(int id, string name)
    {
        var sprint = new SprintEntity { Id = id, Name = name, TeamId = 1 };
        _context.Sprints.Add(sprint);
        return sprint;
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
}
