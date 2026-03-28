using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Controllers;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.PullRequests.Queries;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public sealed class PullRequestsControllerCanonicalFilterTests
{
    [TestMethod]
    public async Task GetMetrics_WrapsResponseWithCanonicalFilterMetadata()
    {
        await using var context = CreateContext();
        context.Repositories.Add(new RepositoryEntity
        {
            Id = 1,
            ProductId = 100,
            Name = "Repo-A",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var fromDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        mediator
            .Setup(service => service.Send(
                It.Is<GetPullRequestMetricsQuery>(query =>
                    query.EffectiveFilter.Context.ProductIds.Values.SequenceEqual(new[] { 100 })
                    && query.EffectiveFilter.RepositoryScope.SequenceEqual(new[] { "Repo-A" })
                    && query.EffectiveFilter.RangeStartUtc == fromDate),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PullRequestMetricsDto(
                    1,
                    "PR-1",
                    "dev",
                    fromDate,
                    null,
                    "active",
                    "Sprint",
                    TimeSpan.FromHours(1),
                    null,
                    1,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0)
            });

        var filterService = new PullRequestFilterResolutionService(
            context,
            NullLogger<PullRequestFilterResolutionService>.Instance);
        var controller = new PullRequestsController(
            mediator.Object,
            filterService,
            NullLogger<PullRequestsController>.Instance);

        var result = await controller.GetMetrics("100", fromDate, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);

        var envelope = ok.Value as PullRequestQueryResponseDto<IReadOnlyList<PullRequestMetricsDto>>;
        Assert.IsNotNull(envelope);
        CollectionAssert.AreEqual(new[] { 100 }, envelope.RequestedFilter.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { 100 }, envelope.EffectiveFilter.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(Array.Empty<string>(), envelope.InvalidFields.ToArray());
        Assert.HasCount(1, envelope.Data);
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PullRequestsControllerCanonicalFilterTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
