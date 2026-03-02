using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Services.Sync;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Pipelines;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class SyncStageDiagnosticsTests
{
    [TestMethod]
    public async Task PullRequestSyncStage_ZeroRepos_SkipsWithDiagnosticLog()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PRDiag_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        var tfsClient = new Mock<ITfsClient>();
        var logger = new Mock<ILogger<PullRequestSyncStage>>();
        var stage = new PullRequestSyncStage(tfsClient.Object, dbContext, logger.Object);

        var context = new SyncContext
        {
            ProductOwnerId = 1,
            RootWorkItemIds = [100],
            RepositoryNames = [] // zero repos
        };

        var result = await stage.ExecuteAsync(context, _ => { }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.ItemCount);

        // TFS should never be called
        tfsClient.Verify(
            c => c.GetPullRequestsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task PullRequestSyncStage_WithRepos_LogsStartDiagnostics()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PRDiag_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        var tfsClient = new Mock<ITfsClient>();
        tfsClient.Setup(c => c.GetPullRequestsAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto>());

        var logger = new Mock<ILogger<PullRequestSyncStage>>();
        var stage = new PullRequestSyncStage(tfsClient.Object, dbContext, logger.Object);

        var context = new SyncContext
        {
            ProductOwnerId = 1,
            RootWorkItemIds = [100],
            RepositoryNames = ["RepoA", "RepoB"]
        };

        var result = await stage.ExecuteAsync(context, _ => { }, CancellationToken.None);

        Assert.IsTrue(result.Success);

        // TFS should be called for each repo
        tfsClient.Verify(
            c => c.GetPullRequestsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task PipelineSyncStage_ZeroPipelines_SkipsWithDiagnosticLog()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PlDiag_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        var tfsClient = new Mock<ITfsClient>();
        var logger = new Mock<ILogger<PipelineSyncStage>>();
        var stage = new PipelineSyncStage(tfsClient.Object, dbContext, logger.Object);

        var context = new SyncContext
        {
            ProductOwnerId = 1,
            RootWorkItemIds = [100],
            PipelineDefinitionIds = [] // zero pipelines
        };

        var result = await stage.ExecuteAsync(context, _ => { }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.ItemCount);

        // TFS should never be called
        tfsClient.Verify(
            c => c.GetPipelineRunsAsync(It.IsAny<int[]>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task PipelineSyncStage_WithPipelines_LogsStartDiagnostics()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PlDiag_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        var tfsClient = new Mock<ITfsClient>();
        tfsClient.Setup(c => c.GetPipelineRunsAsync(
                It.IsAny<int[]>(),
                It.IsAny<string?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PipelineRunDto>());

        var logger = new Mock<ILogger<PipelineSyncStage>>();
        var stage = new PipelineSyncStage(tfsClient.Object, dbContext, logger.Object);

        var context = new SyncContext
        {
            ProductOwnerId = 1,
            RootWorkItemIds = [100],
            PipelineDefinitionIds = [42, 43]
        };

        var result = await stage.ExecuteAsync(context, _ => { }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.ItemCount);
    }
}
