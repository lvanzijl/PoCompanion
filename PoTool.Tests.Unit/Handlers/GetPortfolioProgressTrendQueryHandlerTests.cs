using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetPortfolioProgressTrendQueryHandlerTests
{
    [TestMethod]
    public async Task Handle_AggregatesCanonicalProjectionIntoCanonicalFieldsAndCompatibilityAliases()
    {
        await using var context = CreateContext();

        var owner = new ProfileEntity { Name = "PO 1" };
        context.Profiles.Add(owner);

        var team = new TeamEntity { Name = "Team 1", TeamAreaPath = "\\Project\\Team 1" };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var sprint = CreateSprint(team.Id, 101, "Sprint 1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var productA = new ProductEntity { ProductOwnerId = owner.Id, Name = "Product A" };
        var productB = new ProductEntity { ProductOwnerId = owner.Id, Name = "Product B" };

        context.Sprints.Add(sprint);
        context.Products.AddRange(productA, productB);
        await context.SaveChangesAsync();

        context.PortfolioFlowProjections.AddRange(
            new PortfolioFlowProjectionEntity
            {
                SprintId = sprint.Id,
                ProductId = productA.Id,
                StockStoryPoints = 10,
                RemainingScopeStoryPoints = 4,
                InflowStoryPoints = 2,
                ThroughputStoryPoints = 6,
                CompletionPercent = 60,
                ProjectionTimestamp = DateTimeOffset.UtcNow
            },
            new PortfolioFlowProjectionEntity
            {
                SprintId = sprint.Id,
                ProductId = productB.Id,
                StockStoryPoints = 20,
                RemainingScopeStoryPoints = 6,
                InflowStoryPoints = 3,
                ThroughputStoryPoints = 6,
                CompletionPercent = 70,
                ProjectionTimestamp = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        var handler = new GetPortfolioProgressTrendQueryHandler(
            context,
            NullLogger<GetPortfolioProgressTrendQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetPortfolioProgressTrendQuery(owner.Id, new[] { sprint.Id }),
            CancellationToken.None);

        Assert.HasCount(1, result.Sprints);
        var sprintResult = result.Sprints[0];

        Assert.IsTrue(sprintResult.HasData);
        Assert.AreEqual(30d, sprintResult.StockStoryPoints!.Value, 0.001d);
        Assert.AreEqual(10d, sprintResult.RemainingScopeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(5d, sprintResult.InflowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(12d, sprintResult.ThroughputStoryPoints!.Value, 0.001d);
        Assert.AreEqual(7d, sprintResult.NetFlowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(66.667d, sprintResult.CompletionPercent!.Value, 0.001d);

        Assert.AreEqual(sprintResult.StockStoryPoints, sprintResult.TotalScopeEffort);
        Assert.AreEqual(sprintResult.RemainingScopeStoryPoints, sprintResult.RemainingEffort);
        Assert.AreEqual(sprintResult.InflowStoryPoints, sprintResult.AddedEffort);
        Assert.AreEqual(sprintResult.ThroughputStoryPoints, sprintResult.ThroughputEffort);
        Assert.AreEqual(sprintResult.NetFlowStoryPoints, sprintResult.NetFlow);
        Assert.AreEqual(sprintResult.CompletionPercent, sprintResult.PercentDone);

        Assert.AreEqual(PortfolioTrajectory.Contracting, result.Summary.Trajectory);
        Assert.AreEqual(7d, result.Summary.CumulativeNetFlow!.Value, 0.001d);
        Assert.AreEqual(0d, result.Summary.TotalScopeChangeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(0d, result.Summary.RemainingScopeChangeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(result.Summary.TotalScopeChangeStoryPoints, result.Summary.TotalScopeChangePts);
        Assert.AreEqual(result.Summary.RemainingScopeChangeStoryPoints, result.Summary.RemainingEffortChangePts);
    }

    [TestMethod]
    public async Task Handle_UsesProductFilterAndLeavesMissingSprintRowsEmpty()
    {
        await using var context = CreateContext();

        var owner = new ProfileEntity { Name = "PO 1" };
        context.Profiles.Add(owner);

        var team = new TeamEntity { Name = "Team 1", TeamAreaPath = "\\Project\\Team 1" };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var sprint1 = CreateSprint(team.Id, 101, "Sprint 1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var sprint2 = CreateSprint(team.Id, 102, "Sprint 2", new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        var productA = new ProductEntity { ProductOwnerId = owner.Id, Name = "Product A" };
        var productB = new ProductEntity { ProductOwnerId = owner.Id, Name = "Product B" };

        context.Sprints.AddRange(sprint1, sprint2);
        context.Products.AddRange(productA, productB);
        await context.SaveChangesAsync();

        context.PortfolioFlowProjections.AddRange(
            new PortfolioFlowProjectionEntity
            {
                SprintId = sprint1.Id,
                ProductId = productA.Id,
                StockStoryPoints = 10,
                RemainingScopeStoryPoints = 4,
                InflowStoryPoints = 2,
                ThroughputStoryPoints = 6,
                CompletionPercent = 60,
                ProjectionTimestamp = DateTimeOffset.UtcNow
            },
            new PortfolioFlowProjectionEntity
            {
                SprintId = sprint2.Id,
                ProductId = productB.Id,
                StockStoryPoints = 99,
                RemainingScopeStoryPoints = 50,
                InflowStoryPoints = 40,
                ThroughputStoryPoints = 1,
                CompletionPercent = 49.5,
                ProjectionTimestamp = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        var handler = new GetPortfolioProgressTrendQueryHandler(
            context,
            NullLogger<GetPortfolioProgressTrendQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetPortfolioProgressTrendQuery(owner.Id, new[] { sprint1.Id, sprint2.Id }, new[] { productA.Id }),
            CancellationToken.None);

        Assert.HasCount(2, result.Sprints);

        Assert.IsTrue(result.Sprints[0].HasData);
        Assert.AreEqual(10d, result.Sprints[0].StockStoryPoints!.Value, 0.001d);
        Assert.AreEqual(4d, result.Sprints[0].RemainingScopeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(2d, result.Sprints[0].InflowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(6d, result.Sprints[0].ThroughputStoryPoints!.Value, 0.001d);

        Assert.IsFalse(result.Sprints[1].HasData);
        Assert.IsNull(result.Sprints[1].StockStoryPoints);
        Assert.IsNull(result.Sprints[1].RemainingScopeStoryPoints);
        Assert.IsNull(result.Sprints[1].InflowStoryPoints);
        Assert.IsNull(result.Sprints[1].ThroughputStoryPoints);
        Assert.IsNull(result.Sprints[1].CompletionPercent);
        Assert.IsNull(result.Sprints[1].NetFlowStoryPoints);
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PortfolioProgressTrend_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }

    private static SprintEntity CreateSprint(int teamId, int suffix, string name, DateTime startUtc)
    {
        var endUtc = startUtc.AddDays(13);

        return new SprintEntity
        {
            TeamId = teamId,
            Name = name,
            Path = $"\\Project\\{name}_{suffix}",
            StartUtc = new DateTimeOffset(startUtc, TimeSpan.Zero),
            StartDateUtc = startUtc,
            EndUtc = new DateTimeOffset(endUtc, TimeSpan.Zero),
            EndDateUtc = endUtc,
            LastSyncedUtc = DateTimeOffset.UtcNow,
            LastSyncedDateUtc = DateTime.UtcNow
        };
    }
}
