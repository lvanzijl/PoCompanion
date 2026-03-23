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
    public async Task ExecuteAsync_SkipsAlreadyCompleteBuildsWhenRefreshingCurrentRuns()
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
            .Setup(client => client.GetTestRunsByBuildIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TestRunDto { BuildId = 1001, ExternalId = 5001, TotalTests = 10, PassedTests = 9, NotApplicableTests = 1 },
                new TestRunDto { BuildId = 1001, ExternalId = 5002, TotalTests = 5, PassedTests = 5, NotApplicableTests = 0 }
            });
        tfsClient
            .Setup(client => client.GetCoverageByBuildIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CoverageDto { BuildId = 1001, CoveredLines = 90, TotalLines = 100 }
            });

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
        Assert.HasCount(2, testRuns);
        Assert.AreEqual(5001, testRuns[0].ExternalId);
        Assert.AreEqual(10, testRuns[0].TotalTests);
        Assert.AreEqual(9, testRuns[0].PassedTests);
        Assert.AreEqual(1, testRuns[0].NotApplicableTests);
        Assert.AreEqual(5002, testRuns[1].ExternalId);
        Assert.AreEqual(5, testRuns[1].TotalTests);
        Assert.AreEqual(5, testRuns[1].PassedTests);
        Assert.AreEqual(0, testRuns[1].NotApplicableTests);

        var coverageRows = await dbContext.Coverages
            .OrderBy(coverage => coverage.BuildId)
            .ToListAsync();
        Assert.HasCount(1, coverageRows);
        Assert.AreEqual(90, coverageRows[0].CoveredLines);
        Assert.AreEqual(100, coverageRows[0].TotalLines);

        tfsClient.Verify(
            client => client.GetTestRunsByBuildIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1001 })),
                It.IsAny<CancellationToken>()),
            Times.Once);
        tfsClient.Verify(
            client => client.GetCoverageByBuildIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1001 })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_RequeriesIncompleteBuildsMissingCoverage()
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
                new TestRunDto { BuildId = 1001, ExternalId = 5001, TotalTests = 10, PassedTests = 9, NotApplicableTests = 1 }
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
            .ReturnsAsync(new[]
            {
                new CoverageDto { BuildId = 1001, CoveredLines = 91, TotalLines = 100 }
            });

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

        var existingCoverage = await dbContext.Coverages.SingleAsync();
        dbContext.Coverages.Remove(existingCoverage);
        await dbContext.SaveChangesAsync();

        var secondResult = await stage.ExecuteAsync(syncContext, _ => { }, CancellationToken.None);
        Assert.IsTrue(secondResult.Success);

        var testRunsAfterSecondSync = await dbContext.TestRuns
            .OrderBy(testRun => testRun.ExternalId)
            .ToListAsync();
        Assert.HasCount(1, testRunsAfterSecondSync);
        Assert.AreEqual(5001, testRunsAfterSecondSync[0].ExternalId);
        Assert.AreEqual(12, testRunsAfterSecondSync[0].TotalTests);
        Assert.AreEqual(11, testRunsAfterSecondSync[0].PassedTests);
        Assert.AreEqual(1, testRunsAfterSecondSync[0].NotApplicableTests);

        var coverageRows = await dbContext.Coverages.ToListAsync();
        Assert.HasCount(1, coverageRows);
        Assert.AreEqual(91, coverageRows[0].CoveredLines);
        Assert.AreEqual(100, coverageRows[0].TotalLines);

        tfsClient.Verify(
            client => client.GetTestRunsByBuildIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1001 })),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        tfsClient.Verify(
            client => client.GetCoverageByBuildIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1001 })),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task ExecuteAsync_BackfillsIncompleteCachedBuildsOutsideCurrentRunBatch()
    {
        const int incompleteBuildId = 168570;
        const int completeBuildId = 168571;
        const int otherPipelineIncompleteBuildId = 268570;
        const int currentBuildId = 2001;

        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PipelineSyncStageBuildQuality_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        SeedPipelineDefinition(dbContext, productOwnerId: 1, pipelineDefinitionId: 42);
        dbContext.PipelineDefinitions.Add(new PipelineDefinitionEntity
        {
            Id = 2,
            ProductId = 1,
            RepositoryId = 1,
            PipelineDefinitionId = 43,
            RepoId = "repo-id-2",
            RepoName = "Repo",
            Name = "Other Pipeline",
            LastSyncedUtc = DateTimeOffset.UtcNow
        });

        var cachedIncompleteBuild = new CachedPipelineRunEntity
        {
            ProductOwnerId = 1,
            PipelineDefinitionId = 1,
            TfsRunId = incompleteBuildId,
            RunName = "Incomplete Build",
            State = "completed",
            Result = "Succeeded",
            CachedAt = DateTimeOffset.UtcNow
        };
        var cachedCompleteBuild = new CachedPipelineRunEntity
        {
            ProductOwnerId = 1,
            PipelineDefinitionId = 1,
            TfsRunId = completeBuildId,
            RunName = "Complete Build",
            State = "completed",
            Result = "Succeeded",
            CachedAt = DateTimeOffset.UtcNow
        };
        var otherPipelineIncompleteBuild = new CachedPipelineRunEntity
        {
            ProductOwnerId = 1,
            PipelineDefinitionId = 2,
            TfsRunId = otherPipelineIncompleteBuildId,
            RunName = "Other Pipeline Incomplete Build",
            State = "completed",
            Result = "Succeeded",
            CachedAt = DateTimeOffset.UtcNow
        };

        dbContext.CachedPipelineRuns.AddRange(cachedIncompleteBuild, cachedCompleteBuild, otherPipelineIncompleteBuild);
        await dbContext.SaveChangesAsync();

        dbContext.TestRuns.Add(new TestRunEntity
        {
            BuildId = cachedCompleteBuild.Id,
            ExternalId = 9001,
            TotalTests = 7,
            PassedTests = 7,
            NotApplicableTests = 0,
            CachedAt = DateTimeOffset.UtcNow
        });
        dbContext.Coverages.Add(new CoverageEntity
        {
            BuildId = cachedCompleteBuild.Id,
            CoveredLines = 70,
            TotalLines = 80,
            CachedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var tfsClient = new Mock<ITfsClient>();
        tfsClient
            .Setup(client => client.GetPipelineRunsAsync(
                It.IsAny<int[]>(),
                It.IsAny<string?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreatePipelineRun(runId: currentBuildId, pipelineId: 42)]);
        IEnumerable<int>? requestedTestRunBuildIds = null;
        IEnumerable<int>? requestedCoverageBuildIds = null;

        tfsClient
            .Setup(client => client.GetTestRunsByBuildIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<int>, CancellationToken>((ids, _) => requestedTestRunBuildIds = ids.ToArray())
            .ReturnsAsync(new[]
            {
                new TestRunDto { BuildId = incompleteBuildId, ExternalId = 8001, TotalTests = 4, PassedTests = 4, NotApplicableTests = 0 },
                new TestRunDto { BuildId = currentBuildId, ExternalId = 8002, TotalTests = 6, PassedTests = 5, NotApplicableTests = 1 }
            });
        tfsClient
            .Setup(client => client.GetCoverageByBuildIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<int>, CancellationToken>((ids, _) => requestedCoverageBuildIds = ids.ToArray())
            .ReturnsAsync(new[]
            {
                new CoverageDto { BuildId = incompleteBuildId, CoveredLines = 40, TotalLines = 50 },
                new CoverageDto { BuildId = currentBuildId, CoveredLines = 90, TotalLines = 100 }
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

        var expectedRequestedBuildIds = new[] { incompleteBuildId, currentBuildId };
        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.HasWarnings);
        CollectionAssert.AreEquivalent(expectedRequestedBuildIds, requestedTestRunBuildIds?.ToArray() ?? []);
        CollectionAssert.AreEquivalent(expectedRequestedBuildIds, requestedCoverageBuildIds?.ToArray() ?? []);

        var builds = await dbContext.CachedPipelineRuns
            .OrderBy(build => build.TfsRunId)
            .ToListAsync();
        var currentBuild = builds.Single(build => build.TfsRunId == currentBuildId);
        var refreshedBackfillBuild = builds.Single(build => build.TfsRunId == incompleteBuildId);
        var untouchedCompleteBuild = builds.Single(build => build.TfsRunId == completeBuildId);

        var testRuns = await dbContext.TestRuns
            .OrderBy(testRun => testRun.ExternalId)
            .ToListAsync();
        Assert.HasCount(3, testRuns);

        var coverageRows = await dbContext.Coverages
            .OrderBy(coverage => coverage.BuildId)
            .ToListAsync();
        Assert.HasCount(3, coverageRows);
        var expectedAffectedBuildIds = new[] { refreshedBackfillBuild.Id, currentBuild.Id, untouchedCompleteBuild.Id };
        CollectionAssert.AreEquivalent(
            expectedAffectedBuildIds,
            testRuns.Select(testRun => testRun.BuildId).ToArray());
        CollectionAssert.AreEquivalent(
            expectedAffectedBuildIds,
            coverageRows.Select(coverage => coverage.BuildId).ToArray());

        AssertLogged(logger, "BUILDQUALITY_CHILD_INGEST_SELECTION:");
        AssertLogged(logger, "originalScopeBuildCount=3");
        AssertLogged(logger, "completeBuildCount=1");
        AssertLogged(logger, "incompleteBuildCount=2");
        AssertLogged(logger, "cappedBuildCount=2");
    }

    [TestMethod]
    public async Task ExecuteAsync_CapsMostRecentIncompleteBuildsOnly()
    {
        const int pipelineDefinitionId = 42;
        var maxBuildQualityBuildBatchSize = GetMaxBuildQualityBuildBatchSize();

        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PipelineSyncStageBuildQuality_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);
        SeedPipelineDefinition(dbContext, productOwnerId: 1, pipelineDefinitionId: pipelineDefinitionId);

        var baseFinishedDateUtc = new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc);
        var incompleteBuildIds = Enumerable.Range(3000, maxBuildQualityBuildBatchSize + 5).ToArray();
        foreach (var (runId, index) in incompleteBuildIds.Select((runId, index) => (runId, index)))
        {
            dbContext.CachedPipelineRuns.Add(new CachedPipelineRunEntity
            {
                ProductOwnerId = 1,
                PipelineDefinitionId = 1,
                TfsRunId = runId,
                RunName = $"Build {runId}",
                State = "completed",
                Result = "Succeeded",
                FinishedDateUtc = baseFinishedDateUtc.AddMinutes(index),
                CachedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();

        var completeBuild = new CachedPipelineRunEntity
        {
            ProductOwnerId = 1,
            PipelineDefinitionId = 1,
            TfsRunId = 9999,
            RunName = "Complete Build",
            State = "completed",
            Result = "Succeeded",
            FinishedDateUtc = baseFinishedDateUtc.AddMinutes(500),
            CachedAt = DateTimeOffset.UtcNow
        };
        dbContext.CachedPipelineRuns.Add(completeBuild);
        await dbContext.SaveChangesAsync();

        dbContext.TestRuns.Add(new TestRunEntity
        {
            BuildId = completeBuild.Id,
            ExternalId = 7001,
            TotalTests = 1,
            PassedTests = 1,
            NotApplicableTests = 0,
            CachedAt = DateTimeOffset.UtcNow
        });
        dbContext.Coverages.Add(new CoverageEntity
        {
            BuildId = completeBuild.Id,
            CoveredLines = 10,
            TotalLines = 10,
            CachedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        IEnumerable<int>? requestedTestRunBuildIds = null;
        IEnumerable<int>? requestedCoverageBuildIds = null;

        var tfsClient = new Mock<ITfsClient>();
        tfsClient
            .Setup(client => client.GetPipelineRunsAsync(
                It.IsAny<int[]>(),
                It.IsAny<string?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PipelineRunDto>());
        tfsClient
            .Setup(client => client.GetTestRunsByBuildIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<int>, CancellationToken>((ids, _) => requestedTestRunBuildIds = ids.ToArray())
            .ReturnsAsync(Array.Empty<TestRunDto>());
        tfsClient
            .Setup(client => client.GetCoverageByBuildIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<int>, CancellationToken>((ids, _) => requestedCoverageBuildIds = ids.ToArray())
            .ReturnsAsync(Array.Empty<CoverageDto>());

        var logger = new Mock<ILogger<PipelineSyncStage>>();
        var stage = new PipelineSyncStage(tfsClient.Object, dbContext, logger.Object);

        var result = await stage.ExecuteAsync(
            new SyncContext
            {
                ProductOwnerId = 1,
                RootWorkItemIds = [100],
                PipelineDefinitionIds = [pipelineDefinitionId]
            },
            _ => { },
            CancellationToken.None);

        Assert.IsTrue(result.Success);

        var expectedBuildIds = incompleteBuildIds
            .OrderByDescending(id => baseFinishedDateUtc.AddMinutes(Array.IndexOf(incompleteBuildIds, id)))
            .ThenByDescending(id => id)
            .Take(maxBuildQualityBuildBatchSize)
            .ToArray();

        Assert.IsNotNull(requestedTestRunBuildIds);
        Assert.IsNotNull(requestedCoverageBuildIds);
        CollectionAssert.AreEquivalent(
            expectedBuildIds,
            requestedTestRunBuildIds.ToArray());
        CollectionAssert.AreEquivalent(
            expectedBuildIds,
            requestedCoverageBuildIds.ToArray());
        Assert.AreEqual(
            expectedBuildIds.Length,
            requestedTestRunBuildIds.Distinct().Count());
        Assert.IsFalse(requestedTestRunBuildIds.Contains(completeBuild.TfsRunId));
        AssertLogged(logger, $"cappedBuildCount={maxBuildQualityBuildBatchSize}");
    }

    [TestMethod]
    public async Task ExecuteAsync_LogsBuildQualityTimingAndCountSummaries()
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
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TestRunDto { BuildId = 1001, ExternalId = 5001, TotalTests = 10, PassedTests = 9, NotApplicableTests = 1 },
                new TestRunDto { BuildId = 1002, ExternalId = 5002, TotalTests = 4, PassedTests = 4, NotApplicableTests = 0 }
            });
        tfsClient
            .Setup(client => client.GetCoverageByBuildIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CoverageDto { BuildId = 1001, CoveredLines = 90, TotalLines = 100 },
                new CoverageDto { BuildId = 1002, CoveredLines = 20, TotalLines = 50 }
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
        AssertLogged(logger, "BUILDQUALITY_TESTRUN_RETRIEVAL_SUMMARY:");
        AssertLogged(logger, "BUILDQUALITY_COVERAGE_RETRIEVAL_SUMMARY:");
        AssertLogged(logger, "BUILDQUALITY_TESTRUN_PERSISTENCE_SUMMARY:");
        AssertLogged(logger, "BUILDQUALITY_COVERAGE_PERSISTENCE_SUMMARY:");
        AssertLogged(logger, "BUILDQUALITY_CHILD_INGEST_SELECTION:");
        AssertLogged(logger, "BUILDQUALITY_CHILD_INGEST_SUMMARY:");
        AssertLogged(logger, "originalScopeBuildCount=2");
        AssertLogged(logger, "completeBuildCount=0");
        AssertLogged(logger, "incompleteBuildCount=2");
        AssertLogged(logger, "cappedBuildCount=2");
        AssertLogged(logger, "selectedBuildIds=[");
        AssertLogged(logger, "requestedBuildCount=2");
        AssertLogged(logger, "persistedRowCount=2");
    }

    private static int GetMaxBuildQualityBuildBatchSize()
    {
        var field = typeof(PipelineSyncStage).GetField(
            "MaxBuildQualityBuildBatchSize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.IsNotNull(field);
        return (int?)field.GetValue(null) ?? throw new AssertFailedException("Missing MaxBuildQualityBuildBatchSize constant.");
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

    private static void AssertLogged(Mock<ILogger<PipelineSyncStage>> logger, string messageFragment)
    {
        logger.Verify(
            instance => instance.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(messageFragment, StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
