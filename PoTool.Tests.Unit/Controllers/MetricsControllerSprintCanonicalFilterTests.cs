using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Controllers;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Controllers;

[TestClass]
public sealed class MetricsControllerSprintCanonicalFilterTests
{
    [TestMethod]
    public async Task GetSprintExecution_WrapsResponseWithCanonicalFilterMetadata()
    {
        await using var context = CreateContext();
        context.Products.Add(new ProductEntity { Id = 100, ProductOwnerId = 7, Name = "Product 100" });
        context.Sprints.Add(new SprintEntity
        {
            Id = 42,
            Name = "Sprint 42",
            Path = "\\Project\\Sprint 42",
            TeamId = 3,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(
                It.Is<GetSprintExecutionQuery>(query =>
                    query.ProductOwnerId == 7
                    && query.EffectiveFilter.Context.ProductIds.Values.SequenceEqual(new[] { 100 })
                    && query.EffectiveFilter.SprintId == 42),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SprintExecutionDto
            {
                SprintId = 42,
                SprintName = "Sprint 42",
                HasData = true,
                Summary = new SprintExecutionSummaryDto(),
                CompletedPbis = Array.Empty<SprintExecutionPbiDto>(),
                UnfinishedPbis = Array.Empty<SprintExecutionPbiDto>(),
                AddedDuringSprint = Array.Empty<SprintExecutionPbiDto>(),
                RemovedDuringSprint = Array.Empty<SprintExecutionPbiDto>(),
                SpilloverPbis = Array.Empty<SprintExecutionPbiDto>(),
                StarvedPbis = Array.Empty<SprintExecutionPbiDto>()
            });

        var controller = CreateController(context, mediator.Object);

        var result = await controller.GetSprintExecution(7, 42, 100, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);

        var envelope = ok.Value as SprintQueryResponseDto<SprintExecutionDto>;
        Assert.IsNotNull(envelope);
        CollectionAssert.AreEqual(new[] { 100 }, envelope.EffectiveFilter.ProductIds.Values.ToArray());
        Assert.AreEqual(FilterTimeSelectionModeDto.Sprint, envelope.EffectiveFilter.Time.Mode);
        Assert.AreEqual(42, envelope.EffectiveFilter.Time.SprintId);
        CollectionAssert.AreEqual(Array.Empty<string>(), envelope.InvalidFields.ToArray());
        mediator.VerifyAll();
    }

    [TestMethod]
    public async Task GetBacklogHealth_WrapsLegacyIterationPathWithCanonicalFilterMetadata()
    {
        await using var context = CreateContext();
        context.Sprints.Add(new SprintEntity
        {
            Id = 42,
            Name = "Sprint 42",
            Path = "\\Project\\Sprint 42",
            TeamId = 3,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(
                It.Is<GetBacklogHealthQuery>(query =>
                    query.EffectiveFilter.IterationPath == "\\Project\\Sprint 42"
                    && query.EffectiveFilter.SprintId == 42),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BacklogHealthDto(
                "\\Project\\Sprint 42",
                "Sprint 42",
                1,
                0,
                0,
                0,
                0,
                0,
                new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero),
                Array.Empty<ValidationIssueSummary>()));

        var controller = CreateController(context, mediator.Object);

        var result = await controller.GetBacklogHealth("\\Project\\Sprint 42", null, null, null, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);

        var envelope = ok.Value as SprintQueryResponseDto<BacklogHealthDto>;
        Assert.IsNotNull(envelope);
        CollectionAssert.AreEqual(new[] { "\\Project\\Sprint 42" }, envelope.EffectiveFilter.IterationPaths.Values.ToArray());
        Assert.AreEqual(FilterTimeSelectionModeDto.Sprint, envelope.EffectiveFilter.Time.Mode);
        Assert.AreEqual(42, envelope.EffectiveFilter.Time.SprintId);
        mediator.VerifyAll();
    }

    [TestMethod]
    public async Task GetBacklogHealth_WhenNoItemsMatch_ReturnsEmptyEnvelopeInsteadOf404()
    {
        await using var context = CreateContext();
        context.Sprints.Add(new SprintEntity
        {
            Id = 42,
            Name = "Sprint 42",
            Path = "\\Project\\Sprint 42",
            TeamId = 3,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(It.IsAny<GetBacklogHealthQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BacklogHealthDto?)null);

        var controller = CreateController(context, mediator.Object);

        var result = await controller.GetBacklogHealth("\\Project\\Sprint 42", null, null, null, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);

        var envelope = ok.Value as SprintQueryResponseDto<BacklogHealthDto>;
        Assert.IsNotNull(envelope);
        Assert.AreEqual("\\Project\\Sprint 42", envelope.Data.IterationPath);
        Assert.AreEqual(0, envelope.Data.TotalWorkItems);
        Assert.IsEmpty(envelope.Data.ValidationIssues);
        mediator.VerifyAll();
    }

    [TestMethod]
    public async Task GetSprintMetrics_WhenNoItemsMatch_ReturnsEmptyEnvelopeInsteadOf404()
    {
        await using var context = CreateContext();
        context.Sprints.Add(new SprintEntity
        {
            Id = 42,
            Name = "Sprint 42",
            Path = "\\Project\\Sprint 42",
            TeamId = 3,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(service => service.Send(It.IsAny<GetSprintMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SprintMetricsDto?)null);

        var controller = CreateController(context, mediator.Object);

        var result = await controller.GetSprintMetrics("\\Project\\Sprint 42", null, null, null, CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);

        var envelope = ok.Value as SprintQueryResponseDto<SprintMetricsDto>;
        Assert.IsNotNull(envelope);
        Assert.AreEqual("\\Project\\Sprint 42", envelope.Data.IterationPath);
        Assert.AreEqual(0, envelope.Data.TotalWorkItemCount);
        Assert.AreEqual(0, envelope.Data.CompletedStoryPoints);
        mediator.VerifyAll();
    }

    private static MetricsController CreateController(PoToolDbContext context, IMediator mediator)
    {
        var deliveryFilterService = new DeliveryFilterResolutionService(
            context,
            NullLogger<DeliveryFilterResolutionService>.Instance);
        var sprintFilterService = new SprintFilterResolutionService(
            context,
            NullLogger<SprintFilterResolutionService>.Instance);

        return new MetricsController(
            mediator,
            deliveryFilterService,
            sprintFilterService,
            NullLogger<MetricsController>.Instance);
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"MetricsControllerSprintCanonicalFilterTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
