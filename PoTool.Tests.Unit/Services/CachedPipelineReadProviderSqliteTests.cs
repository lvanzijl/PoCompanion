using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class CachedPipelineReadProviderSqliteTests
{
    private SqliteConnection _connection = null!;
    private PoToolDbContext _dbContext = null!;
    private CachedPipelineReadProvider _provider = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new PoToolDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _provider = new CachedPipelineReadProvider(
            _dbContext,
            Mock.Of<ILogger<CachedPipelineReadProvider>>());
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [TestMethod]
    public async Task GetRunsForPipelinesAsync_TopPerPipelineDoesNotDropQuietPipelineRuns()
    {
        await SeedDefinitionAsync(101, "Busy Pipeline");
        await SeedDefinitionAsync(202, "Quiet Pipeline");

        for (var index = 0; index < 5; index++)
        {
            await SeedRunAsync(
                id: index + 1,
                pipelineDefinitionId: 101,
                tfsRunId: index + 1,
                createdUtc: new DateTime(2026, 3, 20 - index, 9, 0, 0, DateTimeKind.Utc),
                branch: "refs/heads/main");
        }

        await SeedRunAsync(
            id: 100,
            pipelineDefinitionId: 202,
            tfsRunId: 100,
            createdUtc: new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc),
            branch: "refs/heads/main");

        var runs = (await _provider.GetRunsForPipelinesAsync(
                [101, 202],
                branchName: null,
                minStartTime: null,
                top: 2,
                cancellationToken: CancellationToken.None))
            .ToList();

        Assert.HasCount(3, runs, "Expected top 2 busy-pipeline runs plus the quiet pipeline run.");
        Assert.AreEqual(2, runs.Count(run => run.PipelineId == 101));
        Assert.AreEqual(1, runs.Count(run => run.PipelineId == 202));
        Assert.IsTrue(runs.Any(run => run.PipelineId == 202 && run.RunId == 100));
    }

    [TestMethod]
    public async Task GetRunsForPipelinesAsync_AppliesBranchFilterBeforePerPipelineLimit()
    {
        await SeedDefinitionAsync(101, "Release Pipeline");
        await SeedDefinitionAsync(202, "Secondary Release Pipeline");

        await SeedRunAsync(1, 101, 1, new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc), "refs/heads/main");
        await SeedRunAsync(2, 101, 2, new DateTime(2026, 3, 19, 9, 0, 0, DateTimeKind.Utc), "refs/heads/release");
        await SeedRunAsync(3, 202, 3, new DateTime(2026, 3, 18, 9, 0, 0, DateTimeKind.Utc), "refs/heads/release");

        var runs = (await _provider.GetRunsForPipelinesAsync(
                [101, 202],
                branchName: "refs/heads/release",
                minStartTime: null,
                top: 1,
                cancellationToken: CancellationToken.None))
            .ToList();

        Assert.HasCount(2, runs);
        Assert.IsTrue(runs.All(run => run.Branch == "refs/heads/release"));
        CollectionAssert.AreEquivalent(new[] { 101, 202 }, runs.Select(run => run.PipelineId).ToArray());
    }

    private async Task SeedDefinitionAsync(int pipelineDefinitionId, string name)
    {
        if (!await _dbContext.Profiles.AnyAsync(profile => profile.Id == 1))
        {
            _dbContext.Profiles.Add(new ProfileEntity
            {
                Id = 1,
                Name = "PO 1"
            });
        }

        if (!await _dbContext.Products.AnyAsync(product => product.Id == 1))
        {
            _dbContext.Products.Add(new ProductEntity
            {
                Id = 1,
                Name = "Product 1",
                ProductOwnerId = 1
            });
        }

        if (!await _dbContext.Repositories.AnyAsync(repository => repository.Id == pipelineDefinitionId))
        {
            _dbContext.Repositories.Add(new RepositoryEntity
            {
                Id = pipelineDefinitionId,
                ProductId = 1,
                Name = $"Repo-{pipelineDefinitionId}",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        _dbContext.PipelineDefinitions.Add(new PipelineDefinitionEntity
        {
            Id = pipelineDefinitionId,
            PipelineDefinitionId = pipelineDefinitionId,
            ProductId = 1,
            RepositoryId = pipelineDefinitionId,
            RepoId = $"repo-{pipelineDefinitionId}",
            RepoName = $"Repo-{pipelineDefinitionId}",
            Name = name,
            LastSyncedUtc = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedRunAsync(int id, int pipelineDefinitionId, int tfsRunId, DateTime createdUtc, string branch)
    {
        var createdOffset = new DateTimeOffset(createdUtc, TimeSpan.Zero);
        var finishedOffset = createdOffset.AddMinutes(15);

        _dbContext.CachedPipelineRuns.Add(new CachedPipelineRunEntity
        {
            Id = id,
            ProductOwnerId = 1,
            PipelineDefinitionId = pipelineDefinitionId,
            TfsRunId = tfsRunId,
            Result = "succeeded",
            CreatedDate = createdOffset,
            CreatedDateUtc = createdUtc,
            FinishedDate = finishedOffset,
            FinishedDateUtc = finishedOffset.UtcDateTime,
            SourceBranch = branch,
            CachedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync();
    }
}
