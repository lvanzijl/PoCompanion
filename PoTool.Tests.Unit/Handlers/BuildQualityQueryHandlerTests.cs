using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using PoTool.Api.Handlers.BuildQuality;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services.BuildQuality;
using PoTool.Core.BuildQuality.Queries;
using PoTool.Shared.BuildQuality;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class BuildQualityQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private PoToolDbContext _context = null!;
    private BuildQualityScopeLoader _scopeLoader = null!;
    private Mock<IBuildQualityProvider> _provider = null!;

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
        _scopeLoader = new BuildQualityScopeLoader(_context);
        _provider = new Mock<IBuildQualityProvider>(MockBehavior.Strict);
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
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
                EffectiveFilter: DeliveryFilterTestFactory.DateRange(
                    [1, 2],
                    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero))),
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
            new GetBuildQualitySprintQuery(
                ProductOwnerId: 1,
                EffectiveFilter: DeliveryFilterTestFactory.SingleSprint(
                    [1, 2],
                    77,
                    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);

        Assert.AreEqual(77, result.SprintId);
        Assert.AreEqual("Sprint 77", result.SprintName);
        Assert.AreSame(expectedResult, result.Summary);
        Assert.HasCount(3, capturedCalls);
        CollectionAssert.AreEqual(new[] { 1001, 2001 }, capturedCalls[0].Builds.Select(build => build.BuildId).OrderBy(id => id).ToArray());
        CollectionAssert.AreEqual(new[] { 1001, 2001 }, capturedCalls[0].TestRuns.Select(testRun => testRun.BuildId).Distinct().OrderBy(id => id).ToArray());
        CollectionAssert.AreEqual(new[] { 1001, 2001 }, capturedCalls[0].Coverages.Select(coverage => coverage.BuildId).Distinct().OrderBy(id => id).ToArray());
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
        CollectionAssert.AreEqual(new[] { 2001 }, capturedCalls[0].TestRuns.Select(testRun => testRun.BuildId).Distinct().ToArray());
        CollectionAssert.AreEqual(new[] { 2001 }, capturedCalls[0].Coverages.Select(coverage => coverage.BuildId).Distinct().ToArray());
    }

    [TestMethod]
    public async Task ScopeLoader_WithMissingDefaultBranch_IncludesAllBuildsForThatPipeline()
    {
        await SeedProductOwnerScopeAsync();

        var definition = await _context.PipelineDefinitions.SingleAsync(pipeline => pipeline.Id == 201);
        definition.DefaultBranch = null;
        _context.CachedPipelineRuns.Add(
            CreateBuild(id: 2002, pipelineDefinitionDbId: 201, result: "Succeeded", branch: "refs/heads/feature/demo", finishedUtc: new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc)));
        await _context.SaveChangesAsync();

        var selection = await _scopeLoader.LoadAsync(
            productOwnerId: 1,
            windowStartUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            windowEndUtc: new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            repositoryId: null,
            pipelineDefinitionId: null,
            cancellationToken: CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { 1001, 2001, 2002 }, selection.Builds.Select(build => build.BuildId).OrderBy(id => id).ToArray());
    }

    [TestMethod]
    public async Task RollingWindowHandler_WithRealProvider_ComputesExpectedSummaryAndProductResults()
    {
        await SeedProductOwnerScopeAsync();

        var handler = new GetBuildQualityRollingWindowQueryHandler(_scopeLoader, new BuildQualityProvider());

        var result = await handler.Handle(
            new GetBuildQualityRollingWindowQuery(
                ProductOwnerId: 1,
                EffectiveFilter: DeliveryFilterTestFactory.DateRange(
                    [1, 2],
                    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 1, 2 }, result.ProductIds.ToArray());
        CollectionAssert.AreEqual(new[] { "refs/heads/main" }, result.DefaultBranches.ToArray());
        AssertBuildQualityResult(
            result.Summary,
            successRate: 0.5d,
            testPassRate: 20d / 21d,
            testVolume: 21,
            coverage: 0.85d,
            confidence: 1,
            eligibleBuilds: 2,
            succeededBuilds: 1,
            failedBuilds: 0,
            partiallySucceededBuilds: 1,
            canceledBuilds: 0,
            totalTests: 22,
            passedTests: 20,
            notApplicableTests: 1,
            coveredLines: 170,
            totalLines: 200,
            buildThresholdMet: false,
            testThresholdMet: true);
        Assert.HasCount(2, result.Products);
        Assert.AreEqual(1, result.Products[0].ProductId);
        Assert.AreEqual("Product A", result.Products[0].ProductName);
        AssertBuildQualityResult(
            result.Products[0].Result,
            successRate: 1d,
            testPassRate: 1d,
            testVolume: 9,
            coverage: 0.9d,
            confidence: 0,
            eligibleBuilds: 1,
            succeededBuilds: 1,
            failedBuilds: 0,
            partiallySucceededBuilds: 0,
            canceledBuilds: 0,
            totalTests: 10,
            passedTests: 9,
            notApplicableTests: 1,
            coveredLines: 90,
            totalLines: 100,
            buildThresholdMet: false,
            testThresholdMet: false);
        Assert.AreEqual(2, result.Products[1].ProductId);
        Assert.AreEqual("Product B", result.Products[1].ProductName);
        AssertBuildQualityResult(
            result.Products[1].Result,
            successRate: 0d,
            testPassRate: 11d / 12d,
            testVolume: 12,
            coverage: 0.8d,
            confidence: 0,
            eligibleBuilds: 1,
            succeededBuilds: 0,
            failedBuilds: 0,
            partiallySucceededBuilds: 1,
            canceledBuilds: 0,
            totalTests: 12,
            passedTests: 11,
            notApplicableTests: 0,
            coveredLines: 80,
            totalLines: 100,
            buildThresholdMet: false,
            testThresholdMet: false);
    }

    [TestMethod]
    public async Task SprintHandler_WithRealProvider_ComputesExpectedSummaryAndProductResults()
    {
        await SeedProductOwnerScopeAsync();

        var handler = new GetBuildQualitySprintQueryHandler(_context, _scopeLoader, new BuildQualityProvider());

        var result = await handler.Handle(
            new GetBuildQualitySprintQuery(
                ProductOwnerId: 1,
                EffectiveFilter: DeliveryFilterTestFactory.SingleSprint(
                    [1, 2],
                    77,
                    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);

        Assert.AreEqual(1, result.ProductOwnerId);
        Assert.AreEqual(77, result.SprintId);
        Assert.AreEqual("Sprint 77", result.SprintName);
        Assert.AreEqual(7, result.TeamId);
        Assert.AreEqual(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), result.SprintStartUtc);
        Assert.AreEqual(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), result.SprintEndUtc);
        CollectionAssert.AreEqual(new[] { 1, 2 }, result.ProductIds.ToArray());
        CollectionAssert.AreEqual(new[] { "refs/heads/main" }, result.DefaultBranches.ToArray());
        AssertBuildQualityResult(
            result.Summary,
            successRate: 0.5d,
            testPassRate: 20d / 21d,
            testVolume: 21,
            coverage: 0.85d,
            confidence: 1,
            eligibleBuilds: 2,
            succeededBuilds: 1,
            failedBuilds: 0,
            partiallySucceededBuilds: 1,
            canceledBuilds: 0,
            totalTests: 22,
            passedTests: 20,
            notApplicableTests: 1,
            coveredLines: 170,
            totalLines: 200,
            buildThresholdMet: false,
            testThresholdMet: true);
        Assert.HasCount(2, result.Products);
    }

    [TestMethod]
    public async Task PipelineDetailHandler_WithPipelineScope_ComputesExpectedResult()
    {
        await SeedProductOwnerScopeAsync();

        var handler = new GetBuildQualityPipelineDetailQueryHandler(_context, _scopeLoader, new BuildQualityProvider());

        var result = await handler.Handle(
            new GetBuildQualityPipelineDetailQuery(ProductOwnerId: 1, SprintId: 77, PipelineDefinitionId: 2001),
            CancellationToken.None);

        Assert.AreEqual(1, result.ProductOwnerId);
        Assert.AreEqual(77, result.SprintId);
        Assert.AreEqual("Sprint 77", result.SprintName);
        Assert.AreEqual(7, result.TeamId);
        Assert.AreEqual(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), result.SprintStartUtc);
        Assert.AreEqual(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), result.SprintEndUtc);
        Assert.AreEqual(2, result.ProductId);
        Assert.IsNull(result.RepositoryId);
        Assert.IsNull(result.RepositoryName);
        Assert.AreEqual(2001, result.PipelineDefinitionId);
        Assert.AreEqual("Pipeline B", result.PipelineName);
        CollectionAssert.AreEqual(new[] { "refs/heads/main" }, result.DefaultBranches.ToArray());
        AssertBuildQualityResult(
            result.Result,
            successRate: 0d,
            testPassRate: 11d / 12d,
            testVolume: 12,
            coverage: 0.8d,
            confidence: 0,
            eligibleBuilds: 1,
            succeededBuilds: 0,
            failedBuilds: 0,
            partiallySucceededBuilds: 1,
            canceledBuilds: 0,
            totalTests: 12,
            passedTests: 11,
            notApplicableTests: 0,
            coveredLines: 80,
            totalLines: 100,
            buildThresholdMet: false,
            testThresholdMet: false);
    }

    [TestMethod]
    public async Task RollingSprintAndPipelineHandlers_ReturnConsistentResults_ForSinglePipelineProduct()
    {
        await SeedProductOwnerScopeAsync();

        var rollingHandler = new GetBuildQualityRollingWindowQueryHandler(_scopeLoader, new BuildQualityProvider());
        var sprintHandler = new GetBuildQualitySprintQueryHandler(_context, _scopeLoader, new BuildQualityProvider());
        var pipelineHandler = new GetBuildQualityPipelineDetailQueryHandler(_context, _scopeLoader, new BuildQualityProvider());

        var rolling = await rollingHandler.Handle(
            new GetBuildQualityRollingWindowQuery(
                ProductOwnerId: 1,
                EffectiveFilter: DeliveryFilterTestFactory.DateRange(
                    [1, 2],
                    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);
        var sprint = await sprintHandler.Handle(
            new GetBuildQualitySprintQuery(
                ProductOwnerId: 1,
                EffectiveFilter: DeliveryFilterTestFactory.SingleSprint(
                    [1, 2],
                    77,
                    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);
        var pipeline = await pipelineHandler.Handle(
            new GetBuildQualityPipelineDetailQuery(ProductOwnerId: 1, SprintId: 77, PipelineDefinitionId: 2001),
            CancellationToken.None);

        AssertEquivalentBuildQualityResult(rolling.Summary, sprint.Summary);

        var rollingProduct = rolling.Products.Single(product => product.ProductId == 2);
        var sprintProduct = sprint.Products.Single(product => product.ProductId == 2);
        AssertEquivalentBuildQualityResult(rollingProduct.Result, sprintProduct.Result);
        AssertEquivalentBuildQualityResult(rollingProduct.Result, pipeline.Result);
    }

    [TestMethod]
    public async Task SprintHandler_WithMixedMissingChildFacts_KeepsMetricsKnownWhenAnyDataExists()
    {
        await SeedProductOwnerScopeAsync(includeBuildQualityEdgeCases: true);

        var handler = new GetBuildQualitySprintQueryHandler(_context, _scopeLoader, new BuildQualityProvider());

        var result = await handler.Handle(
            new GetBuildQualitySprintQuery(
                ProductOwnerId: 1,
                EffectiveFilter: DeliveryFilterTestFactory.SingleSprint(
                    [1, 2],
                    77,
                    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero))),
            CancellationToken.None);

        var productA = result.Products.Single(product => product.ProductId == 1);

        Assert.IsNotNull(productA.Result.Metrics.SuccessRate);
        Assert.IsNotNull(productA.Result.Metrics.TestPassRate);
        Assert.IsNotNull(productA.Result.Metrics.Coverage);
        Assert.IsGreaterThan(0d, productA.Result.Metrics.SuccessRate!.Value);
        Assert.IsGreaterThan(0d, productA.Result.Metrics.TestPassRate!.Value);
        Assert.IsGreaterThan(0d, productA.Result.Metrics.Coverage!.Value);
        Assert.IsGreaterThan(0, productA.Result.Metrics.Confidence);
        Assert.IsFalse(productA.Result.Evidence.SuccessRateUnknown);
        Assert.IsFalse(productA.Result.Evidence.TestPassRateUnknown);
        Assert.IsFalse(productA.Result.Evidence.CoverageUnknown);
        Assert.AreEqual(5, productA.Result.Evidence.EligibleBuilds);
        Assert.AreEqual(3, productA.Result.Evidence.FailedBuilds + productA.Result.Evidence.PartiallySucceededBuilds);
        Assert.AreEqual(365, productA.Result.Evidence.TotalTests);
        Assert.AreEqual(307, productA.Result.Evidence.PassedTests);
        Assert.AreEqual(10, productA.Result.Evidence.NotApplicableTests);
        Assert.AreEqual(10340, productA.Result.Evidence.CoveredLines);
        Assert.AreEqual(15200, productA.Result.Evidence.TotalLines);
    }

    [TestMethod]
    public void Provider_WithOnlyBuildsAndNoChildFacts_LeavesOnlyBuildSuccessKnown()
    {
        var result = new BuildQualityProvider().Compute(
            [
                new BuildQualityBuildFact(1, "Succeeded"),
                new BuildQualityBuildFact(2, "Failed"),
                new BuildQualityBuildFact(3, "PartiallySucceeded")
            ],
            Array.Empty<BuildQualityTestRunFact>(),
            Array.Empty<BuildQualityCoverageFact>());

        Assert.IsNotNull(result.Metrics.SuccessRate);
        Assert.AreEqual(1d / 3d, result.Metrics.SuccessRate!.Value, 0.000001d);
        Assert.IsNull(result.Metrics.TestPassRate);
        Assert.IsNull(result.Metrics.Coverage);
        Assert.IsFalse(result.Evidence.SuccessRateUnknown);
        Assert.IsTrue(result.Evidence.TestPassRateUnknown);
        Assert.IsTrue(result.Evidence.CoverageUnknown);
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

    private async Task SeedProductOwnerScopeAsync(bool includeBuildQualityEdgeCases = false)
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

        _context.Teams.Add(new TeamEntity
        {
            Id = 7,
            Name = "Team 7",
            TeamAreaPath = "\\Project\\Team 7",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        });

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

        if (includeBuildQualityEdgeCases)
        {
            _context.CachedPipelineRuns.AddRange(
                CreateBuild(id: 1004, pipelineDefinitionDbId: 101, result: "Failed", branch: "refs/heads/main", finishedUtc: new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc)),
                CreateBuild(id: 1005, pipelineDefinitionDbId: 101, result: "PartiallySucceeded", branch: "refs/heads/main", finishedUtc: new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc)),
                CreateBuild(id: 1006, pipelineDefinitionDbId: 101, result: "Succeeded", branch: "refs/heads/main", finishedUtc: new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc)),
                CreateBuild(id: 1007, pipelineDefinitionDbId: 101, result: "Failed", branch: "refs/heads/main", finishedUtc: new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc)));

            _context.TestRuns.AddRange(
                new TestRunEntity { Id = 5, BuildId = 1004, TotalTests = 210, PassedTests = 162, NotApplicableTests = 6, CachedAt = DateTimeOffset.UtcNow },
                new TestRunEntity { Id = 6, BuildId = 1005, TotalTests = 145, PassedTests = 136, NotApplicableTests = 3, CachedAt = DateTimeOffset.UtcNow });

            _context.Coverages.AddRange(
                new CoverageEntity { Id = 5, BuildId = 1004, CoveredLines = 4950, TotalLines = 7500, CachedAt = DateTimeOffset.UtcNow },
                new CoverageEntity { Id = 6, BuildId = 1006, CoveredLines = 5300, TotalLines = 7600, CachedAt = DateTimeOffset.UtcNow });
        }

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

    private static void AssertBuildQualityResult(
        BuildQualityResultDto result,
        double? successRate,
        double? testPassRate,
        int testVolume,
        double? coverage,
        int confidence,
        int eligibleBuilds,
        int succeededBuilds,
        int failedBuilds,
        int partiallySucceededBuilds,
        int canceledBuilds,
        int totalTests,
        int passedTests,
        int notApplicableTests,
        int coveredLines,
        int totalLines,
        bool buildThresholdMet,
        bool testThresholdMet)
    {
        AssertNullableDouble(successRate, result.Metrics.SuccessRate);
        AssertNullableDouble(testPassRate, result.Metrics.TestPassRate);
        Assert.AreEqual(testVolume, result.Metrics.TestVolume);
        AssertNullableDouble(coverage, result.Metrics.Coverage);
        Assert.AreEqual(confidence, result.Metrics.Confidence);

        Assert.AreEqual(eligibleBuilds, result.Evidence.EligibleBuilds);
        Assert.AreEqual(succeededBuilds, result.Evidence.SucceededBuilds);
        Assert.AreEqual(failedBuilds, result.Evidence.FailedBuilds);
        Assert.AreEqual(partiallySucceededBuilds, result.Evidence.PartiallySucceededBuilds);
        Assert.AreEqual(canceledBuilds, result.Evidence.CanceledBuilds);
        Assert.AreEqual(totalTests, result.Evidence.TotalTests);
        Assert.AreEqual(passedTests, result.Evidence.PassedTests);
        Assert.AreEqual(notApplicableTests, result.Evidence.NotApplicableTests);
        Assert.AreEqual(coveredLines, result.Evidence.CoveredLines);
        Assert.AreEqual(totalLines, result.Evidence.TotalLines);
        Assert.AreEqual(buildThresholdMet, result.Evidence.BuildThresholdMet);
        Assert.AreEqual(testThresholdMet, result.Evidence.TestThresholdMet);
        Assert.IsFalse(result.Evidence.SuccessRateUnknown);
        Assert.IsNull(result.Evidence.SuccessRateUnknownReason);
        Assert.IsFalse(result.Evidence.TestPassRateUnknown);
        Assert.IsNull(result.Evidence.TestPassRateUnknownReason);
        Assert.IsFalse(result.Evidence.CoverageUnknown);
        Assert.IsNull(result.Evidence.CoverageUnknownReason);
    }

    private static void AssertNullableDouble(double? expected, double? actual)
    {
        if (expected is null)
        {
            Assert.IsNull(actual);
            return;
        }

        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.Value, actual.Value, 0.000001d);
    }

    private static void AssertEquivalentBuildQualityResult(BuildQualityResultDto expected, BuildQualityResultDto actual)
    {
        AssertNullableDouble(expected.Metrics.SuccessRate, actual.Metrics.SuccessRate);
        AssertNullableDouble(expected.Metrics.TestPassRate, actual.Metrics.TestPassRate);
        Assert.AreEqual(expected.Metrics.TestVolume, actual.Metrics.TestVolume);
        AssertNullableDouble(expected.Metrics.Coverage, actual.Metrics.Coverage);
        Assert.AreEqual(expected.Metrics.Confidence, actual.Metrics.Confidence);

        Assert.AreEqual(expected.Evidence.EligibleBuilds, actual.Evidence.EligibleBuilds);
        Assert.AreEqual(expected.Evidence.SucceededBuilds, actual.Evidence.SucceededBuilds);
        Assert.AreEqual(expected.Evidence.FailedBuilds, actual.Evidence.FailedBuilds);
        Assert.AreEqual(expected.Evidence.PartiallySucceededBuilds, actual.Evidence.PartiallySucceededBuilds);
        Assert.AreEqual(expected.Evidence.CanceledBuilds, actual.Evidence.CanceledBuilds);
        Assert.AreEqual(expected.Evidence.TotalTests, actual.Evidence.TotalTests);
        Assert.AreEqual(expected.Evidence.PassedTests, actual.Evidence.PassedTests);
        Assert.AreEqual(expected.Evidence.NotApplicableTests, actual.Evidence.NotApplicableTests);
        Assert.AreEqual(expected.Evidence.CoveredLines, actual.Evidence.CoveredLines);
        Assert.AreEqual(expected.Evidence.TotalLines, actual.Evidence.TotalLines);
        Assert.AreEqual(expected.Evidence.BuildThresholdMet, actual.Evidence.BuildThresholdMet);
        Assert.AreEqual(expected.Evidence.TestThresholdMet, actual.Evidence.TestThresholdMet);
        Assert.AreEqual(expected.Evidence.SuccessRateUnknown, actual.Evidence.SuccessRateUnknown);
        Assert.AreEqual(expected.Evidence.SuccessRateUnknownReason, actual.Evidence.SuccessRateUnknownReason);
        Assert.AreEqual(expected.Evidence.TestPassRateUnknown, actual.Evidence.TestPassRateUnknown);
        Assert.AreEqual(expected.Evidence.TestPassRateUnknownReason, actual.Evidence.TestPassRateUnknownReason);
        Assert.AreEqual(expected.Evidence.CoverageUnknown, actual.Evidence.CoverageUnknown);
        Assert.AreEqual(expected.Evidence.CoverageUnknownReason, actual.Evidence.CoverageUnknownReason);
    }

    private sealed record CapturedProviderCall(
        IReadOnlyList<BuildQualityBuildFact> Builds,
        IReadOnlyList<BuildQualityTestRunFact> TestRuns,
        IReadOnlyList<BuildQualityCoverageFact> Coverages);
}
