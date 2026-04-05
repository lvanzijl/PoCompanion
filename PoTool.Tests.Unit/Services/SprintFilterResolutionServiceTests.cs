using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Filters;
using PoTool.Core.Metrics.Filters;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class SprintFilterResolutionServiceTests
{
    [TestMethod]
    public async Task ResolveAsync_ProductOwnerScopeDerivesProductsAndMultiSprintWindow()
    {
        await using var context = CreateContext();
        PersistenceTestGraph.EnsureProject(context);
        PersistenceTestGraph.EnsureTeam(context, 3);
        context.Products.AddRange(
            PersistenceTestGraph.CreateProduct(100, "Product 100", 7),
            PersistenceTestGraph.CreateProduct(200, "Product 200", 7),
            PersistenceTestGraph.CreateProduct(300, "Product 300", 9));
        context.ProductTeamLinks.AddRange(
            new ProductTeamLinkEntity { ProductId = 100, TeamId = 3 },
            new ProductTeamLinkEntity { ProductId = 200, TeamId = 3 },
            new ProductTeamLinkEntity { ProductId = 300, TeamId = 3 });
        context.Sprints.AddRange(
            new SprintEntity
            {
                Id = 42,
                Name = "Sprint 42",
                Path = "\\Project\\Sprint 42",
                TeamId = 3,
                StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
            },
            new SprintEntity
            {
                Id = 43,
                Name = "Sprint 43",
                Path = "\\Project\\Sprint 43",
                TeamId = 3,
                StartDateUtc = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDateUtc = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc)
            });
        await context.SaveChangesAsync();

        var service = new SprintFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<SprintFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new SprintFilterBoundaryRequest(
                ProductOwnerId: 7,
                SprintIds: [43, 42]),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 100, 200 }, resolution.EffectiveFilter.Context.ProductIds.Values.ToArray());
        Assert.AreEqual(FilterTimeSelectionMode.MultiSprint, resolution.EffectiveFilter.Context.Time.Mode);
        CollectionAssert.AreEqual(new[] { 42, 43 }, resolution.EffectiveFilter.SprintIds.ToArray());
        CollectionAssert.AreEqual(new[] { "\\Project\\Sprint 42", "\\Project\\Sprint 43" }, resolution.EffectiveFilter.IterationPaths.ToArray());
        Assert.AreEqual(43, resolution.EffectiveFilter.CurrentSprintId);
        Assert.AreEqual(42, resolution.EffectiveFilter.PreviousSprintId);
        Assert.AreEqual(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), resolution.EffectiveFilter.RangeStartUtc);
        Assert.AreEqual(new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero), resolution.EffectiveFilter.RangeEndUtc);
        CollectionAssert.AreEqual(Array.Empty<string>(), resolution.Validation.InvalidFields.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_LegacyIterationPathNormalizesToSprintScope()
    {
        await using var context = CreateContext();
        PersistenceTestGraph.EnsureTeam(context, 3);
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

        var service = new SprintFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<SprintFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new SprintFilterBoundaryRequest(IterationPath: "\\Project\\Sprint 42"),
            "TestBoundary",
            CancellationToken.None);

        Assert.AreEqual(FilterTimeSelectionMode.Sprint, resolution.EffectiveFilter.Context.Time.Mode);
        Assert.AreEqual(42, resolution.EffectiveFilter.SprintId);
        CollectionAssert.AreEqual(new[] { "\\Project\\Sprint 42" }, resolution.EffectiveFilter.IterationPaths.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_SelectedSprintOutsideProductScope_IsInvalid()
    {
        await using var context = CreateContext();
        PersistenceTestGraph.EnsureProject(context);
        PersistenceTestGraph.EnsureTeam(context, 3);
        PersistenceTestGraph.EnsureTeam(context, 9);
        context.Products.AddRange(
            PersistenceTestGraph.CreateProduct(100, "Product 100", 7),
            PersistenceTestGraph.CreateProduct(200, "Product 200", 7));
        context.ProductTeamLinks.AddRange(
            new ProductTeamLinkEntity { ProductId = 100, TeamId = 3 },
            new ProductTeamLinkEntity { ProductId = 200, TeamId = 9 });
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

        var service = new SprintFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<SprintFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new SprintFilterBoundaryRequest(
                ProductOwnerId: 7,
                ProductIds: [200],
                SprintId: 42),
            "TestBoundary",
            CancellationToken.None);

        Assert.IsFalse(resolution.Validation.IsValid);
        CollectionAssert.Contains(resolution.Validation.InvalidFields.ToArray(), nameof(ContextResolutionRequest.SprintIds));
    }

    [TestMethod]
    public async Task ResolveAsync_SprintExecutionWithoutExplicitProduct_IsInvalidWhenRequired()
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
            Path = "\\Project\\Sprint 42",
            TeamId = 3,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var service = new SprintFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<SprintFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new SprintFilterBoundaryRequest(
                ProductOwnerId: 7,
                RequireExplicitProductScope: true,
                SprintId: 42),
            "TestBoundary",
            CancellationToken.None);

        Assert.IsFalse(resolution.Validation.IsValid);
        CollectionAssert.Contains(resolution.Validation.InvalidFields.ToArray(), nameof(SprintFilterContext.ProductIds));
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"SprintFilterResolutionServiceTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
