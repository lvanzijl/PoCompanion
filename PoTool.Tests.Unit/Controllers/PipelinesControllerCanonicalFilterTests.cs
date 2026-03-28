using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Controllers;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Pipelines.Queries;
using PoTool.Shared.Pipelines;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public sealed class PipelinesControllerCanonicalFilterTests
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
        context.PipelineDefinitions.Add(new PipelineDefinitionEntity
        {
            Id = 1,
            PipelineDefinitionId = 101,
            ProductId = 100,
            RepositoryId = 1,
            RepoId = "repo-1",
            RepoName = "Repo-A",
            Name = "Pipeline 101",
            DefaultBranch = "refs/heads/main",
            LastSyncedUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var fromDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        mediator
            .Setup(service => service.Send(
                It.Is<GetPipelineMetricsQuery>(query =>
                    query.EffectiveFilter.Context.ProductIds.Values.SequenceEqual(new[] { 100 })
                    && query.EffectiveFilter.RepositoryScope.SequenceEqual(new[] { "Repo-A" })
                    && query.EffectiveFilter.PipelineIds.SequenceEqual(new[] { 101 })
                    && query.EffectiveFilter.RangeStartUtc == fromDate),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PipelineMetricsDto(
                    101,
                    "Pipeline 101",
                    PipelineType.Build,
                    1,
                    1,
                    0,
                    0,
                    0,
                    0,
                    TimeSpan.FromHours(1),
                    TimeSpan.FromHours(1),
                    TimeSpan.FromHours(1),
                    null,
                    PipelineRunResult.Succeeded,
                    fromDate,
                    0)
            });

        var filterService = new PipelineFilterResolutionService(
            context,
            NullLogger<PipelineFilterResolutionService>.Instance);
        var controller = new PipelinesController(
            mediator.Object,
            filterService,
            NullLogger<PipelinesController>.Instance);

        var result = await controller.GetMetrics("100", fromDate, null, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);

        var envelope = ok.Value as PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>;
        Assert.IsNotNull(envelope);
        CollectionAssert.AreEqual(new[] { 100 }, envelope.RequestedFilter.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { 100 }, envelope.EffectiveFilter.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(Array.Empty<string>(), envelope.InvalidFields.ToArray());
        Assert.HasCount(1, envelope.Data);
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PipelinesControllerCanonicalFilterTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
