using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Filters;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ContextResolverTests
{
    [TestMethod]
    public async Task ResolveAsync_DerivesProductsFromTeamScope()
    {
        await using var context = CreateContext();
        PersistenceTestGraph.EnsureProject(context);
        PersistenceTestGraph.EnsureTeam(context, 3);
        context.Products.AddRange(
            PersistenceTestGraph.CreateProduct(100, "Product 100", 7),
            PersistenceTestGraph.CreateProduct(200, "Product 200", 7));
        context.ProductTeamLinks.AddRange(
            new ProductTeamLinkEntity { ProductId = 100, TeamId = 3 },
            new ProductTeamLinkEntity { ProductId = 200, TeamId = 3 });
        await context.SaveChangesAsync();

        var resolver = new ContextResolver(context);

        var result = await resolver.ResolveAsync(
            new ContextResolutionRequest(
                FilterSelection<int>.All(),
                FilterSelection<int>.Selected([3]),
                Array.Empty<int>(),
                DeriveProductsFromTeams: true),
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 100, 200 }, result.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(Array.Empty<string>(), result.Validation.InvalidFields.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_RejectsSprintOutsideSelectedProductScope()
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
            TeamId = 3,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var resolver = new ContextResolver(context);

        var result = await resolver.ResolveAsync(
            new ContextResolutionRequest(
                FilterSelection<int>.Selected([200]),
                FilterSelection<int>.All(),
                [42]),
            CancellationToken.None);

        Assert.IsFalse(result.Validation.IsValid);
        CollectionAssert.Contains(result.Validation.InvalidFields.ToArray(), nameof(ContextResolutionRequest.SprintIds));
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"ContextResolverTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
