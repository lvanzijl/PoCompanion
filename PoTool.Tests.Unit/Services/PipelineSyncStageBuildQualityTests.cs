using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services.Sync;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class PipelineSyncStageBuildQualityTests
{
    [TestMethod]
    public async Task ExecuteAsync_PersistsRawBuildQualityFactsLinkedByBuildId()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PipelineSyncStageBuildQuality_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        SeedPipelineDefinition(dbContext, productOwnerId: 1, pipelineDefinitionId: 42);

        var tfsClient = new Mock<ITfsClient>();
        tfsClient
            .Setup(client => client.GetPipelineRunsAsync(
                It.IsAny<int[]>(),
                It.IsAny<string?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreatePipelineRun(runId: 1001, pipelineId: 42),
                CreatePipelineRun(runId: 1002, pipelineId: 42)
            });
        tfsClient
            .Setup(client => client.GetTestRunsByBuildIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { 1001, 1002 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TestRunDto { BuildId = 1001, ExternalId = 5001, TotalTests = 10, PassedTests = 9, NotApplicableTests = 1, Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5) },
                new TestRunDto { BuildId = 1001, ExternalId = 5002, TotalTests = 4, PassedTests = 4, NotApplicableTests = 0 },
                new TestRunDto { BuildId = 9999, ExternalId = 5999, TotalTests = 1, PassedTests = 1, NotApplicableTests = 0 }
            });
        tfsClient
            .Setup(client => client.GetCoverageByBuildIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { 1001, 1002 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CoverageDto { BuildId = 1001, CoveredLines = 90, TotalLines = 100 },
                new CoverageDto { BuildId = 1002, CoveredLines = 0, TotalLines = 0 },
                new CoverageDto { BuildId = 9999, CoveredLines = 1, TotalLines = 2 }
            });

        var logger = new Mock<ILogger<PipelineSyncStage>>();
        var stage = new PipelineSyncStage(tfsClient.Object, dbContext, logger.Object);

        var result = await stage.ExecuteAsync(
            new SyncContext
            {
                ProductOwnerId = 1,
                RootWorkItemIds = [100],
                PipelineDefinitionIds = [42]
            },
            _ => { },
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.HasWarnings);

        var builds = await dbContext.CachedPipelineRuns
            .OrderBy(build => build.TfsRunId)
            .ToListAsync();
        Assert.HasCount(2, builds);

        var build1001 = builds.Single(build => build.TfsRunId == 1001);
        var build1002 = builds.Single(build => build.TfsRunId == 1002);

        var testRuns = await dbContext.TestRuns
            .OrderBy(testRun => testRun.ExternalId)
            .ToListAsync();
        Assert.HasCount(2, testRuns);
        CollectionAssert.AreEqual(new[] { build1001.Id, build1001.Id }, testRuns.Select(testRun => testRun.BuildId).ToArray());
        Assert.AreEqual(10, testRuns[0].TotalTests);
        Assert.AreEqual(9, testRuns[0].PassedTests);
        Assert.AreEqual(1, testRuns[0].NotApplicableTests);
        Assert.AreEqual(4, testRuns[1].TotalTests);
        Assert.AreEqual(4, testRuns[1].PassedTests);
        Assert.AreEqual(0, testRuns[1].NotApplicableTests);

        var coverageRows = await dbContext.Coverages
            .OrderBy(coverage => coverage.BuildId)
            .ToListAsync();
        Assert.HasCount(2, coverageRows);
        Assert.AreEqual(build1001.Id, coverageRows[0].BuildId);
        Assert.AreEqual(90, coverageRows[0].CoveredLines);
        Assert.AreEqual(100, coverageRows[0].TotalLines);
        Assert.AreEqual(build1002.Id, coverageRows[1].BuildId);
        Assert.AreEqual(0, coverageRows[1].CoveredLines);
        Assert.AreEqual(0, coverageRows[1].TotalLines);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplacesMissingChildRowsAndUpsertsExistingTestRuns()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PipelineSyncStageBuildQuality_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        SeedPipelineDefinition(dbContext, productOwnerId: 1, pipelineDefinitionId: 42);

        var tfsClient = new Mock<ITfsClient>();
        tfsClient
            .SetupSequence(client => client.GetPipelineRunsAsync(
                It.IsAny<int[]>(),
                It.IsAny<string?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreatePipelineRun(runId: 1001, pipelineId: 42)])
            .ReturnsAsync([CreatePipelineRun(runId: 1001, pipelineId: 42)]);
        tfsClient
            .SetupSequence(client => client.GetTestRunsByBuildIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TestRunDto { BuildId = 1001, ExternalId = 5001, TotalTests = 10, PassedTests = 9, NotApplicableTests = 1 },
                new TestRunDto { BuildId = 1001, ExternalId = 5002, TotalTests = 5, PassedTests = 5, NotApplicableTests = 0 }
            })
            .ReturnsAsync(new[]
            {
                new TestRunDto { BuildId = 1001, ExternalId = 5001, TotalTests = 12, PassedTests = 11, NotApplicableTests = 1 }
            });
        tfsClient
            .SetupSequence(client => client.GetCoverageByBuildIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CoverageDto { BuildId = 1001, CoveredLines = 90, TotalLines = 100 }
            })
            .ReturnsAsync(Array.Empty<CoverageDto>());

        var logger = new Mock<ILogger<PipelineSyncStage>>();
        var stage = new PipelineSyncStage(tfsClient.Object, dbContext, logger.Object);
        var syncContext = new SyncContext
        {
            ProductOwnerId = 1,
            RootWorkItemIds = [100],
            PipelineDefinitionIds = [42]
        };

        var firstResult = await stage.ExecuteAsync(syncContext, _ => { }, CancellationToken.None);
        Assert.IsTrue(firstResult.Success);

        var secondResult = await stage.ExecuteAsync(syncContext, _ => { }, CancellationToken.None);
        Assert.IsTrue(secondResult.Success);

        var testRuns = await dbContext.TestRuns
            .OrderBy(testRun => testRun.ExternalId)
            .ToListAsync();
        Assert.HasCount(1, testRuns);
        Assert.AreEqual(5001, testRuns[0].ExternalId);
        Assert.AreEqual(12, testRuns[0].TotalTests);
        Assert.AreEqual(11, testRuns[0].PassedTests);
        Assert.AreEqual(1, testRuns[0].NotApplicableTests);

        var coverageRows = await dbContext.Coverages.ToListAsync();
        Assert.HasCount(0, coverageRows);
    }

    private static void SeedPipelineDefinition(PoToolDbContext dbContext, int productOwnerId, int pipelineDefinitionId)
    {
        dbContext.Profiles.Add(new ProfileEntity
        {
            Id = productOwnerId,
            Name = $"Owner {productOwnerId}",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        });

        dbContext.Products.Add(new ProductEntity
        {
            Id = 1,
            Name = "Product",
            ProductOwnerId = productOwnerId,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        });

        dbContext.Repositories.Add(new RepositoryEntity
        {
            Id = 1,
            ProductId = 1,
            Name = "Repo",
            CreatedAt = DateTimeOffset.UtcNow
        });

        dbContext.PipelineDefinitions.Add(new PipelineDefinitionEntity
        {
            Id = 1,
            ProductId = 1,
            RepositoryId = 1,
            PipelineDefinitionId = pipelineDefinitionId,
            RepoId = "repo-id",
            RepoName = "Repo",
            Name = "Pipeline",
            LastSyncedUtc = DateTimeOffset.UtcNow
        });

        dbContext.SaveChanges();
    }

    private static PipelineRunDto CreatePipelineRun(int runId, int pipelineId)
    {
        var finishTime = DateTimeOffset.UtcNow;
        var startTime = finishTime.AddMinutes(-10);

        return new PipelineRunDto(
            RunId: runId,
            PipelineId: pipelineId,
            PipelineName: "Pipeline",
            StartTime: startTime,
            FinishTime: finishTime,
            Duration: finishTime - startTime,
            Result: PipelineRunResult.Succeeded,
            Trigger: PipelineRunTrigger.ContinuousIntegration,
            TriggerInfo: "individualCI",
            Branch: "refs/heads/main",
            RequestedFor: "Build User",
            RetrievedAt: DateTimeOffset.UtcNow);
    }
}
