using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Filters;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class DeliveryFilterResolutionServiceTests
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
        context.Sprints.AddRange(
            new SprintEntity
            {
                Id = 42,
                Name = "Sprint 42",
                TeamId = 3,
                StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
            },
            new SprintEntity
            {
                Id = 43,
                Name = "Sprint 43",
                TeamId = 3,
                StartDateUtc = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDateUtc = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc)
            });
        await context.SaveChangesAsync();

        var service = new DeliveryFilterResolutionService(
            context,
            NullLogger<DeliveryFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new DeliveryFilterBoundaryRequest(
                ProductOwnerId: 7,
                SprintIds: [43, 42]),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 100, 200 }, resolution.EffectiveFilter.Context.ProductIds.Values.ToArray());
        Assert.AreEqual(FilterTimeSelectionMode.MultiSprint, resolution.EffectiveFilter.Context.Time.Mode);
        CollectionAssert.AreEqual(new[] { 42, 43 }, resolution.EffectiveFilter.SprintIds.ToArray());
        Assert.AreEqual(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), resolution.EffectiveFilter.RangeStartUtc);
        Assert.AreEqual(new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero), resolution.EffectiveFilter.RangeEndUtc);
        CollectionAssert.AreEqual(Array.Empty<string>(), resolution.Validation.InvalidFields.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_OutOfScopeProductSelectionFallsBackToOwnerScopeAndReportsValidation()
    {
        await using var context = CreateContext();
        PersistenceTestGraph.EnsureProject(context);
        PersistenceTestGraph.EnsureTeam(context, 3);
        context.Products.AddRange(
            PersistenceTestGraph.CreateProduct(100, "Product 100", 7),
            PersistenceTestGraph.CreateProduct(200, "Product 200", 7),
            PersistenceTestGraph.CreateProduct(300, "Product 300", 9));
        context.Sprints.Add(new SprintEntity
        {
            Id = 42,
            Name = "Sprint 42",
            TeamId = 3,
            StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDateUtc = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var service = new DeliveryFilterResolutionService(
            context,
            NullLogger<DeliveryFilterResolutionService>.Instance);

        var resolution = await service.ResolveAsync(
            new DeliveryFilterBoundaryRequest(
                ProductOwnerId: 7,
                ProductIds: [300],
                SprintIds: [42]),
            "TestBoundary",
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 100, 200 }, resolution.EffectiveFilter.Context.ProductIds.Values.ToArray());
        CollectionAssert.AreEqual(new[] { nameof(PoTool.Core.Delivery.Filters.DeliveryFilterContext.ProductIds) }, resolution.Validation.InvalidFields.ToArray());
        Assert.IsTrue(resolution.Validation.Messages.Any(message => message.Field == nameof(PoTool.Core.Delivery.Filters.DeliveryFilterContext.ProductIds)));
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"DeliveryFilterResolutionServiceTests_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
