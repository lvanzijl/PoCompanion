using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class CachedReadProviderDiagnosticsTests
{
    [TestMethod]
    public async Task CachedPullRequestReadProvider_EmptyDb_ReturnsEmptyAndLogsDiagnostics()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var dbContext = new PoToolDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var logger = new Mock<ILogger<CachedPullRequestReadProvider>>();
        var provider = new CachedPullRequestReadProvider(dbContext, logger.Object);

        var result = await provider.GetAllAsync();

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Any());
    }

    [TestMethod]
    public async Task CachedPullRequestReadProvider_WithProductFilter_EmptyDb_ReturnsEmpty()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var dbContext = new PoToolDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var logger = new Mock<ILogger<CachedPullRequestReadProvider>>();
        var provider = new CachedPullRequestReadProvider(dbContext, logger.Object);

        var result = await provider.GetByProductIdsAsync(new List<int> { 1 });

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Any());
    }

    [TestMethod]
    public async Task CachedPipelineReadProvider_EmptyDb_ReturnsEmptyRunsList()
    {
        await using var connection = await OpenConnectionAsync();
        var options = CreateOptions(connection);
        await using var dbContext = new PoToolDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var logger = new Mock<ILogger<CachedPipelineReadProvider>>();
        var provider = new CachedPipelineReadProvider(dbContext, logger.Object);

        var result = await provider.GetRunsForPipelinesAsync(
            new[] { 1, 2 },
            branchName: "refs/heads/main",
            minStartTime: DateTimeOffset.UtcNow.AddMonths(-6));

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Any());
    }

    private static DbContextOptions<PoToolDbContext> CreateOptions(SqliteConnection connection)
        => new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite(connection)
            .Options;

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }
}
