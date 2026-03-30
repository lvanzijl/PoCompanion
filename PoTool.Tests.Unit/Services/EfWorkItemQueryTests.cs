using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;

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
    public async Task GetByRootIdsAsync_ReturnsRootsAndDescendantsOnly()
    {
        _context.WorkItems.AddRange(
            CreateEntity(100, null, "Area\\One"),
            CreateEntity(101, 100, "Area\\One"),
            CreateEntity(102, 101, "Area\\One"),
            CreateEntity(200, null, "Area\\Two"));
        await _context.SaveChangesAsync();

        var result = await _query.GetByRootIdsAsync([100], CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { 100, 101, 102 }, result.Select(item => item.TfsId).ToArray());
    }

    [TestMethod]
    public async Task GetByAreaPathsAsync_MatchesHierarchicalAreaPrefixes()
    {
        _context.WorkItems.AddRange(
            CreateEntity(100, null, "Area\\One"),
            CreateEntity(101, 100, "Area\\One\\Child"),
            CreateEntity(200, null, "Area\\Two"));
        await _context.SaveChangesAsync();

        var result = await _query.GetByAreaPathsAsync(["Area\\One"], CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { 100, 101 }, result.Select(item => item.TfsId).ToArray());
    }

    private static WorkItemEntity CreateEntity(int tfsId, int? parentTfsId, string areaPath) =>
        new()
        {
            TfsId = tfsId,
            ParentTfsId = parentTfsId,
            Type = "Epic",
            Title = $"Item {tfsId}",
            AreaPath = areaPath,
            IterationPath = "Iteration",
            State = "New",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        };
}
