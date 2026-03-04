using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Pipelines;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Tests for GetPipelineSprintTrendsQueryHandler covering team-based pipeline filtering.
/// </summary>
[TestClass]
public class GetPipelineSprintTrendsQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private Mock<ILogger<GetPipelineSprintTrendsQueryHandler>> _mockLogger = null!;
    private GetPipelineSprintTrendsQueryHandler _handler = null!;

    // Sprint window shared across most scenarios
    private static readonly DateTime SprintStart  = new(2026, 1,  1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SprintEnd    = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RunInSprint  = new(2026, 1,  5, 0, 0, 0, DateTimeKind.Utc);   // inside window
    private static readonly DateTime RunAfterEnd  = new(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc);   // outside window

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"PipelineSprintTrendsTests_{Guid.NewGuid()}")
            .Options;
        _context = new PoToolDbContext(options);
        _mockLogger = new Mock<ILogger<GetPipelineSprintTrendsQueryHandler>>();
        _handler = new GetPipelineSprintTrendsQueryHandler(_context, _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Seeds a complete profile -> team -> product -> repo -> pipeline def -> sprint -> run chain.
    /// CachedPipelineRunEntity.PipelineDefinitionId is the DB PK (PipelineDefinitionEntity.Id).
    /// </summary>
    private async Task<(int profileId, int teamId, int productId, int pipelineDefDbId, int sprintId)>
        SeedScenarioAsync(int seed, DateTime runFinishedUtc)
    {
        var profile = new ProfileEntity { Id = seed, Name = $"Profile {seed}" };
        var team    = new TeamEntity    { Id = seed, Name = $"Team {seed}", TeamAreaPath = $"Area {seed}" };
        var product = new ProductEntity { Id = seed, Name = $"Product {seed}", ProductOwnerId = seed };
        var repo    = new RepositoryEntity { Id = seed, ProductId = seed, Name = $"Repo {seed}" };
        var link    = new ProductTeamLinkEntity { ProductId = seed, TeamId = seed };
        var pipelineDef = new PipelineDefinitionEntity
        {
            Id = seed,
            PipelineDefinitionId = seed * 100,
            ProductId = seed,
            RepositoryId = seed,
            RepoId = $"repo-guid-{seed}",
            RepoName = $"Repo {seed}",
            Name = $"Pipeline {seed}",
            LastSyncedUtc = DateTimeOffset.UtcNow
        };
        var sprint = new SprintEntity
        {
            Id = seed,
            TeamId = seed,
            Path = $"\\Sprint {seed}",
            Name = $"Sprint {seed}",
            StartUtc = new DateTimeOffset(SprintStart),
            StartDateUtc = SprintStart,
            EndUtc = new DateTimeOffset(SprintEnd),
            EndDateUtc = SprintEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        };
        var run = new CachedPipelineRunEntity
        {
            Id = seed,
            ProductOwnerId = seed,
            PipelineDefinitionId = seed,    // FK to PipelineDefinitionEntity.Id (DB PK)
            TfsRunId = seed,
            Result = "Succeeded",
            FinishedDate = new DateTimeOffset(runFinishedUtc),
            FinishedDateUtc = runFinishedUtc,
            CachedAt = DateTimeOffset.UtcNow
        };

        _context.Profiles.Add(profile);
        _context.Teams.Add(team);
        _context.Products.Add(product);
        _context.Repositories.Add(repo);
        _context.ProductTeamLinks.Add(link);
        _context.PipelineDefinitions.Add(pipelineDef);
        _context.Sprints.Add(sprint);
        _context.CachedPipelineRuns.Add(run);
        await _context.SaveChangesAsync();

        return (profileId: seed, teamId: seed, productId: seed, pipelineDefDbId: seed, sprintId: seed);
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("TeamId resolves to linked products via ProductTeamLinks; those pipelines' runs are counted")]
    public async Task Handle_TeamFilter_FiltersToTeamLinkedPipelines()
    {
        var (profileId, teamId, _, _, sprintId) = await SeedScenarioAsync(seed: 1, runFinishedUtc: RunInSprint);

        var query = new GetPipelineSprintTrendsQuery(
            ProductOwnerId: profileId, SprintIds: new[] { sprintId }, ProductIds: null, TeamId: teamId);

        var response = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(response.Success);
        Assert.HasCount(1, response.Sprints);
        var sprint = response.Sprints[0];
        Assert.AreEqual(sprintId, sprint.SprintId);
        Assert.AreEqual(1, sprint.TotalRuns, "Run finished within sprint window must be counted");
        Assert.AreEqual(1, sprint.CompletedRuns);
        Assert.IsNotNull(sprint.SuccessRate);
        Assert.AreEqual(100.0, sprint.SuccessRate!.Value, delta: 0.01);
    }

    [TestMethod]
    [Description("TeamId with no ProductTeamLinks returns sprint slot with TotalRuns=0")]
    public async Task Handle_TeamFilter_NoLinkedProducts_ReturnsSprintSlotWithZeroRuns()
    {
        var profile = new ProfileEntity { Id = 10, Name = "Profile 10" };
        var team    = new TeamEntity    { Id = 10, Name = "Team 10", TeamAreaPath = "Area 10" };
        var sprint  = new SprintEntity
        {
            Id = 10, TeamId = 10, Path = "\\Sprint 10", Name = "Sprint 10",
            StartUtc = new DateTimeOffset(SprintStart), StartDateUtc = SprintStart,
            EndUtc = new DateTimeOffset(SprintEnd), EndDateUtc = SprintEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        };
        _context.Profiles.Add(profile);
        _context.Teams.Add(team);
        _context.Sprints.Add(sprint);
        await _context.SaveChangesAsync();

        var query = new GetPipelineSprintTrendsQuery(
            ProductOwnerId: 10, SprintIds: new[] { 10 }, ProductIds: null, TeamId: 10);

        var response = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(response.Success);
        // Sprint slot returned (needed for chart X-axis) with no runs
        Assert.HasCount(1, response.Sprints);
        Assert.AreEqual(0, response.Sprints[0].TotalRuns);
    }

    [TestMethod]
    [Description("TeamId with linked products but no pipeline definitions returns sprint slot with TotalRuns=0")]
    public async Task Handle_TeamFilter_LinkedProductsButNoPipelineDefs_ReturnsSprintSlotWithZeroRuns()
    {
        var profile = new ProfileEntity { Id = 20, Name = "Profile 20" };
        var team    = new TeamEntity    { Id = 20, Name = "Team 20", TeamAreaPath = "Area 20" };
        var product = new ProductEntity { Id = 20, Name = "Product 20", ProductOwnerId = 20 };
        var link    = new ProductTeamLinkEntity { ProductId = 20, TeamId = 20 };
        var sprint  = new SprintEntity
        {
            Id = 20, TeamId = 20, Path = "\\Sprint 20", Name = "Sprint 20",
            StartUtc = new DateTimeOffset(SprintStart), StartDateUtc = SprintStart,
            EndUtc = new DateTimeOffset(SprintEnd), EndDateUtc = SprintEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        };
        _context.Profiles.Add(profile);
        _context.Teams.Add(team);
        _context.Products.Add(product);
        _context.ProductTeamLinks.Add(link);
        _context.Sprints.Add(sprint);
        await _context.SaveChangesAsync();

        var query = new GetPipelineSprintTrendsQuery(
            ProductOwnerId: 20, SprintIds: new[] { 20 }, ProductIds: null, TeamId: 20);

        var response = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(response.Success);
        Assert.HasCount(1, response.Sprints);
        Assert.AreEqual(0, response.Sprints[0].TotalRuns);
    }

    [TestMethod]
    [Description("Explicit ProductIds takes priority over TeamId")]
    public async Task Handle_ProductIdsTakePriorityOverTeamId()
    {
        var (profileId1, teamId1, productId1, _, sprintId1) = await SeedScenarioAsync(seed: 30, runFinishedUtc: RunInSprint);
        var (profileId2, teamId2, productId2, _, sprintId2) = await SeedScenarioAsync(seed: 31, runFinishedUtc: RunInSprint);

        // ProductIds=[30] wins over TeamId=31
        var query = new GetPipelineSprintTrendsQuery(
            ProductOwnerId: profileId1,
            SprintIds: new[] { sprintId1 },
            ProductIds: new List<int> { productId1 },
            TeamId: teamId2);

        var response = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(response.Success);
        Assert.HasCount(1, response.Sprints);
        Assert.AreEqual(1, response.Sprints[0].TotalRuns, "Only runs for the explicit product should be counted");
    }

    [TestMethod]
    [Description("TeamId filter excludes runs from pipelines not linked to the selected team")]
    public async Task Handle_TeamFilter_ExcludesRunsFromOtherTeamsPipelines()
    {
        // team 40 has a run INSIDE the sprint window; team 41's run is AFTER the sprint window
        var (profileId1, teamId1, _, _, sprintId1) = await SeedScenarioAsync(seed: 40, runFinishedUtc: RunInSprint);
        var (profileId2, teamId2, _, _, sprintId2) = await SeedScenarioAsync(seed: 41, runFinishedUtc: RunAfterEnd);

        // Query sprint 40's window, filtered to team 41
        var query = new GetPipelineSprintTrendsQuery(
            ProductOwnerId: profileId1,
            SprintIds: new[] { sprintId1 },
            ProductIds: null,
            TeamId: teamId2);

        var response = await _handler.Handle(query, CancellationToken.None);

        // Sprint 40 date range is queried, but team 40's run is excluded (wrong team)
        // and team 41's run is outside the sprint 40 window -> TotalRuns=0
        Assert.IsTrue(response.Success);
        Assert.HasCount(1, response.Sprints);
        Assert.AreEqual(0, response.Sprints[0].TotalRuns,
            "Team 40 run excluded (wrong team); team 41 run outside sprint 40 window");
    }

    // -----------------------------------------------------------------------
    // P90 duration tests
    // -----------------------------------------------------------------------

    [TestMethod]
    [Description("P90DurationHours is null when fewer than 3 runs have duration data (gap policy)")]
    public async Task Handle_P90Duration_NullWhenFewerThanThreeRuns()
    {
        // Seed 2 runs with duration data — below the P90 threshold of 3
        var profile = new ProfileEntity { Id = 50, Name = "Profile 50" };
        var team    = new TeamEntity    { Id = 50, Name = "Team 50", TeamAreaPath = "Area 50" };
        var product = new ProductEntity { Id = 50, Name = "Product 50", ProductOwnerId = 50 };
        var repo    = new RepositoryEntity { Id = 50, ProductId = 50, Name = "Repo 50" };
        var link    = new ProductTeamLinkEntity { ProductId = 50, TeamId = 50 };
        var pipelineDef = new PipelineDefinitionEntity
        {
            Id = 50, PipelineDefinitionId = 5000, ProductId = 50, RepositoryId = 50,
            RepoId = "repo-guid-50", RepoName = "Repo 50", Name = "Pipeline 50",
            LastSyncedUtc = DateTimeOffset.UtcNow
        };
        var sprint = new SprintEntity
        {
            Id = 50, TeamId = 50, Path = "\\Sprint 50", Name = "Sprint 50",
            StartUtc = new DateTimeOffset(SprintStart), StartDateUtc = SprintStart,
            EndUtc = new DateTimeOffset(SprintEnd), EndDateUtc = SprintEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        };

        var runBase = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        // 2 runs, each 1 hour long
        _context.Profiles.Add(profile);
        _context.Teams.Add(team);
        _context.Products.Add(product);
        _context.Repositories.Add(repo);
        _context.ProductTeamLinks.Add(link);
        _context.PipelineDefinitions.Add(pipelineDef);
        _context.Sprints.Add(sprint);
        _context.CachedPipelineRuns.AddRange(
            new CachedPipelineRunEntity
            {
                Id = 501, ProductOwnerId = 50, PipelineDefinitionId = 50, TfsRunId = 501,
                Result = "Succeeded",
                CreatedDateUtc = runBase, FinishedDateUtc = runBase.AddHours(1),
                FinishedDate = new DateTimeOffset(runBase.AddHours(1)), CachedAt = DateTimeOffset.UtcNow
            },
            new CachedPipelineRunEntity
            {
                Id = 502, ProductOwnerId = 50, PipelineDefinitionId = 50, TfsRunId = 502,
                Result = "Succeeded",
                CreatedDateUtc = runBase.AddHours(2), FinishedDateUtc = runBase.AddHours(3),
                FinishedDate = new DateTimeOffset(runBase.AddHours(3)), CachedAt = DateTimeOffset.UtcNow
            });
        await _context.SaveChangesAsync();

        var query = new GetPipelineSprintTrendsQuery(
            ProductOwnerId: 50, SprintIds: new[] { 50 }, ProductIds: null, TeamId: null);

        var response = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(response.Success);
        Assert.HasCount(1, response.Sprints);
        var sprintDto = response.Sprints[0];
        Assert.AreEqual(2, sprintDto.TotalRuns);
        Assert.IsNotNull(sprintDto.MedianDurationHours, "Median should be present for 2 runs");
        Assert.IsNull(sprintDto.P90DurationHours, "P90 must be null when fewer than 3 runs have duration data");
    }

    [TestMethod]
    [Description("P90DurationHours is computed when 3 or more runs have duration data")]
    public async Task Handle_P90Duration_ComputedWhenThreeOrMoreRuns()
    {
        var profile = new ProfileEntity { Id = 60, Name = "Profile 60" };
        var team    = new TeamEntity    { Id = 60, Name = "Team 60", TeamAreaPath = "Area 60" };
        var product = new ProductEntity { Id = 60, Name = "Product 60", ProductOwnerId = 60 };
        var repo    = new RepositoryEntity { Id = 60, ProductId = 60, Name = "Repo 60" };
        var link    = new ProductTeamLinkEntity { ProductId = 60, TeamId = 60 };
        var pipelineDef = new PipelineDefinitionEntity
        {
            Id = 60, PipelineDefinitionId = 6000, ProductId = 60, RepositoryId = 60,
            RepoId = "repo-guid-60", RepoName = "Repo 60", Name = "Pipeline 60",
            LastSyncedUtc = DateTimeOffset.UtcNow
        };
        var sprint = new SprintEntity
        {
            Id = 60, TeamId = 60, Path = "\\Sprint 60", Name = "Sprint 60",
            StartUtc = new DateTimeOffset(SprintStart), StartDateUtc = SprintStart,
            EndUtc = new DateTimeOffset(SprintEnd), EndDateUtc = SprintEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        };

        var runBase = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        // 3 runs with durations 1h, 2h, 3h
        _context.Profiles.Add(profile);
        _context.Teams.Add(team);
        _context.Products.Add(product);
        _context.Repositories.Add(repo);
        _context.ProductTeamLinks.Add(link);
        _context.PipelineDefinitions.Add(pipelineDef);
        _context.Sprints.Add(sprint);
        _context.CachedPipelineRuns.AddRange(
            new CachedPipelineRunEntity
            {
                Id = 601, ProductOwnerId = 60, PipelineDefinitionId = 60, TfsRunId = 601,
                Result = "Succeeded",
                CreatedDateUtc = runBase, FinishedDateUtc = runBase.AddHours(1),
                FinishedDate = new DateTimeOffset(runBase.AddHours(1)), CachedAt = DateTimeOffset.UtcNow
            },
            new CachedPipelineRunEntity
            {
                Id = 602, ProductOwnerId = 60, PipelineDefinitionId = 60, TfsRunId = 602,
                Result = "Succeeded",
                CreatedDateUtc = runBase.AddHours(2), FinishedDateUtc = runBase.AddHours(4),
                FinishedDate = new DateTimeOffset(runBase.AddHours(4)), CachedAt = DateTimeOffset.UtcNow
            },
            new CachedPipelineRunEntity
            {
                Id = 603, ProductOwnerId = 60, PipelineDefinitionId = 60, TfsRunId = 603,
                Result = "Succeeded",
                CreatedDateUtc = runBase.AddHours(5), FinishedDateUtc = runBase.AddHours(8),
                FinishedDate = new DateTimeOffset(runBase.AddHours(8)), CachedAt = DateTimeOffset.UtcNow
            });
        await _context.SaveChangesAsync();

        var query = new GetPipelineSprintTrendsQuery(
            ProductOwnerId: 60, SprintIds: new[] { 60 }, ProductIds: null, TeamId: null);

        var response = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(response.Success);
        Assert.HasCount(1, response.Sprints);
        var sprintDto = response.Sprints[0];
        Assert.AreEqual(3, sprintDto.TotalRuns);
        Assert.IsNotNull(sprintDto.P90DurationHours, "P90 must be computed for 3 or more runs");
        // Durations sorted: 1h, 2h, 3h → P90 rank = 0.9*(3-1)=1.8 → lo=1(2h) hi=2(3h) → 2 + (3-2)*0.8 = 2.8
        Assert.AreEqual(2.8, sprintDto.P90DurationHours!.Value, delta: 0.01);
    }

    [TestMethod]
    [Description("Sprint with no runs returns TotalRuns=0 and null metrics (gap, not zero)")]
    public async Task Handle_EmptySprint_ReturnsNullMetrics()
    {
        var profile = new ProfileEntity { Id = 70, Name = "Profile 70" };
        var team    = new TeamEntity    { Id = 70, Name = "Team 70", TeamAreaPath = "Area 70" };
        var sprint  = new SprintEntity
        {
            Id = 70, TeamId = 70, Path = "\\Sprint 70", Name = "Sprint 70",
            StartUtc = new DateTimeOffset(SprintStart), StartDateUtc = SprintStart,
            EndUtc = new DateTimeOffset(SprintEnd), EndDateUtc = SprintEnd,
            LastSyncedDateUtc = DateTime.UtcNow
        };
        _context.Profiles.Add(profile);
        _context.Teams.Add(team);
        _context.Sprints.Add(sprint);
        await _context.SaveChangesAsync();

        var query = new GetPipelineSprintTrendsQuery(
            ProductOwnerId: 70, SprintIds: new[] { 70 }, ProductIds: null, TeamId: null);

        var response = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(response.Success);
        Assert.HasCount(1, response.Sprints);
        var sprintDto = response.Sprints[0];
        Assert.AreEqual(0, sprintDto.TotalRuns);
        Assert.IsNull(sprintDto.SuccessRate, "SuccessRate must be null (gap) for empty sprint");
        Assert.IsNull(sprintDto.MedianDurationHours, "MedianDurationHours must be null (gap) for empty sprint");
        Assert.IsNull(sprintDto.P90DurationHours, "P90DurationHours must be null (gap) for empty sprint");
        Assert.IsNull(sprintDto.FlakinessRate, "FlakinessRate must be null (gap) for empty sprint");
    }
}
