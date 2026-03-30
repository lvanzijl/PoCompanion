using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class EfWorkItemQueryTests
{
    private SqliteConnection _connection = null!;
    private PoToolDbContext _context = null!;
    private EfWorkItemQuery _query = null!;

    [TestInitialize]
    public async Task SetupAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new PoToolDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _query = new EfWorkItemQuery(_context);
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [TestMethod]
    public async Task GetWorkItemsForListingAsync_WithProductIds_ReturnsHierarchyScopedItems()
    {
        _context.Products.Add(new ProductEntity { Id = 1, Name = "Product 1" });
        _context.ProductBacklogRoots.Add(new ProductBacklogRootEntity { ProductId = 1, WorkItemTfsId = 100 });
        _context.WorkItems.AddRange(
            CreateEntity(100, null, "Area\\One", WorkItemType.Epic),
            CreateEntity(101, 100, "Area\\One", WorkItemType.Feature),
            CreateEntity(200, null, "Area\\Two", WorkItemType.Epic));
        await _context.SaveChangesAsync();

        var result = await _query.GetWorkItemsForListingAsync([1], fallbackAreaPaths: null, CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { 100, 101 }, result.Select(item => item.TfsId).ToArray());
    }

    [TestMethod]
    public async Task GetGoalsForListingAsync_FiltersGoalsInsideConfiguredRootScope()
    {
        _context.Products.Add(new ProductEntity { Id = 1, Name = "Product 1" });
        _context.ProductBacklogRoots.Add(new ProductBacklogRootEntity { ProductId = 1, WorkItemTfsId = 100 });
        _context.WorkItems.AddRange(
            CreateEntity(100, null, "Area\\One", WorkItemType.Goal),
            CreateEntity(101, 100, "Area\\One", WorkItemType.Epic),
            CreateEntity(102, null, "Area\\One", WorkItemType.Goal));
        await _context.SaveChangesAsync();

        var result = await _query.GetGoalsForListingAsync(fallbackAreaPaths: null, CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { 100 }, result.Select(item => item.TfsId).ToArray());
    }

    [TestMethod]
    public async Task GetDependencyGraphSourceAsync_ReturnsScopedAndRelevantWorkItems()
    {
        _context.WorkItems.AddRange(
            CreateEntity(100, null, "Area\\One", WorkItemType.Epic),
            CreateEntity(101, null, "Area\\One\\Child", WorkItemType.Feature),
            CreateEntity(200, null, "Area\\Two", WorkItemType.Task));
        await _context.SaveChangesAsync();

        var result = await _query.GetDependencyGraphSourceAsync(
            areaPathFilter: "Area\\One",
            workItemIds: null,
            workItemTypes: [WorkItemType.Epic, WorkItemType.Feature],
            CancellationToken.None);

        Assert.HasCount(3, result.ScopedWorkItems);
        CollectionAssert.AreEquivalent(new[] { 100, 101 }, result.RelevantWorkItems.Select(item => item.TfsId).ToArray());
    }

    [TestMethod]
    public async Task GetValidationImpactSourceAsync_BuildsChildrenLookupForFilteredItems()
    {
        _context.WorkItems.AddRange(
            CreateEntity(100, null, "Area\\One", WorkItemType.Goal, iterationPath: "Sprint A"),
            CreateEntity(101, 100, "Area\\One", WorkItemType.Epic, iterationPath: "Sprint A"),
            CreateEntity(200, null, "Area\\Two", WorkItemType.Goal));
        await _context.SaveChangesAsync();

        var result = await _query.GetValidationImpactSourceAsync("Area\\One", "Sprint A", CancellationToken.None);

        Assert.HasCount(2, result.WorkItems);
        Assert.IsTrue(result.ChildrenByParentId.ContainsKey(100));
        CollectionAssert.AreEquivalent(new[] { 101 }, result.ChildrenByParentId[100].ToArray());
    }

    [TestMethod]
    public async Task GetProductBacklogAnalyticsSourceAsync_ReturnsNullForUnknownProduct()
    {
        var result = await _query.GetProductBacklogAnalyticsSourceAsync(99, CancellationToken.None);

        Assert.IsNull(result);
    }

    private static WorkItemEntity CreateEntity(
        int tfsId,
        int? parentTfsId,
        string areaPath,
        string type,
        string iterationPath = "Iteration") =>
        new()
        {
            TfsId = tfsId,
            ParentTfsId = parentTfsId,
            Type = type,
            Title = $"Item {tfsId}",
            AreaPath = areaPath,
            IterationPath = iterationPath,
            State = "New",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        };
}
