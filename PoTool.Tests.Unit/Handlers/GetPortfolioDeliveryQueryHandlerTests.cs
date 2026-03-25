using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

[TestClass]
public sealed class GetPortfolioDeliveryQueryHandlerTests
{
    [TestMethod]
    public async Task Handle_DelegatesPortfolioDeliveryRollupsToCdcServiceAndMapsResult()
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

        context.SprintMetricsProjections.AddRange(
            new SprintMetricsProjectionEntity
            {
                SprintId = sprint.Id,
                ProductId = productA.Id,
                CompletedPbiCount = 2,
                CompletedPbiStoryPoints = 8,
                BugsCreatedCount = 1,
                BugsWorkedCount = 2,
                BugsClosedCount = 1,
                ProgressionDelta = 20
            },
            new SprintMetricsProjectionEntity
            {
                SprintId = sprint.Id,
                ProductId = productB.Id,
                CompletedPbiCount = 1,
                CompletedPbiStoryPoints = 2,
                BugsCreatedCount = 0,
                BugsWorkedCount = 1,
                BugsClosedCount = 0,
                ProgressionDelta = 10
            });
        await context.SaveChangesAsync();

        var projectionService = new StubSprintTrendProjectionService(
            [
                new FeatureProgressDto
                {
                    FeatureId = 101,
                    FeatureTitle = "Feature A",
                    EpicTitle = "Epic A",
                    ProductId = productA.Id,
                    SprintCompletedEffort = 8,
                    TotalStoryPoints = 13,
                    ProgressPercent = 60
                }
            ]);

        var portfolioDeliverySummaryService = new Mock<IPortfolioDeliverySummaryService>(MockBehavior.Strict);
        portfolioDeliverySummaryService
            .Setup(service => service.BuildSummary(
                It.Is<PortfolioDeliverySummaryRequest>(request =>
                    request.ProductProjections.Count == 2
                    && request.ProductProjections.Sum(projection => projection.DeliveredStoryPoints) == 10d
                    && request.FeatureContributions.Count == 1
                    && request.FeatureContributions[0].DeliveredStoryPoints == 8d
                    && request.TopFeatureLimit == 10)))
            .Returns(new PortfolioDeliverySummaryResult(
                TotalDeliveredStoryPoints: 10,
                TotalCompletedPbis: 3,
                AverageProgressPercent: 15,
                TotalBugsCreated: 1,
                TotalBugsWorked: 3,
                TotalBugsClosed: 1,
                ProductSummaries:
                [
                    new PortfolioProductDeliverySummaryResult(productA.Id, "Product A", 2, 8, 80, 1, 2, 1, 20),
                    new PortfolioProductDeliverySummaryResult(productB.Id, "Product B", 1, 2, 20, 0, 1, 0, 10)
                ],
                FeatureContributionSummaries:
                [
                    new PortfolioFeatureContributionSummaryResult(101, "Feature A", "Epic A", productA.Id, "Product A", 8, 80, 13, 60)
                ]));

        var handler = new GetPortfolioDeliveryQueryHandler(
            context,
            projectionService,
            portfolioDeliverySummaryService.Object,
            NullLogger<GetPortfolioDeliveryQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetPortfolioDeliveryQuery(owner.Id, [sprint.Id]),
            CancellationToken.None);

        Assert.IsTrue(result.HasData);
        Assert.AreEqual(1, result.SprintCount);
        Assert.AreEqual(3, result.Summary.TotalCompletedPbis);
        Assert.AreEqual(10d, result.Summary.TotalCompletedEffort, 0.001d);
        Assert.AreEqual(FeatureProgressMode.StoryPoints, projectionService.LastProgressMode);
        Assert.AreEqual(10d, result.Summary.TotalDeliveredStoryPoints, 0.001d);
        Assert.AreEqual(1, result.Summary.TotalCompletedBugs);
        Assert.HasCount(2, result.Products);
        Assert.AreEqual(8d, result.Products[0].CompletedEffort, 0.001d);
        Assert.AreEqual(8d, result.Products[0].DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(80d, result.Products[0].EffortShare, 0.001d);
        Assert.AreEqual(80d, result.Products[0].DeliveredSharePercent, 0.001d);
        Assert.HasCount(1, result.TopFeatures);
        Assert.AreEqual(8d, result.TopFeatures[0].SprintCompletedEffort, 0.001d);
        Assert.AreEqual(8d, result.TopFeatures[0].DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(13d, result.TopFeatures[0].TotalStoryPoints, 0.001d);
        Assert.AreEqual(80d, result.TopFeatures[0].EffortShare, 0.001d);
        Assert.AreEqual(80d, result.TopFeatures[0].DeliveredSharePercent, 0.001d);

        portfolioDeliverySummaryService.VerifyAll();
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PortfolioDelivery_{Guid.NewGuid()}")
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

    private sealed class StubSprintTrendProjectionService : SprintTrendProjectionService
    {
        private readonly IReadOnlyList<FeatureProgressDto> _featureProgress;
        public FeatureProgressMode? LastProgressMode { get; private set; }

        public StubSprintTrendProjectionService(IReadOnlyList<FeatureProgressDto> featureProgress)
            : base(
                Mock.Of<IServiceScopeFactory>(),
                NullLogger<SprintTrendProjectionService>.Instance,
                stateClassificationService: null,
                new CanonicalStoryPointResolutionService(),
                new HierarchyRollupService(new CanonicalStoryPointResolutionService()),
                new DeliveryProgressRollupService(
                    new CanonicalStoryPointResolutionService(),
                    new HierarchyRollupService(new CanonicalStoryPointResolutionService())),
                new SprintCommitmentService(),
                new SprintCompletionService(),
                new SprintSpilloverService(),
                new SprintDeliveryProjectionService(
                    new CanonicalStoryPointResolutionService(),
                    new HierarchyRollupService(new CanonicalStoryPointResolutionService()),
                    new DeliveryProgressRollupService(
                        new CanonicalStoryPointResolutionService(),
                        new HierarchyRollupService(new CanonicalStoryPointResolutionService())),
                    new SprintCompletionService(),
                    new SprintSpilloverService()))
        {
            _featureProgress = featureProgress;
        }

        public override Task<IReadOnlyList<FeatureProgressDto>> ComputeFeatureProgressAsync(
            int productOwnerId,
            FeatureProgressMode progressMode,
            DateTime? sprintStartUtc = null,
            DateTime? sprintEndUtc = null,
            CancellationToken cancellationToken = default,
            int? sprintId = null)
        {
            LastProgressMode = progressMode;
            return Task.FromResult(_featureProgress);
        }
    }
}
