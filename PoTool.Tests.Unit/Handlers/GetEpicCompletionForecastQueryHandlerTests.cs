using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetEpicCompletionForecastQueryHandlerTests
{
    [TestMethod]
    public async Task Handle_WithNonExistentEpic_ReturnsNull()
    {
        await using var context = CreateContext();
        var handler = new GetEpicCompletionForecastQueryHandler(context, NullLogger<GetEpicCompletionForecastQueryHandler>.Instance);

        var result = await handler.Handle(new GetEpicCompletionForecastQuery(999), CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task Handle_ReadsPersistedProjectionVariantAndMapsForecastDto()
    {
        await using var context = CreateContext();
        context.WorkItems.Add(new WorkItemEntity
        {
            TfsId = 1,
            Type = "Epic",
            Title = "Epic 1",
            AreaPath = "Area\\Epic",
            IterationPath = "Sprint 3",
            State = "Active",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        });
        context.ForecastProjections.Add(new ForecastProjectionEntity
        {
            WorkItemId = 1,
            WorkItemType = "Epic",
            SprintsRemaining = 2,
            EstimatedCompletionDate = new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero),
            Confidence = nameof(ForecastConfidenceLevel.Medium),
            LastUpdated = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
            ProjectionVariantsJson = System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new StoredProjectionVariant(
                    5,
                    1,
                    "Epic",
                    21,
                    8,
                    13,
                    6,
                    3,
                    new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                    ForecastConfidenceLevel.Medium,
                    new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
                    [
                        new CompletionProjection(
                            "Sprint +1",
                            "Forecast/1",
                            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
                            new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero),
                            6,
                            7,
                            53.8)
                    ]),
                new StoredProjectionVariant(
                    6,
                    1,
                    "Epic",
                    21,
                    8,
                    13,
                    7,
                    2,
                    new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero),
                    ForecastConfidenceLevel.High,
                    new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
                    [
                        new CompletionProjection(
                            "Sprint +1",
                            "Forecast/1",
                            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
                            new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero),
                            7,
                            6,
                            53.8)
                    ])
            })
        });
        await context.SaveChangesAsync();

        var handler = new GetEpicCompletionForecastQueryHandler(context, NullLogger<GetEpicCompletionForecastQueryHandler>.Instance);

        var result = await handler.Handle(new GetEpicCompletionForecastQuery(1, 6), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.EpicId);
        Assert.AreEqual("Epic 1", result.Title);
        Assert.AreEqual("Epic", result.Type);
        Assert.AreEqual(21d, result.TotalStoryPoints, 0.001);
        Assert.AreEqual(8d, result.DoneStoryPoints, 0.001);
        Assert.AreEqual(13d, result.RemainingStoryPoints, 0.001);
        Assert.AreEqual(7d, result.EstimatedVelocity, 0.001);
        Assert.AreEqual(2, result.SprintsRemaining);
        Assert.AreEqual(ForecastConfidence.High, result.Confidence);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero), result.EstimatedCompletionDate);
        Assert.AreEqual("Area\\Epic", result.AreaPath);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero), result.AnalysisTimestamp);
        Assert.HasCount(1, result.ForecastByDate);
        Assert.AreEqual(7d, result.ForecastByDate[0].ExpectedCompletedStoryPoints, 0.001);
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"EpicForecastHandler_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }

    private sealed record StoredProjectionVariant(
        int MaxSprintsForVelocity,
        int WorkItemId,
        string WorkItemType,
        double TotalScopeStoryPoints,
        double CompletedScopeStoryPoints,
        double RemainingScopeStoryPoints,
        double EstimatedVelocity,
        int SprintsRemaining,
        DateTimeOffset? EstimatedCompletionDate,
        ForecastConfidenceLevel Confidence,
        DateTimeOffset LastUpdated,
        IReadOnlyList<CompletionProjection> ForecastByDate);
}
