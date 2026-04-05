using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Filters;
using PoTool.Core.PullRequests.Filters;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PullRequestFilterResolutionServiceTests
{
    [TestMethod]
    public async Task ResolveAsync_TeamScopeResolvesLinkedProductsAndRepositories()
    {
        await using var context = CreateContext();
        context.Teams.Add(new TeamEntity { Id = 10, Name = "Team 10", TeamAreaPath = "\\Team\\10" });
        PersistenceTestGraph.EnsureProject(context);
        context.Products.AddRange(
            PersistenceTestGraph.CreateProduct(100, "Product 100", 1),
            PersistenceTestGraph.CreateProduct(200, "Product 200", 1));
        context.ProductTeamLinks.Add(new ProductTeamLinkEntity { TeamId = 10, ProductId = 100 });
        context.Repositories.Add(PersistenceTestGraph.CreateRepository(1, 100, "Repo-Team"));
        context.PullRequests.AddRange(
            new PullRequestEntity
            {
                Id = 1,
                InternalId = 1,
                Title = "Scoped",
                RepositoryName = "Repo-Team",
                CreatedBy = "dev",
                CreatedDate = DateTimeOffset.UtcNow,
                CreatedDateUtc = DateTime.UtcNow,
                Status = "completed",
                IterationPath = "Sprint",
                SourceBranch = "feature",
                TargetBranch = "main",
                ProductId = 100,
                RetrievedAt = DateTimeOffset.UtcNow
            },
            new PullRequestEntity
            {
                Id = 2,
                InternalId = 2,
                Title = "Out of scope",
                RepositoryName = "Repo-Other",
                CreatedBy = "dev",
                CreatedDate = DateTimeOffset.UtcNow,
                CreatedDateUtc = DateTime.UtcNow,
                Status = "completed",
                IterationPath = "Sprint",
                SourceBranch = "feature",
                TargetBranch = "main",
                ProductId = 200,
                RetrievedAt = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        var service = new PullRequestFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<PullRequestFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PullRequestFilterBoundaryRequest(TeamId: 10),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 10 }, resolution.EffectiveFilter.Context.TeamIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { 100 }, resolution.EffectiveFilter.Context.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { "Repo-Team" }, resolution.EffectiveFilter.RepositoryScope.ToArray());
        Assert.AreEqual(FilterTimeSelectionMode.None, resolution.EffectiveFilter.Context.Time.Mode);
    }

    [TestMethod]
    public async Task ResolveAsync_SprintSelectionProducesRangeAndSprintMetadata()
    {
        await using var context = CreateContext();
        PersistenceTestGraph.EnsureTeam(context, 5);
        context.Sprints.Add(new SprintEntity
        {
            Id = 42,
            Name = "Sprint 42",
            TeamId = 5,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var service = new PullRequestFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<PullRequestFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PullRequestFilterBoundaryRequest(SprintId: 42),
            "TestBoundary",
            CancellationToken.None);

        Assert.AreEqual(FilterTimeSelectionMode.Sprint, resolution.EffectiveFilter.Context.Time.Mode);
        Assert.AreEqual(42, resolution.EffectiveFilter.SprintId);
        Assert.AreEqual(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), resolution.EffectiveFilter.RangeStartUtc);
        Assert.AreEqual(new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero), resolution.EffectiveFilter.RangeEndUtc);
    }

    [TestMethod]
    public async Task ResolveAsync_SprintOutsideSelectedTeamScope_IsInvalid()
    {
        await using var context = CreateContext();
        PersistenceTestGraph.EnsureTeam(context, 5);
        PersistenceTestGraph.EnsureTeam(context, 6);
        context.Sprints.Add(new SprintEntity
        {
            Id = 42,
            Name = "Sprint 42",
            TeamId = 5,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var service = new PullRequestFilterResolutionService(
            context,
            new ContextResolver(context),
            NullLogger<PullRequestFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PullRequestFilterBoundaryRequest(TeamId: 6, SprintId: 42),
            "TestBoundary",
            CancellationToken.None);

        Assert.IsFalse(resolution.Validation.IsValid);
        CollectionAssert.Contains(resolution.Validation.InvalidFields.ToArray(), nameof(ContextResolutionRequest.SprintIds));
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PullRequestFilterResolutionTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
