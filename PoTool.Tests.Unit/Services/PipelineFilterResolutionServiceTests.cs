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
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, resolution.EffectiveFilter.Context.RepositoryIds.Values.ToArray());
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, resolution.EffectiveFilter.RepositoryScope.ToArray());
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

    [TestMethod]
    public async Task ResolveAsync_RequestedRepositoryIds_UseStableIdsInsteadOfRepositoryNames()
    {
        await using var context = CreateContext();
        context.Products.Add(new ProductEntity { Id = 100, ProductOwnerId = 7, Name = "Product 100" });
        context.Repositories.Add(new RepositoryEntity
        {
            Id = 1,
            ProductId = 100,
            Name = "Renamed Repo",
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.PipelineDefinitions.Add(new PipelineDefinitionEntity
        {
            Id = 1,
            PipelineDefinitionId = 101,
            ProductId = 100,
            RepositoryId = 1,
            RepoId = "repo-1",
            RepoName = "Legacy Repo Name",
            Name = "Pipeline 101",
            DefaultBranch = "refs/heads/main",
            LastSyncedUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new PipelineFilterResolutionService(
            context,
            NullLogger<PipelineFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PipelineFilterBoundaryRequest(ProductIds: [100], RepositoryIds: [1]),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 1 }, resolution.RequestedFilter.RepositoryIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { 1 }, resolution.EffectiveFilter.RepositoryScope.ToArray());
        CollectionAssert.AreEqual(new[] { 101 }, resolution.EffectiveFilter.PipelineIds.ToArray());
        CollectionAssert.AreEqual(Array.Empty<string>(), resolution.Validation.InvalidFields.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_ExplicitValidProductSelectionWithinOwnerScope_RemainsUnchanged()
    {
        await using var context = CreateContext();
        SeedScopedProductsAndPipelines(context);
        await context.SaveChangesAsync();

        var service = new PipelineFilterResolutionService(
            context,
            NullLogger<PipelineFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PipelineFilterBoundaryRequest(ProductOwnerId: 7, ProductIds: [200], SprintId: 42),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 200 }, resolution.EffectiveFilter.Context.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(Array.Empty<string>(), resolution.Validation.InvalidFields.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_OutOfScopeProductSelectionFallsBackToOwnerScopeAndReportsValidation()
    {
        await using var context = CreateContext();
        SeedScopedProductsAndPipelines(context);
        context.Products.Add(new ProductEntity { Id = 300, ProductOwnerId = 9, Name = "Product 300" });
        await context.SaveChangesAsync();

        var service = new PipelineFilterResolutionService(
            context,
            NullLogger<PipelineFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PipelineFilterBoundaryRequest(ProductOwnerId: 7, ProductIds: [300], SprintId: 42),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 100, 200 }, resolution.EffectiveFilter.Context.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { nameof(PoTool.Core.Pipelines.Filters.PipelineFilterContext.ProductIds) }, resolution.Validation.InvalidFields.ToArray());
        Assert.IsTrue(resolution.Validation.Messages.Any(message => message.Field == nameof(PoTool.Core.Pipelines.Filters.PipelineFilterContext.ProductIds)));
    }

    [TestMethod]
    public async Task ResolveAsync_MixedValidAndOutOfScopeProductsFallsBackToOwnerScopeAndReportsValidation()
    {
        await using var context = CreateContext();
        SeedScopedProductsAndPipelines(context);
        context.Products.Add(new ProductEntity { Id = 300, ProductOwnerId = 9, Name = "Product 300" });
        await context.SaveChangesAsync();

        var service = new PipelineFilterResolutionService(
            context,
            NullLogger<PipelineFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PipelineFilterBoundaryRequest(ProductOwnerId: 7, ProductIds: [100, 300], SprintId: 42),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 100, 200 }, resolution.EffectiveFilter.Context.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { nameof(PoTool.Core.Pipelines.Filters.PipelineFilterContext.ProductIds) }, resolution.Validation.InvalidFields.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_ExplicitEmptyProductSelectionFallsBackToOwnerScopeAndReportsValidation()
    {
        await using var context = CreateContext();
        SeedScopedProductsAndPipelines(context);
        await context.SaveChangesAsync();

        var service = new PipelineFilterResolutionService(
            context,
            NullLogger<PipelineFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new PipelineFilterBoundaryRequest(ProductOwnerId: 7, ProductIds: [], SprintId: 42),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 100, 200 }, resolution.EffectiveFilter.Context.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { nameof(PoTool.Core.Pipelines.Filters.PipelineFilterContext.ProductIds) }, resolution.Validation.InvalidFields.ToArray());
        Assert.IsTrue(resolution.Validation.Messages.Any(message => message.Message.Contains("cannot be empty", StringComparison.Ordinal)));
    }

    private static void SeedScopedProductsAndPipelines(PoToolDbContext context)
    {
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
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PipelineFilterResolutionServiceTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
