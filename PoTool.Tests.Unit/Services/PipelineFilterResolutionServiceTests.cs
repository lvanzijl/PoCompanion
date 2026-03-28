using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Filters;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PipelineFilterResolutionServiceTests
{
    [TestMethod]
    public async Task ResolveAsync_ProductOwnerScopeResolvesProductsRepositoriesAndSprintWindow()
    {
        await using var context = CreateContext();
        context.Products.AddRange(
            new ProductEntity { Id = 100, ProductOwnerId = 7, Name = "Product 100" },
            new ProductEntity { Id = 200, ProductOwnerId = 7, Name = "Product 200" });
        context.Repositories.AddRange(
            new RepositoryEntity { Id = 1, ProductId = 100, Name = "Repo-A", CreatedAt = DateTimeOffset.UtcNow },
            new RepositoryEntity { Id = 2, ProductId = 200, Name = "Repo-B", CreatedAt = DateTimeOffset.UtcNow });
        context.PipelineDefinitions.AddRange(
            new PipelineDefinitionEntity
            {
                Id = 1,
                PipelineDefinitionId = 11,
                ProductId = 100,
                RepositoryId = 1,
                RepoId = "repo-1",
                RepoName = "Repo-A",
                Name = "Pipeline A",
                DefaultBranch = "refs/heads/main",
                LastSyncedUtc = DateTimeOffset.UtcNow
            },
            new PipelineDefinitionEntity
            {
                Id = 2,
                PipelineDefinitionId = 22,
                ProductId = 200,
                RepositoryId = 2,
                RepoId = "repo-2",
                RepoName = "Repo-B",
                Name = "Pipeline B",
                DefaultBranch = "refs/heads/release",
                LastSyncedUtc = DateTimeOffset.UtcNow
            });
        context.Sprints.Add(new SprintEntity
        {
            Id = 42,
            Name = "Sprint 42",
            TeamId = 3,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var service = new PipelineFilterResolutionService(
            context,
            NullLogger<PipelineFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PipelineFilterBoundaryRequest(ProductOwnerId: 7, SprintId: 42),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 100, 200 }, resolution.EffectiveFilter.Context.ProductIds.Values.ToArray());
        CollectionAssert.AreEquivalent(new[] { "Repo-A", "Repo-B" }, resolution.EffectiveFilter.RepositoryScope.ToArray());
        CollectionAssert.AreEqual(new[] { 11, 22 }, resolution.EffectiveFilter.PipelineIds.ToArray());
        Assert.AreEqual(FilterTimeSelectionMode.Sprint, resolution.EffectiveFilter.Context.Time.Mode);
        Assert.AreEqual(42, resolution.EffectiveFilter.SprintId);
        Assert.AreEqual(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), resolution.EffectiveFilter.RangeStartUtc);
        Assert.AreEqual(new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero), resolution.EffectiveFilter.RangeEndUtc);
    }

    [TestMethod]
    public async Task ResolveAsync_InvalidSprintProducesValidationMessage()
    {
        await using var context = CreateContext();

        var service = new PipelineFilterResolutionService(
            context,
            NullLogger<PipelineFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PipelineFilterBoundaryRequest(SprintId: 999),
            "TestBoundary",
            CancellationToken.None);

        Assert.AreEqual(FilterTimeSelectionMode.None, resolution.EffectiveFilter.Context.Time.Mode);
        Assert.IsNull(resolution.EffectiveFilter.SprintId);
        CollectionAssert.AreEqual(new[] { nameof(PoTool.Core.Pipelines.Filters.PipelineFilterContext.Time) }, resolution.Validation.InvalidFields.ToArray());
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PipelineFilterResolutionServiceTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
