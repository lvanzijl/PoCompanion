using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Pipelines;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Filters;
using PoTool.Core.Pipelines.Filters;
using PoTool.Core.Pipelines.Queries;
using PoTool.Shared.Pipelines;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Tests for GetPipelineInsightsQueryHandler.
/// Covers failure rate calculation, top-3 ranking, delta computation, and empty states.
/// </summary>
[TestClass]
public class GetPipelineInsightsQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private PoToolDbContext _context = null!;
    private IPipelineInsightsReadStore _readStore = null!;
    private Mock<ILogger<GetPipelineInsightsQueryHandler>> _mockLogger = null!;
    private GetPipelineInsightsQueryHandler _handler = null!;

    // Shared sprint window
    private static readonly DateTime SprintStart = new(2026, 2,  1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SprintEnd   = new(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PrevStart   = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PrevEnd     = new(2026, 2,  1, 0, 0, 0, DateTimeKind.Utc);

    // Dates inside each window
    private static readonly DateTime RunInCurrent  = new(2026, 2,  8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RunInPrevious = new(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);

    [TestInitialize]
    public async Task SetupAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context    = new PoToolDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _readStore  = new EfPipelineInsightsReadStore(_context);
        _mockLogger = new Mock<ILogger<GetPipelineInsightsQueryHandler>>();
        _handler    = new GetPipelineInsightsQueryHandler(_readStore, _mockLogger.Object);
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(int profileId, int teamId, int productId, int pipelineDefId, int sprintId, int prevSprintId)>
        SeedFullScenarioAsync(int seed)
    {
        var profile = new ProfileEntity { Id = seed, Name = $"PO {seed}" };
        var team    = new TeamEntity    { Id = seed, Name = $"Team {seed}", TeamAreaPath = $"Area/{seed}" };
        PersistenceTestGraph.EnsureProject(_context);
        var product = PersistenceTestGraph.CreateProduct(seed, $"Product {seed}", seed);
        var repo    = PersistenceTestGraph.CreateRepository(seed, seed, $"Repo {seed}");
        var pipeDef = new PipelineDefinitionEntity
        {
            Id = seed,
            PipelineDefinitionId = seed * 100,
            ProductId = seed,
            RepositoryId = seed,
            RepoId = $"guid-{seed}",
            RepoName = $"Repo {seed}",
            Name = $"Pipeline {seed}",
            LastSyncedUtc = DateTimeOffset.UtcNow
        };
        // Current sprint
        var sprint = new SprintEntity
        {
            Id = seed,
            TeamId = seed,
            Team = team,
            Path = $"\\Sprint\\{seed}",
            Name = $"Sprint {seed}",
            StartUtc     = new DateTimeOffset(SprintStart),
            StartDateUtc = SprintStart,
            EndUtc       = new DateTimeOffset(SprintEnd),
            EndDateUtc   = SprintEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        };
        // Previous sprint
        var prevSprint = new SprintEntity
        {
            Id = seed + 1000,
            TeamId = seed,
            Team = team,
            Path = $"\\Sprint\\prev{seed}",
            Name = $"Sprint prev{seed}",
            StartUtc     = new DateTimeOffset(PrevStart),
            StartDateUtc = PrevStart,
            EndUtc       = new DateTimeOffset(PrevEnd),
            EndDateUtc   = PrevEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        };

        _context.Profiles.Add(profile);
        _context.Teams.Add(team);
        _context.Products.Add(product);
        _context.Repositories.Add(repo);
        _context.PipelineDefinitions.Add(pipeDef);
        _context.Sprints.Add(sprint);
        _context.Sprints.Add(prevSprint);
        await _context.SaveChangesAsync();

        return (seed, seed, seed, seed, seed, seed + 1000);
    }

    private async Task AddRunAsync(int id, int pipelineDefId, int productOwnerId, DateTime finishedUtc,
        string result = "Succeeded", DateTime? startedUtc = null)
    {
        _context.CachedPipelineRuns.Add(new CachedPipelineRunEntity
        {
            Id                   = id,
            ProductOwnerId       = productOwnerId,
            PipelineDefinitionId = pipelineDefId,
            TfsRunId             = id,
            Result               = result,
            CreatedDateUtc       = startedUtc ?? finishedUtc.AddMinutes(-10),
            FinishedDateUtc      = finishedUtc,
            CachedAt             = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private static PipelineEffectiveFilter CreateFilter(
        IEnumerable<int>? productIds,
        IEnumerable<int> repositoryIds,
        int? sprintId)
        => new(
            new PipelineFilterContext(
                productIds?.Any() == true
                    ? FilterSelection<int>.Selected(productIds)
                    : FilterSelection<int>.All(),
                FilterSelection<int>.All(),
                repositoryIds.Any()
                    ? FilterSelection<int>.Selected(repositoryIds)
                    : FilterSelection<int>.Selected(Array.Empty<int>()),
                sprintId.HasValue
                    ? FilterTimeSelection.Sprint(sprintId.Value)
                    : FilterTimeSelection.None()),
            repositoryIds.ToArray(),
            Array.Empty<int>(),
            Array.Empty<PipelineBranchScope>(),
            sprintId.HasValue ? new DateTimeOffset(SprintStart) : null,
            sprintId.HasValue ? new DateTimeOffset(SprintEnd) : null,
            sprintId);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [TestMethod]
    [Description("When sprint does not exist, returns empty result with sprintId preserved")]
    public async Task Handle_UnknownSprint_ReturnsEmptyResult()
    {
        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(productIds: null, repositoryIds: Array.Empty<int>(), sprintId: null), RequestedSprintId: 999),
            CancellationToken.None);

        Assert.AreEqual(999, result.SprintId);
        Assert.AreEqual(0,   result.TotalBuilds);
    }

    [TestMethod]
    [Description("Product with no pipeline runs shows HasData=false")]
    public async Task Handle_NoRuns_ProductShowsHasDataFalse()
    {
        var (profileId, _, _, _, sprintId, _) = await SeedFullScenarioAsync(seed: 1);

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)),
            CancellationToken.None);

        Assert.AreEqual(sprintId, result.SprintId);
        Assert.HasCount(1, result.Products, "One product expected");
        Assert.IsFalse(result.Products[0].HasData, "No runs — HasData must be false");
        Assert.AreEqual(0, result.TotalBuilds);
    }

    [TestMethod]
    [Description("Single successful run produces 0% failure rate and 100% success rate")]
    public async Task Handle_OneSuccessRun_CorrectRates()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 2);
        await AddRunAsync(id: 1, pipeDefId, profileId, RunInCurrent, "Succeeded");

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)),
            CancellationToken.None);

        Assert.AreEqual(1, result.TotalBuilds);
        Assert.AreEqual(0.0, result.FailureRate);
        Assert.HasCount(1, result.Products);

        var product = result.Products[0];
        Assert.IsTrue(product.HasData);
        Assert.AreEqual(1,     product.CompletedBuilds);
        Assert.AreEqual(0.0,   product.FailureRate);
        Assert.AreEqual(100.0, product.SuccessRate);
    }

    [TestMethod]
    [Description("Mixed succeeded/failed runs produce correct failure rate")]
    public async Task Handle_MixedRuns_CorrectFailureRate()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 3);
        // 2 succeeded, 2 failed → 50% failure rate
        await AddRunAsync(id: 1, pipeDefId, profileId, RunInCurrent, "Succeeded");
        await AddRunAsync(id: 2, pipeDefId, profileId, RunInCurrent, "Succeeded");
        await AddRunAsync(id: 3, pipeDefId, profileId, RunInCurrent, "Failed");
        await AddRunAsync(id: 4, pipeDefId, profileId, RunInCurrent, "Failed");

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)),
            CancellationToken.None);

        Assert.AreEqual(50.0, result.FailureRate, "Global failure rate must be 50%");
        var product = result.Products[0];
        Assert.AreEqual(50.0, product.FailureRate, "Product failure rate must be 50%");
        Assert.AreEqual(2,    product.FailedBuilds);
        Assert.AreEqual(4,    product.CompletedBuilds);
    }

    [TestMethod]
    [Description("PartiallySucceeded counted as warning when IncludePartiallySucceeded=true")]
    public async Task Handle_PartialSuccess_IncludedAsWarning_WhenToggleOn()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 4);
        await AddRunAsync(id: 1, pipeDefId, profileId, RunInCurrent, "Succeeded");
        await AddRunAsync(id: 2, pipeDefId, profileId, RunInCurrent, "PartiallySucceeded");

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId), IncludePartiallySucceeded: true),
            CancellationToken.None);

        var product = result.Products[0];
        Assert.AreEqual(2,    product.CompletedBuilds, "PartiallySucceeded must be in completed when toggle ON");
        Assert.AreEqual(1,    product.WarningBuilds);
        Assert.AreEqual(50.0, product.WarningRate,     "Warning rate must be 50%");
        Assert.AreEqual(0.0,  product.FailureRate);
    }

    [TestMethod]
    [Description("PartiallySucceeded excluded when IncludePartiallySucceeded=false")]
    public async Task Handle_PartialSuccess_ExcludedWhenToggleOff()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 5);
        await AddRunAsync(id: 1, pipeDefId, profileId, RunInCurrent, "Succeeded");
        await AddRunAsync(id: 2, pipeDefId, profileId, RunInCurrent, "PartiallySucceeded");

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId), IncludePartiallySucceeded: false),
            CancellationToken.None);

        var product = result.Products[0];
        Assert.AreEqual(1, product.CompletedBuilds, "PartiallySucceeded excluded from completed when toggle OFF");
        Assert.AreEqual(0, product.WarningBuilds);
    }

    [TestMethod]
    [Description("Canceled run excluded by default (IncludeCanceled=false)")]
    public async Task Handle_Canceled_ExcludedByDefault()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 6);
        await AddRunAsync(id: 1, pipeDefId, profileId, RunInCurrent, "Succeeded");
        await AddRunAsync(id: 2, pipeDefId, profileId, RunInCurrent, "Canceled");

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId), IncludeCanceled: false),
            CancellationToken.None);

        Assert.AreEqual(2, result.TotalBuilds,  "TotalBuilds counts all runs");
        var product = result.Products[0];
        Assert.AreEqual(1, product.CompletedBuilds, "Canceled excluded from completed by default");
    }

    [TestMethod]
    [Description("Canceled run included when IncludeCanceled=true")]
    public async Task Handle_Canceled_IncludedWhenToggleOn()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 7);
        await AddRunAsync(id: 1, pipeDefId, profileId, RunInCurrent, "Succeeded");
        await AddRunAsync(id: 2, pipeDefId, profileId, RunInCurrent, "Canceled");

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId), IncludeCanceled: true),
            CancellationToken.None);

        var product = result.Products[0];
        Assert.AreEqual(2, product.CompletedBuilds, "Canceled included when toggle ON");
    }

    [TestMethod]
    [Description("Unknown, None, missing, and unrecognized results normalize predictably and do not affect completed metrics")]
    public async Task Handle_UnknownAndMissingResults_AreIgnoredAfterNormalization()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 71);
        await AddRunAsync(id: 1, pipeDefId, profileId, RunInCurrent, "Succeeded");
        await AddRunAsync(id: 2, pipeDefId, profileId, RunInCurrent.AddMinutes(1), null!);
        await AddRunAsync(id: 3, pipeDefId, profileId, RunInCurrent.AddMinutes(2), "Unknown");
        await AddRunAsync(id: 4, pipeDefId, profileId, RunInCurrent.AddMinutes(3), "None");
        await AddRunAsync(id: 5, pipeDefId, profileId, RunInCurrent.AddMinutes(4), "Deferred");

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)),
            CancellationToken.None);

        Assert.AreEqual(5, result.TotalBuilds, "TotalBuilds still reflects all raw runs");
        var product = result.Products[0];
        Assert.AreEqual(1, product.CompletedBuilds, "Only the succeeded run should contribute to completed metrics");
        Assert.AreEqual(0, product.FailedBuilds);
        Assert.AreEqual(0, product.WarningBuilds);
        Assert.AreEqual(0.0, product.FailureRate);
        Assert.AreEqual(0.0, product.WarningRate);
    }

    [TestMethod]
    [Description("Pipeline Insights repository scoping uses repository ID, so repository renames do not affect grouping")]
    public async Task Handle_RepositoryRename_DoesNotAffectRepositoryScopedGrouping()
    {
        var (profileId, _, productId, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 72);
        var repository = await _context.Repositories.SingleAsync(value => value.Id == productId);
        var definition = await _context.PipelineDefinitions.SingleAsync(value => value.Id == pipeDefId);

        repository.Name = "Repo Renamed";
        definition.RepoName = "Repo Original";
        await _context.SaveChangesAsync();

        await AddRunAsync(id: 1, pipeDefId, profileId, RunInCurrent, "Succeeded");

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)),
            CancellationToken.None);

        Assert.AreEqual(1, result.TotalBuilds);
        Assert.IsTrue(result.Products[0].HasData);
        Assert.AreEqual(1, result.Products[0].CompletedBuilds);
    }

    [TestMethod]
    [Description("Runs outside the sprint window are not counted")]
    public async Task Handle_RunOutsideWindow_NotCounted()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 8);
        var outsideDate = SprintEnd.AddDays(1);   // outside the sprint window
        await AddRunAsync(id: 1, pipeDefId, profileId, outsideDate, "Failed");

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)),
            CancellationToken.None);

        Assert.AreEqual(0, result.TotalBuilds, "Run finished after sprint end must not be counted");
    }

    [TestMethod]
    [Description("Runs that start before the sprint but finish inside the sprint are counted because analytics are finish-time anchored")]
    public async Task Handle_RunStartingBeforeSprintButFinishingInsideWindow_IsCounted()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 81);
        await AddRunAsync(
            id: 1,
            pipeDefId,
            profileId,
            finishedUtc: new DateTime(2026, 2, 2, 10, 0, 0, DateTimeKind.Utc),
            result: "Succeeded",
            startedUtc: new DateTime(2026, 1, 31, 22, 0, 0, DateTimeKind.Utc));

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)),
            CancellationToken.None);

        Assert.AreEqual(1, result.TotalBuilds);
        Assert.AreEqual(1, result.Products[0].CompletedBuilds);
    }

    [TestMethod]
    [Description("Top-3 ranking: pipeline with highest failure rate gets rank 1")]
    public async Task Handle_Top3_RankedByFailureRateDescending()
    {
        // Seed profile with three products and one pipeline each
        int profileId = 20;
        _context.Profiles.Add(new ProfileEntity { Id = profileId, Name = "PO20" });
        _context.Teams.Add(new TeamEntity { Id = 20, Name = "Team20", TeamAreaPath = "A" });
        PersistenceTestGraph.EnsureProject(_context);
        var sprint = new SprintEntity
        {
            Id = 20, TeamId = 20, Path = "\\S\\20", Name = "S20",
            StartDateUtc = SprintStart, EndDateUtc = SprintEnd,
            StartUtc = new DateTimeOffset(SprintStart), EndUtc = new DateTimeOffset(SprintEnd),
            LastSyncedDateUtc = DateTime.UtcNow
        };
        _context.Sprints.Add(sprint);

        // Product A: pipeline with 10% failure rate (1/10)
        SeedProductAndPipeline(10, profileId, teamId: 20, pipelineDbId: 10, pipelineName: "PipeA");
        // Product B: pipeline with 50% failure rate (5/10)
        SeedProductAndPipeline(11, profileId, teamId: 20, pipelineDbId: 11, pipelineName: "PipeB");
        // Product C: pipeline with 80% failure rate (8/10)
        SeedProductAndPipeline(12, profileId, teamId: 20, pipelineDbId: 12, pipelineName: "PipeC");

        await _context.SaveChangesAsync();

        // Add runs
        await AddMixedRunsAsync(baseId: 100, pipeDefId: 10, profileId, succeeded: 9, failed: 1);
        await AddMixedRunsAsync(baseId: 200, pipeDefId: 11, profileId, succeeded: 5, failed: 5);
        await AddMixedRunsAsync(baseId: 300, pipeDefId: 12, profileId, succeeded: 2, failed: 8);

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(
                PipelineInsightTestFilters.Create(new[] { 10, 11, 12 }, new[] { 10, 11, 12 }, 20)),
            CancellationToken.None);

        var top3 = result.GlobalTop3InTrouble;
        Assert.HasCount(3, top3, "Three pipelines must appear in global top-3");
        Assert.AreEqual("PipeC", top3[0].PipelineName, "PipeC (80%) must be rank 1");
        Assert.AreEqual("PipeB", top3[1].PipelineName, "PipeB (50%) must be rank 2");
        Assert.AreEqual("PipeA", top3[2].PipelineName, "PipeA (10%) must be rank 3");
        Assert.AreEqual(120, top3[0].PipelineDefinitionId, "Public pipeline identity must use the external TFS pipeline definition ID");
        Assert.AreEqual(110, top3[1].PipelineDefinitionId, "Public pipeline identity must use the external TFS pipeline definition ID");
        Assert.AreEqual(100, top3[2].PipelineDefinitionId, "Public pipeline identity must use the external TFS pipeline definition ID");
        Assert.AreEqual(1, top3[0].Rank);
        Assert.AreEqual(2, top3[1].Rank);
        Assert.AreEqual(3, top3[2].Rank);
    }

    [TestMethod]
    [Description("Delta vs previous sprint is computed correctly (failure rate difference in pp)")]
    public async Task Handle_WithPreviousSprint_DeltaFailureRateIsCorrect()
    {
        var (profileId, _, _, pipeDefId, sprintId, prevSprintId) = await SeedFullScenarioAsync(seed: 30);

        // Current sprint: 2 failed / 10 = 20% failure
        await AddMixedRunsAsync(baseId: 1, pipeDefId, profileId, succeeded: 8, failed: 2, finishedUtc: RunInCurrent);

        // Previous sprint: 5 failed / 10 = 50% failure
        await AddMixedRunsAsync(baseId: 100, pipeDefId, profileId, succeeded: 5, failed: 5, finishedUtc: RunInPrevious);

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)),
            CancellationToken.None);

        Assert.IsTrue(result.PreviousSprintId.HasValue, "Previous sprint must be identified");
        var top3 = result.Products[0].Top3InTrouble;
        Assert.HasCount(1, top3);
        var entry = top3[0];

        // Current = 20%, Previous = 50%, Delta = 20 - 50 = -30 pp
        Assert.AreEqual(20.0, entry.FailureRate, 0.1);
        Assert.IsTrue(entry.DeltaFailureRate.HasValue, "Delta must be present");
        Assert.AreEqual(-30.0, entry.DeltaFailureRate!.Value, 0.1, "Delta must be -30 pp (improved)");
    }

    [TestMethod]
    [Description("When no previous sprint exists, delta is null (n/a)")]
    public async Task Handle_NoPreviousSprint_DeltaIsNull()
    {
        // Create scenario but no previous sprint
        int profileId = 40;
        _context.Profiles.Add(new ProfileEntity { Id = profileId, Name = "PO40" });
        _context.Teams.Add(new TeamEntity { Id = 40, Name = "Team40", TeamAreaPath = "X" });
        PersistenceTestGraph.EnsureProject(_context);
        var product = PersistenceTestGraph.CreateProduct(40, "Product40", profileId);
        var repo    = PersistenceTestGraph.CreateRepository(40, 40, "Repo40");
        var pipe    = new PipelineDefinitionEntity
        {
            Id = 40, PipelineDefinitionId = 400, ProductId = 40, RepositoryId = 40,
            RepoId = "guid-40", RepoName = "Repo40", Name = "Pipe40",
            LastSyncedUtc = DateTimeOffset.UtcNow
        };
        var sprint = new SprintEntity
        {
            Id = 40, TeamId = 40, Path = "\\S\\40", Name = "S40",
            StartDateUtc = SprintStart, EndDateUtc = SprintEnd,
            StartUtc = new DateTimeOffset(SprintStart), EndUtc = new DateTimeOffset(SprintEnd),
            LastSyncedDateUtc = DateTime.UtcNow
        };
        _context.Products.Add(product);
        _context.Repositories.Add(repo);
        _context.PipelineDefinitions.Add(pipe);
        _context.Sprints.Add(sprint);
        await _context.SaveChangesAsync();

        await AddMixedRunsAsync(baseId: 1, pipeDefId: 40, profileId, succeeded: 5, failed: 5, finishedUtc: RunInCurrent);

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { 40 }, new[] { 40 }, 40)),
            CancellationToken.None);

        Assert.IsFalse(result.PreviousSprintId.HasValue, "No previous sprint must result in null PreviousSprintId");
        var top3 = result.Products[0].Top3InTrouble;
        Assert.HasCount(1, top3);
        Assert.IsNull(top3[0].DeltaFailureRate, "Delta must be null when no previous sprint");
    }

    [TestMethod]
    [Description("P90 duration requires at least 3 data points; null otherwise")]
    public async Task Handle_P90Duration_NullWhenFewerThan3Runs()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 50);

        // Only 2 runs with duration
        await AddRunAsync(id: 1, pipeDefId, profileId, RunInCurrent, "Succeeded",
            startedUtc: RunInCurrent.AddMinutes(-5));
        await AddRunAsync(id: 2, pipeDefId, profileId, RunInCurrent, "Failed",
            startedUtc: RunInCurrent.AddMinutes(-10));

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)),
            CancellationToken.None);

        Assert.IsNull(result.P90DurationMinutes, "P90 must be null with fewer than 3 runs");
        Assert.IsNull(result.Products[0].P90DurationMinutes, "Product P90 must be null with fewer than 3 runs");
    }

    [TestMethod]
    [Description("P90 duration is computed when at least 3 data points are present")]
    public async Task Handle_P90Duration_ComputedWith3OrMoreRuns()
    {
        var (profileId, _, _, pipeDefId, sprintId, _) = await SeedFullScenarioAsync(seed: 51);

        // 3 runs with known durations: 5, 10, 15 minutes
        await AddRunAsync(id: 1, pipeDefId, profileId, RunInCurrent, "Succeeded",
            startedUtc: RunInCurrent.AddMinutes(-5));
        await AddRunAsync(id: 2, pipeDefId, profileId, RunInCurrent.AddSeconds(1), "Succeeded",
            startedUtc: RunInCurrent.AddSeconds(1).AddMinutes(-10));
        await AddRunAsync(id: 3, pipeDefId, profileId, RunInCurrent.AddSeconds(2), "Succeeded",
            startedUtc: RunInCurrent.AddSeconds(2).AddMinutes(-15));

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)),
            CancellationToken.None);

        Assert.IsNotNull(result.P90DurationMinutes, "P90 must be present with 3+ runs");
        Assert.IsGreaterThan(0.0, result.P90DurationMinutes!.Value);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void SeedProductAndPipeline(int id, int profileId, int teamId, int pipelineDbId, string pipelineName)
    {
        PersistenceTestGraph.EnsureProject(_context);
        _context.Products.Add(PersistenceTestGraph.CreateProduct(id, $"Product {id}", profileId));
        _context.Repositories.Add(PersistenceTestGraph.CreateRepository(id, id, $"Repo {id}"));
        _context.PipelineDefinitions.Add(new PipelineDefinitionEntity
        {
            Id = pipelineDbId,
            PipelineDefinitionId = pipelineDbId * 10,
            ProductId = id,
            RepositoryId = id,
            RepoId = $"guid-{id}",
            RepoName = $"Repo {id}",
            Name = pipelineName,
            LastSyncedUtc = DateTimeOffset.UtcNow
        });
    }

    private async Task AddMixedRunsAsync(int baseId, int pipeDefId, int profileId,
        int succeeded, int failed, DateTime? finishedUtc = null)
    {
        var date = finishedUtc ?? RunInCurrent;
        for (int i = 0; i < succeeded; i++)
        {
            _context.CachedPipelineRuns.Add(new CachedPipelineRunEntity
            {
                Id                   = baseId + i,
                ProductOwnerId       = profileId,
                PipelineDefinitionId = pipeDefId,
                TfsRunId             = baseId + i,
                Result               = "Succeeded",
                FinishedDateUtc      = date.AddSeconds(i),
                CachedAt             = DateTimeOffset.UtcNow
            });
        }
        for (int i = 0; i < failed; i++)
        {
            _context.CachedPipelineRuns.Add(new CachedPipelineRunEntity
            {
                Id                   = baseId + succeeded + i,
                ProductOwnerId       = profileId,
                PipelineDefinitionId = pipeDefId,
                TfsRunId             = baseId + succeeded + i,
                Result               = "Failed",
                FinishedDateUtc      = date.AddSeconds(succeeded + i),
                CachedAt             = DateTimeOffset.UtcNow
            });
        }
        await _context.SaveChangesAsync();
    }
}

// ── Phase 2: Scatter point tests ───────────────────────────────────────────────

[TestClass]
public class GetPipelineInsightsScatterPointTests
{
    private PoToolDbContext _context = null!;
    private IPipelineInsightsReadStore _readStore = null!;
    private Mock<ILogger<GetPipelineInsightsQueryHandler>> _mockLogger = null!;
    private GetPipelineInsightsQueryHandler _handler = null!;

    private static readonly DateTime SprintStart = new(2026, 2,  1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SprintEnd   = new(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"ScatterTests_{Guid.NewGuid()}")
            .Options;
        _context    = new PoToolDbContext(options);
        _readStore  = new EfPipelineInsightsReadStore(_context);
        _mockLogger = new Mock<ILogger<GetPipelineInsightsQueryHandler>>();
        _handler    = new GetPipelineInsightsQueryHandler(_readStore, _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(int profileId, int pipeDefId, int sprintId)> SeedAsync(int seed)
    {
        _context.Profiles.Add(new ProfileEntity { Id = seed, Name = $"PO {seed}" });
        _context.Teams.Add(new TeamEntity { Id = seed, Name = $"Team {seed}", TeamAreaPath = $"A/{seed}" });
        PersistenceTestGraph.EnsureProject(_context);
        _context.Products.Add(PersistenceTestGraph.CreateProduct(seed, $"Prod {seed}", seed));
        _context.Repositories.Add(PersistenceTestGraph.CreateRepository(seed, seed, $"Repo {seed}"));
        _context.PipelineDefinitions.Add(new PipelineDefinitionEntity
        {
            Id = seed, PipelineDefinitionId = seed * 10, ProductId = seed,
            RepositoryId = seed, RepoId = $"guid-{seed}", RepoName = $"Repo {seed}",
            Name = $"Pipe {seed}", LastSyncedUtc = DateTimeOffset.UtcNow
        });
        _context.Sprints.Add(new SprintEntity
        {
            Id = seed, TeamId = seed, Path = $"\\S\\{seed}", Name = $"Spr {seed}",
            StartUtc = new DateTimeOffset(SprintStart), StartDateUtc = SprintStart,
            EndUtc   = new DateTimeOffset(SprintEnd),   EndDateUtc   = SprintEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return (seed, seed, seed);
    }

    private async Task AddRunAsync(int id, int pipeDefId, int profileId,
        string result, DateTime createdUtc, DateTime finishedUtc,
        string? runName = null, string? branch = null, string? url = null)
    {
        _context.CachedPipelineRuns.Add(new CachedPipelineRunEntity
        {
            Id                   = id,
            ProductOwnerId       = profileId,
            PipelineDefinitionId = pipeDefId,
            TfsRunId             = id * 10,
            RunName              = runName ?? $"20260101.{id}",
            Result               = result,
            CreatedDateUtc       = createdUtc,
            FinishedDateUtc      = finishedUtc,
            CreatedDate          = new DateTimeOffset(createdUtc),
            FinishedDate         = new DateTimeOffset(finishedUtc),
            SourceBranch         = branch ?? "refs/heads/main",
            Url                  = url ?? $"https://dev.azure.com/test/{id}",
            CachedAt             = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    [TestMethod]
    [Description("ScatterPoints are populated for a product with runs")]
    public async Task Handle_WithRuns_ScatterPointsPopulated()
    {
        var (profileId, pipeDefId, sprintId) = await SeedAsync(seed: 200);
        var start = new DateTime(2026, 2, 5, 10, 0, 0, DateTimeKind.Utc);

        await AddRunAsync(id: 1, pipeDefId, profileId, "Succeeded",
            createdUtc: start, finishedUtc: start.AddMinutes(12));
        await AddRunAsync(id: 2, pipeDefId, profileId, "Failed",
            createdUtc: start.AddHours(2), finishedUtc: start.AddHours(2).AddMinutes(5));

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        Assert.HasCount(1, result.Products);
        var scatter = result.Products[0].ScatterPoints;
        Assert.HasCount(2, scatter, "Both runs must appear as scatter points");
    }

    [TestMethod]
    [Description("ScatterPoints carry correct result, duration, and pipeline name")]
    public async Task Handle_ScatterPoints_HaveCorrectFields()
    {
        var (profileId, pipeDefId, sprintId) = await SeedAsync(seed: 201);
        var created  = new DateTime(2026, 2, 6, 9, 0, 0, DateTimeKind.Utc);
        var finished = created.AddMinutes(20);

        await AddRunAsync(id: 1, pipeDefId, profileId, "Succeeded",
            createdUtc: created, finishedUtc: finished,
            runName: "20260206.1", branch: "refs/heads/main",
            url: "https://dev.azure.com/test/1");

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        var pt = result.Products[0].ScatterPoints[0];
        Assert.AreEqual("Succeeded", pt.Result);
        Assert.AreEqual("20260206.1", pt.BuildNumber);
        Assert.AreEqual("Pipe 201", pt.PipelineName);
        Assert.AreEqual(2010, pt.PipelineDefinitionId, "Scatter points must expose the external TFS pipeline definition ID");
        Assert.IsNotNull(pt.StartTime, "StartTime must be set");
        Assert.IsTrue(pt.DurationMinutes.HasValue, "DurationMinutes must be set");
        Assert.AreEqual(20.0, pt.DurationMinutes!.Value, delta: 0.05);
        Assert.AreEqual("refs/heads/main", pt.Branch);
        Assert.AreEqual("https://dev.azure.com/test/1", pt.Url);
    }

    [TestMethod]
    [Description("ScatterPoints are empty for a product with no runs")]
    public async Task Handle_NoRuns_ScatterPointsEmpty()
    {
        var (profileId, _, sprintId) = await SeedAsync(seed: 202);

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        Assert.IsFalse(result.Products[0].HasData);
        Assert.HasCount(0, result.Products[0].ScatterPoints);
    }

    [TestMethod]
    [Description("ScatterPoints are ordered by start time ascending")]
    public async Task Handle_ScatterPoints_OrderedByStartTimeAscending()
    {
        var (profileId, pipeDefId, sprintId) = await SeedAsync(seed: 203);

        // Add runs out of order
        var t1 = new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 2,  5, 9, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 2,  8, 9, 0, 0, DateTimeKind.Utc);

        await AddRunAsync(id: 1, pipeDefId, profileId, "Succeeded", t1, t1.AddMinutes(10));
        await AddRunAsync(id: 2, pipeDefId, profileId, "Failed",    t2, t2.AddMinutes(8));
        await AddRunAsync(id: 3, pipeDefId, profileId, "Succeeded", t3, t3.AddMinutes(12));

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        var scatter = result.Products[0].ScatterPoints;
        Assert.HasCount(3, scatter);
        Assert.IsLessThanOrEqualTo(scatter[1].StartTime!.Value, scatter[0].StartTime!.Value, "Points must be sorted by start time");
        Assert.IsLessThanOrEqualTo(scatter[2].StartTime!.Value, scatter[1].StartTime!.Value, "Points must be sorted by start time");
    }

    [TestMethod]
    [Description("Sprint start/end boundaries are returned in PipelineInsightsDto")]
    public async Task Handle_ReturnsSprintBoundaries()
    {
        var (profileId, _, sprintId) = await SeedAsync(seed: 204);

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        Assert.IsNotNull(result.SprintStart, "SprintStart must be set");
        Assert.IsNotNull(result.SprintEnd,   "SprintEnd must be set");
        Assert.AreEqual(
            new DateTimeOffset(SprintStart),
            result.SprintStart!.Value,
            "SprintStart must match the sprint entity's StartUtc");
        Assert.AreEqual(
            new DateTimeOffset(SprintEnd),
            result.SprintEnd!.Value,
            "SprintEnd must match the sprint entity's EndUtc");
    }
}

internal static class PipelineInsightTestFilters
{
    private static readonly DateTimeOffset SprintStart = new(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
    private static readonly DateTimeOffset SprintEnd = new(new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc));

    public static PipelineEffectiveFilter Create(
        IEnumerable<int>? productIds,
        IEnumerable<int> repositoryIds,
        int? sprintId)
        => new(
            new PipelineFilterContext(
                productIds?.Any() == true
                    ? FilterSelection<int>.Selected(productIds)
                    : FilterSelection<int>.All(),
                FilterSelection<int>.All(),
                repositoryIds.Any()
                    ? FilterSelection<int>.Selected(repositoryIds)
                    : FilterSelection<int>.Selected(Array.Empty<int>()),
                sprintId.HasValue
                    ? FilterTimeSelection.Sprint(sprintId.Value)
                    : FilterTimeSelection.None()),
            repositoryIds.ToArray(),
            Array.Empty<int>(),
            Array.Empty<PipelineBranchScope>(),
            sprintId.HasValue ? SprintStart : null,
            sprintId.HasValue ? SprintEnd : null,
            sprintId);
}

// ── Phase 3: Per-pipeline breakdown + half-sprint trend tests ─────────────────

[TestClass]
public class GetPipelineInsightsBreakdownTests
{
    private PoToolDbContext _context = null!;
    private IPipelineInsightsReadStore _readStore = null!;
    private Mock<ILogger<GetPipelineInsightsQueryHandler>> _mockLogger = null!;
    private GetPipelineInsightsQueryHandler _handler = null!;

    // Sprint: Feb 1 – Feb 15  → midpoint = Feb 8
    private static readonly DateTime SprintStart = new(2026, 2,  1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SprintMid   = new(2026, 2,  8, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SprintEnd   = new(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);

    [TestInitialize]
    public void Setup()
    {
        var opts = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"BreakdownTests_{Guid.NewGuid()}")
            .Options;
        _context    = new PoToolDbContext(opts);
        _readStore  = new EfPipelineInsightsReadStore(_context);
        _mockLogger = new Mock<ILogger<GetPipelineInsightsQueryHandler>>();
        _handler    = new GetPipelineInsightsQueryHandler(_readStore, _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(int profileId, int pipeDefId, int sprintId)> SeedAsync(int seed)
    {
        _context.Profiles.Add(new ProfileEntity { Id = seed, Name = $"PO {seed}" });
        _context.Teams.Add(new TeamEntity { Id = seed, Name = $"Team {seed}", TeamAreaPath = $"A/{seed}" });
        PersistenceTestGraph.EnsureProject(_context);
        _context.Products.Add(PersistenceTestGraph.CreateProduct(seed, $"Prod {seed}", seed));
        _context.Repositories.Add(PersistenceTestGraph.CreateRepository(seed, seed, $"Repo {seed}"));
        _context.PipelineDefinitions.Add(new PipelineDefinitionEntity
        {
            Id = seed, PipelineDefinitionId = seed * 10, ProductId = seed,
            RepositoryId = seed, RepoId = $"guid-{seed}", RepoName = $"Repo {seed}",
            Name = $"Pipe {seed}", LastSyncedUtc = DateTimeOffset.UtcNow
        });
        _context.Sprints.Add(new SprintEntity
        {
            Id = seed, TeamId = seed, Path = $"\\S\\{seed}", Name = $"Spr {seed}",
            StartUtc = new DateTimeOffset(SprintStart), StartDateUtc = SprintStart,
            EndUtc   = new DateTimeOffset(SprintEnd),   EndDateUtc   = SprintEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return (seed, seed, seed);
    }

    private async Task AddRunAsync(int id, int pipeDefId, int profileId,
        string result, DateTime finishedUtc, DateTime? startedUtc = null)
    {
        _context.CachedPipelineRuns.Add(new CachedPipelineRunEntity
        {
            Id                   = id,
            ProductOwnerId       = profileId,
            PipelineDefinitionId = pipeDefId,
            TfsRunId             = id * 10,
            RunName              = $"20260101.{id}",
            Result               = result,
            CreatedDateUtc       = startedUtc ?? finishedUtc.AddMinutes(-10),
            FinishedDateUtc      = finishedUtc,
            CreatedDate          = new DateTimeOffset(startedUtc ?? finishedUtc.AddMinutes(-10)),
            FinishedDate         = new DateTimeOffset(finishedUtc),
            CachedAt             = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private static PipelineEffectiveFilter CreateFilter(
        IEnumerable<int>? productIds,
        IEnumerable<int> repositoryIds,
        int? sprintId)
        => new(
            new PipelineFilterContext(
                productIds?.Any() == true
                    ? FilterSelection<int>.Selected(productIds)
                    : FilterSelection<int>.All(),
                FilterSelection<int>.All(),
                repositoryIds.Any()
                    ? FilterSelection<int>.Selected(repositoryIds)
                    : FilterSelection<int>.Selected(Array.Empty<int>()),
                sprintId.HasValue
                    ? FilterTimeSelection.Sprint(sprintId.Value)
                    : FilterTimeSelection.None()),
            repositoryIds.ToArray(),
            Array.Empty<int>(),
            Array.Empty<PipelineBranchScope>(),
            sprintId.HasValue ? new DateTimeOffset(SprintStart) : null,
            sprintId.HasValue ? new DateTimeOffset(SprintEnd) : null,
            sprintId);

    // ── Breakdown populated ───────────────────────────────────────────────────

    [TestMethod]
    [Description("PipelineBreakdown is populated for a product with runs")]
    public async Task Handle_WithRuns_BreakdownPopulated()
    {
        var (profileId, pipeDefId, sprintId) = await SeedAsync(seed: 300);

        await AddRunAsync(id: 1, pipeDefId, profileId, "Succeeded",
            finishedUtc: new DateTime(2026, 2, 5, 12, 0, 0, DateTimeKind.Utc));
        await AddRunAsync(id: 2, pipeDefId, profileId, "Failed",
            finishedUtc: new DateTime(2026, 2, 6, 12, 0, 0, DateTimeKind.Utc));

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        Assert.HasCount(1, result.Products[0].PipelineBreakdown, "Breakdown must contain one pipeline");
    }

    [TestMethod]
    [Description("Breakdown entry has correct per-pipeline metrics")]
    public async Task Handle_Breakdown_CorrectMetrics()
    {
        var (profileId, pipeDefId, sprintId) = await SeedAsync(seed: 301);

        // 3 succeeded, 1 failed = 75% success, 25% failure
        await AddRunAsync(id: 1, pipeDefId, profileId, "Succeeded",
            finishedUtc: new DateTime(2026, 2, 4, 10, 0, 0, DateTimeKind.Utc),
            startedUtc:  new DateTime(2026, 2, 4,  9, 50, 0, DateTimeKind.Utc)); // 10 min
        await AddRunAsync(id: 2, pipeDefId, profileId, "Succeeded",
            finishedUtc: new DateTime(2026, 2, 5, 10, 0, 0, DateTimeKind.Utc),
            startedUtc:  new DateTime(2026, 2, 5,  9, 50, 0, DateTimeKind.Utc)); // 10 min
        await AddRunAsync(id: 3, pipeDefId, profileId, "Succeeded",
            finishedUtc: new DateTime(2026, 2, 6, 10, 0, 0, DateTimeKind.Utc),
            startedUtc:  new DateTime(2026, 2, 6,  9, 50, 0, DateTimeKind.Utc)); // 10 min
        await AddRunAsync(id: 4, pipeDefId, profileId, "Failed",
            finishedUtc: new DateTime(2026, 2, 7, 10, 0, 0, DateTimeKind.Utc),
            startedUtc:  new DateTime(2026, 2, 7,  9, 40, 0, DateTimeKind.Utc)); // 20 min

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        var entry = result.Products[0].PipelineBreakdown[0];
        Assert.AreEqual(3010, entry.PipelineDefinitionId, "Breakdown entries must expose the external TFS pipeline definition ID");
        Assert.AreEqual(4,    entry.TotalRuns,      "TotalRuns must be 4");
        Assert.AreEqual(4,    entry.CompletedRuns,  "CompletedRuns must be 4");
        Assert.AreEqual(3,    entry.SucceededRuns,  "SucceededRuns must be 3");
        Assert.AreEqual(1,    entry.FailedRuns,     "FailedRuns must be 1");
        Assert.AreEqual(25.0, entry.FailureRate,    delta: 0.1, message: "FailureRate must be 25%");
        Assert.AreEqual(75.0, entry.SuccessRate,    delta: 0.1, message: "SuccessRate must be 75%");
        Assert.IsTrue(entry.MedianDurationMinutes.HasValue, "MedianDurationMinutes must be set");
    }

    [TestMethod]
    [Description("Breakdown entries are ordered by failure rate descending")]
    public async Task Handle_Breakdown_OrderedByFailureRateDesc()
    {
        // Two pipelines: pipe 400 = 100% failure, pipe 401 = 0% failure
        var seed1 = 400;
        var seed2 = 401;
        _context.Profiles.Add(new ProfileEntity { Id = seed1, Name = $"PO {seed1}" });
        _context.Teams.Add(new TeamEntity { Id = seed1, Name = $"Team {seed1}", TeamAreaPath = $"A/{seed1}" });
        PersistenceTestGraph.EnsureProject(_context);
        _context.Products.Add(PersistenceTestGraph.CreateProduct(seed1, $"Prod {seed1}", seed1));
        _context.Repositories.Add(PersistenceTestGraph.CreateRepository(seed1, seed1, $"Repo {seed1}"));
        _context.Repositories.Add(PersistenceTestGraph.CreateRepository(seed2, seed1, $"Repo {seed2}"));
        _context.PipelineDefinitions.Add(new PipelineDefinitionEntity
        {
            Id = seed1, PipelineDefinitionId = seed1 * 10, ProductId = seed1,
            RepositoryId = seed1, RepoId = $"g-{seed1}", RepoName = $"R{seed1}",
            Name = "Pipeline A", LastSyncedUtc = DateTimeOffset.UtcNow
        });
        _context.PipelineDefinitions.Add(new PipelineDefinitionEntity
        {
            Id = seed2, PipelineDefinitionId = seed2 * 10, ProductId = seed1,
            RepositoryId = seed2, RepoId = $"g-{seed2}", RepoName = $"R{seed2}",
            Name = "Pipeline B", LastSyncedUtc = DateTimeOffset.UtcNow
        });
        _context.Sprints.Add(new SprintEntity
        {
            Id = seed1, TeamId = seed1, Path = $"\\S\\{seed1}", Name = $"Spr {seed1}",
            StartUtc = new DateTimeOffset(SprintStart), StartDateUtc = SprintStart,
            EndUtc   = new DateTimeOffset(SprintEnd),   EndDateUtc   = SprintEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var t = new DateTime(2026, 2, 5, 10, 0, 0, DateTimeKind.Utc);
        // Pipeline A: all fail
        _context.CachedPipelineRuns.Add(new CachedPipelineRunEntity { Id = 1, ProductOwnerId = seed1, PipelineDefinitionId = seed1, TfsRunId = 1, Result = "Failed",    FinishedDateUtc = t,              CachedAt = DateTimeOffset.UtcNow });
        _context.CachedPipelineRuns.Add(new CachedPipelineRunEntity { Id = 2, ProductOwnerId = seed1, PipelineDefinitionId = seed1, TfsRunId = 2, Result = "Failed",    FinishedDateUtc = t.AddHours(1),  CachedAt = DateTimeOffset.UtcNow });
        // Pipeline B: all succeed
        _context.CachedPipelineRuns.Add(new CachedPipelineRunEntity { Id = 3, ProductOwnerId = seed1, PipelineDefinitionId = seed2, TfsRunId = 3, Result = "Succeeded", FinishedDateUtc = t.AddHours(2),  CachedAt = DateTimeOffset.UtcNow });
        _context.CachedPipelineRuns.Add(new CachedPipelineRunEntity { Id = 4, ProductOwnerId = seed1, PipelineDefinitionId = seed2, TfsRunId = 4, Result = "Succeeded", FinishedDateUtc = t.AddHours(3),  CachedAt = DateTimeOffset.UtcNow });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { seed1 }, new[] { seed1, seed2 }, seed1)), CancellationToken.None);

        var breakdown = result.Products[0].PipelineBreakdown;
        Assert.HasCount(2, breakdown, "Two pipelines must appear in breakdown");
        Assert.IsGreaterThanOrEqualTo(breakdown[1].FailureRate, breakdown[0].FailureRate,
            "Breakdown must be ordered failure rate descending");
        Assert.AreEqual("Pipeline A", breakdown[0].PipelineName, "Worst pipeline must be first");
    }

    // ── Half-sprint trend ─────────────────────────────────────────────────────

    [TestMethod]
    [Description("HalfSprintTrend is Improving when second half failure rate is ≥ 10 pp lower")]
    public async Task Handle_HalfSprintTrend_Improving()
    {
        var (profileId, pipeDefId, sprintId) = await SeedAsync(seed: 500);

        // First half (Feb 1–7): 5 runs, all fail  → 100%
        var fh = new DateTime(2026, 2, 3, 10, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 5; i++)
            await AddRunAsync(id: i + 1, pipeDefId, profileId, "Failed",
                finishedUtc: fh.AddHours(i));

        // Second half (Feb 8–14): 5 runs, all succeed → 0%
        var sh = new DateTime(2026, 2, 9, 10, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 5; i++)
            await AddRunAsync(id: i + 10, pipeDefId, profileId, "Succeeded",
                finishedUtc: sh.AddHours(i));

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        var entry = result.Products[0].PipelineBreakdown[0];
        Assert.AreEqual(PipelineHalfSprintTrend.Improving, entry.HalfSprintTrend,
            "Should be Improving (100% → 0%)");
        Assert.IsNotNull(entry.FirstHalfFailureRate,  "FirstHalfFailureRate must be set");
        Assert.IsNotNull(entry.SecondHalfFailureRate, "SecondHalfFailureRate must be set");
        Assert.AreEqual(100.0, entry.FirstHalfFailureRate!.Value,  delta: 0.1);
        Assert.AreEqual(0.0,   entry.SecondHalfFailureRate!.Value, delta: 0.1);
    }

    [TestMethod]
    [Description("HalfSprintTrend is Degrading when second half failure rate is ≥ 10 pp higher")]
    public async Task Handle_HalfSprintTrend_Degrading()
    {
        var (profileId, pipeDefId, sprintId) = await SeedAsync(seed: 501);

        // First half: 5 succeeded → 0%
        var fh = new DateTime(2026, 2, 3, 10, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 5; i++)
            await AddRunAsync(id: i + 1, pipeDefId, profileId, "Succeeded",
                finishedUtc: fh.AddHours(i));

        // Second half: 5 failed → 100%
        var sh = new DateTime(2026, 2, 9, 10, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 5; i++)
            await AddRunAsync(id: i + 10, pipeDefId, profileId, "Failed",
                finishedUtc: sh.AddHours(i));

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        var entry = result.Products[0].PipelineBreakdown[0];
        Assert.AreEqual(PipelineHalfSprintTrend.Degrading, entry.HalfSprintTrend,
            "Should be Degrading (0% → 100%)");
    }

    [TestMethod]
    [Description("HalfSprintTrend is Stable when failure rate difference is < 10 pp")]
    public async Task Handle_HalfSprintTrend_Stable()
    {
        var (profileId, pipeDefId, sprintId) = await SeedAsync(seed: 502);

        // First half: 4 runs, 1 failed → 25%
        var fh = new DateTime(2026, 2, 3, 10, 0, 0, DateTimeKind.Utc);
        await AddRunAsync(id: 1, pipeDefId, profileId, "Failed",    finishedUtc: fh);
        await AddRunAsync(id: 2, pipeDefId, profileId, "Succeeded", finishedUtc: fh.AddHours(1));
        await AddRunAsync(id: 3, pipeDefId, profileId, "Succeeded", finishedUtc: fh.AddHours(2));
        await AddRunAsync(id: 4, pipeDefId, profileId, "Succeeded", finishedUtc: fh.AddHours(3));

        // Second half: 4 runs, 1 failed → 25%  (diff = 0 pp → stable)
        var sh = new DateTime(2026, 2, 9, 10, 0, 0, DateTimeKind.Utc);
        await AddRunAsync(id: 5, pipeDefId, profileId, "Failed",    finishedUtc: sh);
        await AddRunAsync(id: 6, pipeDefId, profileId, "Succeeded", finishedUtc: sh.AddHours(1));
        await AddRunAsync(id: 7, pipeDefId, profileId, "Succeeded", finishedUtc: sh.AddHours(2));
        await AddRunAsync(id: 8, pipeDefId, profileId, "Succeeded", finishedUtc: sh.AddHours(3));

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        var entry = result.Products[0].PipelineBreakdown[0];
        Assert.AreEqual(PipelineHalfSprintTrend.Stable, entry.HalfSprintTrend,
            "Should be Stable (25% → 25%)");
    }

    [TestMethod]
    [Description("HalfSprintTrend is Insufficient when either half has fewer than 2 completed runs")]
    public async Task Handle_HalfSprintTrend_Insufficient()
    {
        var (profileId, pipeDefId, sprintId) = await SeedAsync(seed: 503);

        // Only 1 run — goes into first half, second half empty
        await AddRunAsync(id: 1, pipeDefId, profileId, "Failed",
            finishedUtc: new DateTime(2026, 2, 4, 10, 0, 0, DateTimeKind.Utc));

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        var entry = result.Products[0].PipelineBreakdown[0];
        Assert.AreEqual(PipelineHalfSprintTrend.Insufficient, entry.HalfSprintTrend,
            "Should be Insufficient with only 1 run");
        Assert.IsNull(entry.FirstHalfFailureRate,  "FirstHalfFailureRate must be null when insufficient");
        Assert.IsNull(entry.SecondHalfFailureRate, "SecondHalfFailureRate must be null when insufficient");
    }

    [TestMethod]
    [Description("Breakdown is empty for a product with no runs")]
    public async Task Handle_NoRuns_BreakdownEmpty()
    {
        var (profileId, _, sprintId) = await SeedAsync(seed: 504);

        var result = await _handler.Handle(
            new GetPipelineInsightsQuery(PipelineInsightTestFilters.Create(new[] { profileId }, new[] { profileId }, sprintId)), CancellationToken.None);

        Assert.IsFalse(result.Products[0].HasData);
        Assert.HasCount(0, result.Products[0].PipelineBreakdown);
    }
}
