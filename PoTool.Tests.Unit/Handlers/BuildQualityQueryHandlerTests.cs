using Microsoft.EntityFrameworkCore;
using Moq;
using PoTool.Api.Handlers.BuildQuality;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services.BuildQuality;
using PoTool.Core.BuildQuality.Queries;
using PoTool.Shared.BuildQuality;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class BuildQualityQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private BuildQualityScopeLoader _scopeLoader = null!;
    private Mock<IBuildQualityProvider> _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"BuildQualityQueryHandlers_{Guid.NewGuid()}")
            .Options;

        _context = new PoToolDbContext(options);
        _scopeLoader = new BuildQualityScopeLoader(_context);
        _provider = new Mock<IBuildQualityProvider>(MockBehavior.Strict);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    public async Task RollingWindowHandler_SelectsOnlyInScopeDefaultBranchFacts_AndUsesProviderResults()
    {
        await SeedProductOwnerScopeAsync();

        var expectedResult = CreateExpectedResult();
        var capturedCalls = CaptureProviderCalls(expectedResult);
        var handler = new GetBuildQualityRollingWindowQueryHandler(_scopeLoader, _provider.Object);

        var result = await handler.Handle(
            new GetBuildQualityRollingWindowQuery(
                ProductOwnerId: 1,
                WindowStartUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                WindowEndUtc: new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        Assert.AreSame(expectedResult, result.Summary);
        Assert.HasCount(2, result.Products);
        Assert.AreSame(expectedResult, result.Products[0].Result);
        Assert.HasCount(3, capturedCalls);
        CollectionAssert.AreEqual(new[] { 1001, 2001 }, capturedCalls[0].Builds.Select(build => build.BuildId).OrderBy(id => id).ToArray());
        CollectionAssert.AreEqual(new[] { 1001, 2001 }, capturedCalls[0].TestRuns.Select(testRun => testRun.BuildId).Distinct().OrderBy(id => id).ToArray());
        CollectionAssert.AreEqual(new[] { 1001, 2001 }, capturedCalls[0].Coverages.Select(coverage => coverage.BuildId).Distinct().OrderBy(id => id).ToArray());
    }

    [TestMethod]
    public async Task SprintHandler_UsesSprintWindowSelection_AndDelegatesToProvider()
    {
        await SeedProductOwnerScopeAsync();

        var expectedResult = CreateExpectedResult();
        var capturedCalls = CaptureProviderCalls(expectedResult);
        var handler = new GetBuildQualitySprintQueryHandler(_context, _scopeLoader, _provider.Object);

        var result = await handler.Handle(
            new GetBuildQualitySprintQuery(ProductOwnerId: 1, SprintId: 77),
            CancellationToken.None);

        Assert.AreEqual(77, result.SprintId);
        Assert.AreEqual("Sprint 77", result.SprintName);
        Assert.AreSame(expectedResult, result.Summary);
        Assert.HasCount(3, capturedCalls);
        CollectionAssert.AreEqual(new[] { 1001, 2001 }, capturedCalls[0].Builds.Select(build => build.BuildId).OrderBy(id => id).ToArray());
    }

    [TestMethod]
    public async Task PipelineDetailHandler_FiltersToRequestedRepository_AndDoesNotRecomputeProviderOutput()
    {
        await SeedProductOwnerScopeAsync();

        var expectedResult = CreateExpectedResult();
        var capturedCalls = CaptureProviderCalls(expectedResult);
        var handler = new GetBuildQualityPipelineDetailQueryHandler(_context, _scopeLoader, _provider.Object);

        var result = await handler.Handle(
            new GetBuildQualityPipelineDetailQuery(ProductOwnerId: 1, SprintId: 77, RepositoryId: 20),
            CancellationToken.None);

        Assert.AreSame(expectedResult, result.Result);
        Assert.AreEqual(20, result.RepositoryId);
        Assert.AreEqual("Repo B", result.RepositoryName);
        Assert.AreEqual(2, result.ProductId);
        Assert.HasCount(1, capturedCalls);
        CollectionAssert.AreEqual(new[] { 2001 }, capturedCalls[0].Builds.Select(build => build.BuildId).ToArray());
    }

    private List<CapturedProviderCall> CaptureProviderCalls(BuildQualityResultDto expectedResult)
    {
        var capturedCalls = new List<CapturedProviderCall>();

        _provider
            .Setup(provider => provider.Compute(
                It.IsAny<IEnumerable<BuildQualityBuildFact>>(),
                It.IsAny<IEnumerable<BuildQualityTestRunFact>>(),
                It.IsAny<IEnumerable<BuildQualityCoverageFact>>()))
            .Callback<IEnumerable<BuildQualityBuildFact>, IEnumerable<BuildQualityTestRunFact>, IEnumerable<BuildQualityCoverageFact>>((builds, testRuns, coverages) =>
            {
                capturedCalls.Add(new CapturedProviderCall(
                    builds.ToList(),
                    testRuns.ToList(),
                    coverages.ToList()));
            })
            .Returns(expectedResult);

        return capturedCalls;
    }

    private async Task SeedProductOwnerScopeAsync()
    {
        _context.Profiles.Add(new ProfileEntity
        {
            Id = 1,
            Name = "Owner",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        });

        _context.Products.AddRange(
            new ProductEntity
            {
                Id = 1,
                ProductOwnerId = 1,
                Name = "Product A",
                CreatedAt = DateTimeOffset.UtcNow,
                LastModified = DateTimeOffset.UtcNow
            },
            new ProductEntity
            {
                Id = 2,
                ProductOwnerId = 1,
                Name = "Product B",
                CreatedAt = DateTimeOffset.UtcNow,
                LastModified = DateTimeOffset.UtcNow
            });

        _context.Repositories.AddRange(
            new RepositoryEntity { Id = 10, ProductId = 1, Name = "Repo A", CreatedAt = DateTimeOffset.UtcNow },
            new RepositoryEntity { Id = 20, ProductId = 2, Name = "Repo B", CreatedAt = DateTimeOffset.UtcNow });

        _context.PipelineDefinitions.AddRange(
            new PipelineDefinitionEntity
            {
                Id = 101,
                PipelineDefinitionId = 1001,
                ProductId = 1,
                RepositoryId = 10,
                RepoId = "repo-a",
                RepoName = "Repo A",
                Name = "Pipeline A",
                DefaultBranch = "refs/heads/main",
                LastSyncedUtc = DateTimeOffset.UtcNow
            },
            new PipelineDefinitionEntity
            {
                Id = 201,
                PipelineDefinitionId = 2001,
                ProductId = 2,
                RepositoryId = 20,
                RepoId = "repo-b",
                RepoName = "Repo B",
                Name = "Pipeline B",
                DefaultBranch = "refs/heads/main",
                LastSyncedUtc = DateTimeOffset.UtcNow
            });

        _context.Sprints.Add(new SprintEntity
        {
            Id = 77,
            TeamId = 7,
            Path = "\\Sprint\\77",
            Name = "Sprint 77",
            StartUtc = new DateTimeOffset(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            StartDateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTimeOffset(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc)),
            EndDateUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            LastSyncedDateUtc = DateTime.UtcNow
        });

        _context.CachedPipelineRuns.AddRange(
            CreateBuild(id: 1001, pipelineDefinitionDbId: 101, result: "Succeeded", branch: "refs/heads/main", finishedUtc: new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc)),
            CreateBuild(id: 1002, pipelineDefinitionDbId: 101, result: "Failed", branch: "refs/heads/feature", finishedUtc: new DateTime(2026, 3, 6, 0, 0, 0, DateTimeKind.Utc)),
            CreateBuild(id: 1003, pipelineDefinitionDbId: 101, result: "Failed", branch: "refs/heads/main", finishedUtc: new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc)),
            CreateBuild(id: 2001, pipelineDefinitionDbId: 201, result: "PartiallySucceeded", branch: "refs/heads/main", finishedUtc: new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc)));

        _context.TestRuns.AddRange(
            new TestRunEntity { Id = 1, BuildId = 1001, TotalTests = 10, PassedTests = 9, NotApplicableTests = 1, CachedAt = DateTimeOffset.UtcNow },
            new TestRunEntity { Id = 2, BuildId = 1002, TotalTests = 5, PassedTests = 0, NotApplicableTests = 0, CachedAt = DateTimeOffset.UtcNow },
            new TestRunEntity { Id = 3, BuildId = 1003, TotalTests = 8, PassedTests = 7, NotApplicableTests = 0, CachedAt = DateTimeOffset.UtcNow },
            new TestRunEntity { Id = 4, BuildId = 2001, TotalTests = 12, PassedTests = 11, NotApplicableTests = 0, CachedAt = DateTimeOffset.UtcNow });

        _context.Coverages.AddRange(
            new CoverageEntity { Id = 1, BuildId = 1001, CoveredLines = 90, TotalLines = 100, CachedAt = DateTimeOffset.UtcNow },
            new CoverageEntity { Id = 2, BuildId = 1002, CoveredLines = 10, TotalLines = 100, CachedAt = DateTimeOffset.UtcNow },
            new CoverageEntity { Id = 3, BuildId = 1003, CoveredLines = 70, TotalLines = 100, CachedAt = DateTimeOffset.UtcNow },
            new CoverageEntity { Id = 4, BuildId = 2001, CoveredLines = 80, TotalLines = 100, CachedAt = DateTimeOffset.UtcNow });

        await _context.SaveChangesAsync();
    }

    private static CachedPipelineRunEntity CreateBuild(
        int id,
        int pipelineDefinitionDbId,
        string result,
        string branch,
        DateTime finishedUtc)
    {
        return new CachedPipelineRunEntity
        {
            Id = id,
            ProductOwnerId = 1,
            PipelineDefinitionId = pipelineDefinitionDbId,
            TfsRunId = id,
            Result = result,
            SourceBranch = branch,
            CreatedDateUtc = finishedUtc.AddMinutes(-10),
            FinishedDateUtc = finishedUtc,
            CachedAt = DateTimeOffset.UtcNow
        };
    }

    private static BuildQualityResultDto CreateExpectedResult()
    {
        return new BuildQualityResultDto
        {
            Metrics = new BuildQualityMetricsDto
            {
                SuccessRate = 0.123d,
                TestPassRate = 0.456d,
                TestVolume = 42,
                Coverage = 0.789d,
                Confidence = 2
            },
            Evidence = new BuildQualityEvidenceDto
            {
                EligibleBuilds = 99,
                SucceededBuilds = 10,
                FailedBuilds = 20,
                PartiallySucceededBuilds = 30,
                CanceledBuilds = 40,
                TotalTests = 50,
                PassedTests = 60,
                NotApplicableTests = 8,
                CoveredLines = 90,
                TotalLines = 100,
                BuildThresholdMet = true,
                TestThresholdMet = true
            }
        };
    }

    private sealed record CapturedProviderCall(
        IReadOnlyList<BuildQualityBuildFact> Builds,
        IReadOnlyList<BuildQualityTestRunFact> TestRuns,
        IReadOnlyList<BuildQualityCoverageFact> Coverages);
}
