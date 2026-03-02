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
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PRProviderDiag_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        var logger = new Mock<ILogger<CachedPullRequestReadProvider>>();
        var provider = new CachedPullRequestReadProvider(dbContext, logger.Object);

        var result = await provider.GetAllAsync();

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Any());
    }

    [TestMethod]
    public async Task CachedPullRequestReadProvider_WithProductFilter_EmptyDb_ReturnsEmpty()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PRProviderDiag_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        var logger = new Mock<ILogger<CachedPullRequestReadProvider>>();
        var provider = new CachedPullRequestReadProvider(dbContext, logger.Object);

        var result = await provider.GetByProductIdsAsync(new List<int> { 1 });

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Any());
    }

    [TestMethod]
    public async Task CachedPipelineReadProvider_EmptyDb_ReturnsEmptyRunsList()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PlProviderDiag_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        var logger = new Mock<ILogger<CachedPipelineReadProvider>>();
        var provider = new CachedPipelineReadProvider(dbContext, logger.Object);

        var result = await provider.GetRunsForPipelinesAsync(
            new[] { 1, 2 },
            branchName: "refs/heads/main",
            minStartTime: DateTimeOffset.UtcNow.AddMonths(-6));

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Any());
    }
}
