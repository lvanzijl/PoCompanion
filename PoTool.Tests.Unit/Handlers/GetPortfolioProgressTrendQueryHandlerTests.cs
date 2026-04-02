using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Domain.Portfolio;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetPortfolioProgressTrendQueryHandlerTests
{
    [TestMethod]
    public async Task Handle_DelegatesPortfolioRollupsToCdcServiceAndMapsResult()
    {
        await using var context = CreateContext();

        var owner = new ProfileEntity { Name = "PO 1" };
        context.Profiles.Add(owner);

        var team = new TeamEntity { Name = "Team 1", TeamAreaPath = "\\Project\\Team 1" };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var sprint = CreateSprint(team.Id, 101, "Sprint 1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        PersistenceTestGraph.EnsureProject(context);
        var productA = new ProductEntity { ProductOwnerId = owner.Id, ProjectId = PersistenceTestGraph.DefaultProjectId, Name = "Product A" };
        var productB = new ProductEntity { ProductOwnerId = owner.Id, ProjectId = PersistenceTestGraph.DefaultProjectId, Name = "Product B" };

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

        var portfolioFlowSummaryService = new Mock<IPortfolioFlowSummaryService>(MockBehavior.Strict);
        portfolioFlowSummaryService
            .Setup(service => service.BuildTrend(
                It.Is<PortfolioFlowTrendRequest>(request =>
                    request.Sprints.Count == 1
                    && request.Sprints[0].SprintId == sprint.Id
                    && request.Projections.Count == 2
                    && request.Projections.Sum(projection => projection.StockStoryPoints) == 30d
                    && request.Projections.Sum(projection => projection.RemainingScopeStoryPoints) == 10d
                    && request.Projections.Sum(projection => projection.InflowStoryPoints) == 5d
                    && request.Projections.Sum(projection => projection.ThroughputStoryPoints) == 12d)))
            .Returns(new PortfolioFlowTrendResult(
                [
                    new PortfolioFlowSummaryResult(
                        sprint.Id,
                        StockStoryPoints: 30,
                        RemainingScopeStoryPoints: 10,
                        InflowStoryPoints: 5,
                        ThroughputStoryPoints: 12,
                        CompletionPercent: 66.667,
                        NetFlowStoryPoints: 7,
                        HasData: true)
                ],
                new PortfolioFlowTrendSummaryResult(
                    CumulativeNetFlowStoryPoints: 7,
                    TotalScopeChangeStoryPoints: 0,
                    TotalScopeChangePercent: 0,
                    RemainingScopeChangeStoryPoints: 0,
                    Trajectory: PortfolioTrajectory.Contracting)));

        var handler = new GetPortfolioProgressTrendQueryHandler(
            context,
            portfolioFlowSummaryService.Object,
            NullLogger<GetPortfolioProgressTrendQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetPortfolioProgressTrendQuery(
                DeliveryFilterTestFactory.MultiSprint(
                    [productA.Id, productB.Id],
                    [sprint.Id],
                    sprint.StartUtc,
                    sprint.EndUtc)),
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

        portfolioFlowSummaryService.VerifyAll();
    }

    [TestMethod]
    public async Task Handle_AppliesProductFilterBeforeInvokingCdcService()
    {
        await using var context = CreateContext();

        var owner = new ProfileEntity { Name = "PO 1" };
        context.Profiles.Add(owner);

        var team = new TeamEntity { Name = "Team 1", TeamAreaPath = "\\Project\\Team 1" };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var sprint1 = CreateSprint(team.Id, 101, "Sprint 1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var sprint2 = CreateSprint(team.Id, 102, "Sprint 2", new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        PersistenceTestGraph.EnsureProject(context);
        var productA = new ProductEntity { ProductOwnerId = owner.Id, ProjectId = PersistenceTestGraph.DefaultProjectId, Name = "Product A" };
        var productB = new ProductEntity { ProductOwnerId = owner.Id, ProjectId = PersistenceTestGraph.DefaultProjectId, Name = "Product B" };

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

        var portfolioFlowSummaryService = new Mock<IPortfolioFlowSummaryService>(MockBehavior.Strict);
        portfolioFlowSummaryService
            .Setup(service => service.BuildTrend(
                It.Is<PortfolioFlowTrendRequest>(request =>
                    request.Sprints.Count == 2
                    && request.Projections.Count == 1
                    && request.Projections[0].ProductId == productA.Id
                    && request.Projections[0].SprintId == sprint1.Id)))
            .Returns(new PortfolioFlowTrendResult(
                [
                    new PortfolioFlowSummaryResult(
                        sprint1.Id,
                        StockStoryPoints: 10,
                        RemainingScopeStoryPoints: 4,
                        InflowStoryPoints: 2,
                        ThroughputStoryPoints: 6,
                        CompletionPercent: 60,
                        NetFlowStoryPoints: 4,
                        HasData: true),
                    new PortfolioFlowSummaryResult(
                        sprint2.Id,
                        StockStoryPoints: null,
                        RemainingScopeStoryPoints: null,
                        InflowStoryPoints: null,
                        ThroughputStoryPoints: null,
                        CompletionPercent: null,
                        NetFlowStoryPoints: null,
                        HasData: false)
                ],
                new PortfolioFlowTrendSummaryResult(
                    CumulativeNetFlowStoryPoints: 4,
                    TotalScopeChangeStoryPoints: 0,
                    TotalScopeChangePercent: 0,
                    RemainingScopeChangeStoryPoints: 0,
                    Trajectory: PortfolioTrajectory.Contracting)));

        var handler = new GetPortfolioProgressTrendQueryHandler(
            context,
            portfolioFlowSummaryService.Object,
            NullLogger<GetPortfolioProgressTrendQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetPortfolioProgressTrendQuery(
                DeliveryFilterTestFactory.MultiSprint(
                    [productA.Id],
                    [sprint1.Id, sprint2.Id],
                    sprint1.StartUtc,
                    sprint2.EndUtc)),
            CancellationToken.None);

        Assert.HasCount(2, result.Sprints);
        Assert.IsTrue(result.Sprints[0].HasData);
        Assert.AreEqual(10d, result.Sprints[0].StockStoryPoints!.Value, 0.001d);

        Assert.IsFalse(result.Sprints[1].HasData);
        Assert.IsNull(result.Sprints[1].StockStoryPoints);
        Assert.IsNull(result.Sprints[1].RemainingScopeStoryPoints);
        Assert.IsNull(result.Sprints[1].InflowStoryPoints);
        Assert.IsNull(result.Sprints[1].ThroughputStoryPoints);
        Assert.IsNull(result.Sprints[1].CompletionPercent);
        Assert.IsNull(result.Sprints[1].NetFlowStoryPoints);

        portfolioFlowSummaryService.VerifyAll();
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
