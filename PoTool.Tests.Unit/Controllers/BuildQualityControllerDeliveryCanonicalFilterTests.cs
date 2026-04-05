using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Controllers;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.BuildQuality.Queries;
using PoTool.Shared.BuildQuality;
using PoTool.Shared.Metrics;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public sealed class BuildQualityControllerDeliveryCanonicalFilterTests
{
    [TestMethod]
    public async Task GetSprint_WrapsResponseWithCanonicalFilterMetadata()
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
                It.Is<GetBuildQualitySprintQuery>(query =>
                    query.ProductOwnerId == 7
                    && query.EffectiveFilter.Context.ProductIds.Values.SequenceEqual(new[] { 100 })
                    && query.EffectiveFilter.SprintId == 42
                    && query.EffectiveFilter.RangeStartUtc == new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)
                    && query.EffectiveFilter.RangeEndUtc == new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryBuildQualityDto
            {
                ProductOwnerId = 7,
                SprintId = 42,
                Summary = new BuildQualityResultDto()
            });

        var filterService = new DeliveryFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<DeliveryFilterResolutionService>.Instance);
        var controller = new BuildQualityController(
            mediator.Object,
            filterService);

        var result = await controller.GetSprint(7, 42, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);

        var envelope = ok.Value as DeliveryQueryResponseDto<DeliveryBuildQualityDto>;
        Assert.IsNotNull(envelope);
        CollectionAssert.AreEqual(new[] { 100 }, envelope.EffectiveFilter.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(Array.Empty<string>(), envelope.InvalidFields.ToArray());
        mediator.VerifyAll();
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"BuildQualityControllerDeliveryCanonicalFilterTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
