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
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public sealed class PipelinesControllerCanonicalFilterTests
{
    [TestMethod]
    public async Task GetMetrics_WrapsResponseWithCanonicalFilterMetadata()
    {
        await using var context = CreateContext();
        PersistenceTestGraph.EnsureProject(context);
        context.Products.Add(PersistenceTestGraph.CreateProduct(100, "Product 100", 7));
        context.Repositories.Add(PersistenceTestGraph.CreateRepository(1, 100, "Repo-A"));
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
                    && query.EffectiveFilter.RepositoryScope.SequenceEqual(new[] { 1 })
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
            new ContextResolver(context),
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
        CollectionAssert.AreEqual(new[] { 1 }, envelope.EffectiveFilter.RepositoryIds.Values.ToArray());
        CollectionAssert.AreEqual(Array.Empty<string>(), envelope.InvalidFields.ToArray());
        Assert.HasCount(1, envelope.Data);
    }

    [TestMethod]
    public async Task GetInsights_WrapsResponseWithCanonicalFilterMetadata()
    {
        await using var context = CreateContext();
        PersistenceTestGraph.EnsureProject(context);
        PersistenceTestGraph.EnsureTeam(context, 3);
        context.Products.Add(PersistenceTestGraph.CreateProduct(100, "Product 100", 7));
        context.Products.Add(PersistenceTestGraph.CreateProduct(200, "Product 200", 7));
        context.ProductTeamLinks.Add(new ProductTeamLinkEntity { ProductId = 100, TeamId = 3 });
        context.ProductTeamLinks.Add(new ProductTeamLinkEntity { ProductId = 200, TeamId = 3 });
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
                It.Is<GetPipelineInsightsQuery>(query =>
                    query.EffectiveFilter.Context.ProductIds.Values.SequenceEqual(new[] { 100 })
                    && query.EffectiveFilter.SprintId == 42),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineInsightsDto
            {
                SprintId = 42,
                SprintName = "Sprint 42"
            });

        var filterService = new PipelineFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<PipelineFilterResolutionService>.Instance);
        var controller = new PipelinesController(
            mediator.Object,
            filterService,
            NullLogger<PipelinesController>.Instance);

        var result = await controller.GetInsights(7, 42, [100], true, false, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);

        var envelope = ok.Value as PipelineQueryResponseDto<PipelineInsightsDto>;
        Assert.IsNotNull(envelope);
        CollectionAssert.AreEqual(new[] { 100 }, envelope.RequestedFilter.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { 100 }, envelope.EffectiveFilter.ProductIds.Values.ToArray());
        Assert.AreEqual(42, envelope.EffectiveFilter.Time.SprintId);
        CollectionAssert.AreEqual(Array.Empty<string>(), envelope.InvalidFields.ToArray());
        mediator.VerifyAll();
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PipelinesControllerCanonicalFilterTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
