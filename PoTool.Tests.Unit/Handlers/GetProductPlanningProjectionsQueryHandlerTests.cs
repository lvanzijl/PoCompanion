using Microsoft.EntityFrameworkCore;
using PoTool.Api.Handlers.Settings.Products;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Settings.Queries;
using PoTool.Shared.Metrics;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetProductPlanningProjectionsQueryHandlerTests
{
    [TestMethod]
    public async Task Handle_WhenProductDoesNotExist_ReturnsNull()
    {
        await using var context = CreateContext();
        var handler = new GetProductPlanningProjectionsQueryHandler(context);

        var result = await handler.Handle(new GetProductPlanningProjectionsQuery(999), CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task Handle_ReturnsRoadmapEpicsInOrderWithPersistedForecasts()
    {
        await using var context = CreateContext();
        SeedData(context);
        var handler = new GetProductPlanningProjectionsQueryHandler(context);

        var result = await handler.Handle(new GetProductPlanningProjectionsQuery(1), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.HasCount(2, result);
        Assert.AreEqual(11, result[0].EpicId);
        Assert.AreEqual("Roadmap Epic B", result[0].EpicTitle);
        Assert.AreEqual(1, result[0].RoadmapOrder);
        Assert.IsFalse(result[0].HasForecast);
        Assert.IsNull(result[0].EstimatedCompletionDate);

        Assert.AreEqual(10, result[1].EpicId);
        Assert.AreEqual(2, result[1].RoadmapOrder);
        Assert.IsTrue(result[1].HasForecast);
        Assert.AreEqual(3, result[1].SprintsRemaining);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), result[1].EstimatedCompletionDate);
        Assert.AreEqual(ForecastConfidence.Medium, result[1].Confidence);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero), result[1].LastUpdated);
    }

    [TestMethod]
    public async Task Handle_WhenForecastHasNoCompletionDate_UsesLastUpdatedForCompletedForecasts()
    {
        await using var context = CreateContext();
        SeedData(context);
        context.ForecastProjections.Add(new ForecastProjectionEntity
        {
            WorkItemId = 11,
            WorkItemType = "Epic",
            SprintsRemaining = 0,
            EstimatedCompletionDate = null,
            Confidence = nameof(ForecastConfidence.Low),
            LastUpdated = new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero),
            ProjectionVariantsJson = "[]"
        });
        await context.SaveChangesAsync();

        var handler = new GetProductPlanningProjectionsQueryHandler(context);

        var result = await handler.Handle(new GetProductPlanningProjectionsQuery(1), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero), result[0].EstimatedCompletionDate);
        Assert.IsTrue(result[0].HasForecast);
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PlanningProjections_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }

    private static void SeedData(PoToolDbContext context)
    {
        PersistenceTestGraph.EnsureProject(context, "proj", "proj", "proj");
        context.Products.Add(new ProductEntity
        {
            Id = 1,
            Name = "Product A",
            ProjectId = "proj"
        });

        context.WorkItems.AddRange(
            CreateEpic(10, "Roadmap Epic A", 20d, "roadmap;alpha"),
            CreateEpic(11, "Roadmap Epic B", 10d, "roadmap"),
            CreateEpic(12, "Non-roadmap Epic", 5d, "alpha"));

        context.ResolvedWorkItems.AddRange(
            CreateResolved(10),
            CreateResolved(11),
            CreateResolved(12));

        context.ForecastProjections.Add(new ForecastProjectionEntity
        {
            WorkItemId = 10,
            WorkItemType = "Epic",
            SprintsRemaining = 3,
            EstimatedCompletionDate = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            Confidence = nameof(ForecastConfidence.Medium),
            LastUpdated = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
            ProjectionVariantsJson = "[]"
        });

        context.SaveChanges();
    }

    private static WorkItemEntity CreateEpic(int tfsId, string title, double backlogPriority, string? tags)
    {
        return new WorkItemEntity
        {
            TfsId = tfsId,
            Type = "Epic",
            Title = title,
            AreaPath = "Area",
            IterationPath = "Iteration",
            State = "Active",
            RetrievedAt = DateTimeOffset.UtcNow,
            Tags = tags,
            BacklogPriority = backlogPriority,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        };
    }

    private static ResolvedWorkItemEntity CreateResolved(int workItemId)
    {
        return new ResolvedWorkItemEntity
        {
            WorkItemId = workItemId,
            WorkItemType = "Epic",
            ResolvedProductId = 1,
            ResolvedEpicId = workItemId,
            ResolutionStatus = ResolutionStatus.Resolved,
            LastResolvedAt = DateTimeOffset.UtcNow
        };
    }
}
