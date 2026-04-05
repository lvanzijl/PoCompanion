using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Controllers;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.Metrics;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public sealed class MetricsControllerDeliveryCanonicalFilterTests
{
    [TestMethod]
    public async Task GetPortfolioProgressTrend_WrapsResponseWithCanonicalFilterMetadata()
    {
        await using var context = CreateContext();
        PersistenceTestGraph.EnsureProject(context);
        PersistenceTestGraph.EnsureTeam(context, 3);
        context.Products.Add(PersistenceTestGraph.CreateProduct(100, "Product 100", 7));
        context.ProductTeamLinks.Add(new ProductTeamLinkEntity { ProductId = 100, TeamId = 3 });
        context.Sprints.Add(new SprintEntity
        {
            Id = 42,
            Name = "Sprint 42",
            TeamId = 3,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(
                It.Is<GetPortfolioProgressTrendQuery>(query =>
                    query.EffectiveFilter.Context.ProductIds.Values.SequenceEqual(new[] { 100 })
                    && query.EffectiveFilter.SprintIds.SequenceEqual(new[] { 42 })
                    && query.EffectiveFilter.RangeStartUtc == new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)
                    && query.EffectiveFilter.RangeEndUtc == new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioProgressTrendDto
            {
                Sprints = Array.Empty<PortfolioSprintProgressDto>(),
                Summary = new PortfolioProgressSummaryDto
                {
                    Trajectory = PortfolioTrajectory.Stable
                }
            });

        var filterService = new DeliveryFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<DeliveryFilterResolutionService>.Instance);
        var sprintFilterService = new SprintFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<SprintFilterResolutionService>.Instance);
        var cacheStateRepository = new Mock<ICacheStateRepository>();
        cacheStateRepository
            .Setup(repository => repository.GetCacheStateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheStateDto
            {
                ProductOwnerId = 7,
                SyncStatus = CacheSyncStatusDto.Success,
                LastSuccessfulSync = DateTimeOffset.UtcNow
            });
        var currentProfileProvider = new Mock<ICurrentProfileProvider>();
        currentProfileProvider
            .Setup(provider => provider.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        var cacheReadinessStateService = new CacheReadinessStateService(currentProfileProvider.Object, cacheStateRepository.Object);
        var cacheStateResponseService = new CacheStateResponseService(cacheReadinessStateService, NullLogger<CacheStateResponseService>.Instance);
        var controller = new MetricsController(
            mediator.Object,
            cacheStateResponseService,
            filterService,
            sprintFilterService,
            NullLogger<MetricsController>.Instance);

        var result = await controller.GetPortfolioProgressTrend(7, [42], null, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);

        var envelope = ok.Value as DeliveryQueryResponseDto<PortfolioProgressTrendDto>;
        Assert.IsNotNull(envelope);
        CollectionAssert.AreEqual(Array.Empty<int>(), envelope.RequestedFilter.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { 100 }, envelope.EffectiveFilter.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(Array.Empty<string>(), envelope.InvalidFields.ToArray());
        mediator.VerifyAll();
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"MetricsControllerDeliveryCanonicalFilterTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
