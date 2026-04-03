using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Filters;
using PoTool.Tests.Unit.TestSupport;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioFilterResolutionServiceTests
{
    [TestMethod]
    public async Task ResolveAsync_ReplacesProjectMismatchWithAllProjects()
    {
        await using var context = CreateContext();
        await SeedOwnerDataAsync(context);
        var service = CreateService(context);

        var resolution = await service.ResolveAsync(
            42,
            new PortfolioReadQueryOptions(ProductId: 1, ProjectNumber: "PRJ-200"),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.Contains(resolution.Validation.InvalidFields.ToArray(), nameof(FilterContext.ProjectNumbers));
        Assert.IsTrue(resolution.EffectiveFilter.ProjectNumbers.IsAll);
        Assert.IsFalse(resolution.RequestedFilter.ProjectNumbers.IsAll);
    }

    [TestMethod]
    public async Task ResolveAsync_ReplacesInvalidDateRangeWithNoTimeConstraint()
    {
        await using var context = CreateContext();
        await SeedOwnerDataAsync(context);
        var service = CreateService(context);

        var resolution = await service.ResolveAsync(
            42,
            new PortfolioReadQueryOptions(
                RangeStartUtc: new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
                RangeEndUtc: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.Contains(resolution.Validation.InvalidFields.ToArray(), nameof(FilterContext.Time));
        Assert.AreEqual(FilterTimeSelectionMode.None, resolution.EffectiveFilter.Time.Mode);
    }

    [TestMethod]
    public async Task ResolveAsync_KeepsValidScopedSelections()
    {
        await using var context = CreateContext();
        await SeedOwnerDataAsync(context);
        var service = CreateService(context);

        var resolution = await service.ResolveAsync(
            42,
            new PortfolioReadQueryOptions(
                ProductId: 1,
                ProjectNumber: "PRJ-100",
                WorkPackage: "WP-1",
                LifecycleState: PortfolioLifecycleState.Active,
                RangeStartUtc: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                RangeEndUtc: new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)),
            "TestBoundary",
            CancellationToken.None);

        Assert.IsTrue(resolution.Validation.IsValid);
        CollectionAssert.AreEqual(new[] { 1 }, resolution.EffectiveFilter.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { "PRJ-100" }, resolution.EffectiveFilter.ProjectNumbers.Values.ToArray());
        CollectionAssert.AreEqual(new[] { "WP-1" }, resolution.EffectiveFilter.WorkPackages.Values.ToArray());
        CollectionAssert.AreEqual(new[] { PortfolioLifecycleState.Active }, resolution.EffectiveFilter.LifecycleStates.Values.ToArray());
        Assert.AreEqual(FilterTimeSelectionMode.DateRange, resolution.EffectiveFilter.Time.Mode);
    }

    private static PortfolioFilterResolutionService CreateService(PoToolDbContext context)
        => new(
            context,
            new FilterContextValidator(),
            NullLogger<PortfolioFilterResolutionService>.Instance);

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PortfolioFilterResolutionTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }

    private static async Task SeedOwnerDataAsync(PoToolDbContext context)
    {
        PersistenceTestGraph.EnsureProject(context);
        context.Products.AddRange(
            PersistenceTestGraph.CreateProduct(1, "Product A", 42),
            PersistenceTestGraph.CreateProduct(2, "Product B", 42));

        context.PortfolioSnapshots.AddRange(
            new PortfolioSnapshotEntity
            {
                SnapshotId = 1,
                ProductId = 1,
                Source = "Sprint 3",
                TimestampUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                Items =
                [
                    new PortfolioSnapshotItemEntity
                    {
                        ProjectNumber = "PRJ-100",
                        WorkPackage = "WP-1",
                        Progress = 0.4d,
                        TotalWeight = 10d,
                        LifecycleState = PoTool.Core.Domain.DeliveryTrends.Models.WorkPackageLifecycleState.Active
                    }
                ]
            },
            new PortfolioSnapshotEntity
            {
                SnapshotId = 2,
                ProductId = 2,
                Source = "Sprint 3",
                TimestampUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                Items =
                [
                    new PortfolioSnapshotItemEntity
                    {
                        ProjectNumber = "PRJ-200",
                        WorkPackage = "WP-9",
                        Progress = 0.6d,
                        TotalWeight = 8d,
                        LifecycleState = PoTool.Core.Domain.DeliveryTrends.Models.WorkPackageLifecycleState.Retired
                    }
                ]
            });

        await context.SaveChangesAsync();
    }
}
