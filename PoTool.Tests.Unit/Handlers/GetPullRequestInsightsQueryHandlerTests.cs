using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.PullRequests;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Filters;
using PoTool.Core.PullRequests.Filters;
using PoTool.Core.PullRequests.Queries;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Tests for GetPullRequestInsightsQueryHandler.
///
/// Business rules verified:
///   - Empty date range returns empty DTO (zero summary, empty collections).
///   - Date range filter excludes PRs outside the window.
///   - Repository name filter works.
///   - TeamId scoping applies ProductTeamLinks: only PRs linked to team products are included.
///   - Summary rates (merge, abandon, rework) are computed correctly.
///   - Color category is assigned correctly (merged-clean / merged-rework / abandoned / active).
///   - Top 3 problematic PRs are ranked by composite score (lifetime 40%, cycles 30%, files 20%, comments 10%).
///   - Longest PR table is ordered by lifetime descending and capped at 20.
///   - Repository breakdown is sorted by PR count descending and contains correct per-repo stats.
///   - Scatter points are generated for every PR in scope.
///   - Median/P90 edge cases (null when sample too small).
/// </summary>
[TestClass]
public class GetPullRequestInsightsQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private Mock<ILogger<GetPullRequestInsightsQueryHandler>> _mockLogger = null!;
    private IPullRequestQueryStore _queryStore = null!;
    private GetPullRequestInsightsQueryHandler _handler = null!;

    // Common date window used by most tests
    private static readonly DateTimeOffset RangeFrom = new(2026, 1, 1,  0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RangeTo   = new(2026, 3, 1,  0, 0, 0, TimeSpan.Zero);

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PrInsightsTests_{Guid.NewGuid()}")
            .Options;
        _context    = new PoToolDbContext(options);
        _mockLogger = new Mock<ILogger<GetPullRequestInsightsQueryHandler>>();
        _queryStore = new EfPullRequestQueryStore(_context);
        _handler    = new GetPullRequestInsightsQueryHandler(_queryStore, _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task AddPrAsync(
        int id,
        string status,
        DateTimeOffset createdDate,
        DateTimeOffset? completedDate = null,
        string repository = "Repo-A",
        string author = "dev1",
        int? productId = null)
    {
        _context.PullRequests.Add(new PullRequestEntity
        {
            Id             = id,
            InternalId     = id,
            Title          = $"PR-{id}",
            Status         = status,
            CreatedBy      = author,
            RepositoryName = repository,
            ProductId      = productId,
            CreatedDate    = createdDate,
            CreatedDateUtc = createdDate.UtcDateTime,
            CompletedDate  = completedDate,
            IterationPath  = @"\Team\Sprint 1",
            SourceBranch   = "refs/heads/feature",
            TargetBranch   = "refs/heads/main",
            RetrievedAt    = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private async Task AddIterationsAsync(int prId, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            _context.PullRequestIterations.Add(new PullRequestIterationEntity
            {
                Id              = prId * 100 + i,
                PullRequestId   = prId,
                IterationNumber = i,
                CreatedDate     = DateTimeOffset.UtcNow,
                UpdatedDate     = DateTimeOffset.UtcNow,
                CommitCount     = 1,
                ChangeCount     = 10
            });
        }
        await _context.SaveChangesAsync();
    }

    private async Task AddCommentsAsync(int prId, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            _context.PullRequestComments.Add(new PullRequestCommentEntity
            {
                InternalId     = prId * 1000 + i,
                Id             = prId * 1000 + i,
                PullRequestId  = prId,
                ThreadId       = i,
                Author         = "reviewer1",
                Content        = "comment",
                CreatedDate    = DateTimeOffset.UtcNow,
                CreatedDateUtc = DateTime.UtcNow,
                IsResolved     = false
            });
        }
        await _context.SaveChangesAsync();
    }

    private async Task AddFileChangesAsync(int prId, params string[] filePaths)
    {
        for (var i = 0; i < filePaths.Length; i++)
        {
            _context.PullRequestFileChanges.Add(new PullRequestFileChangeEntity
            {
                Id            = prId * 10000 + i,
                PullRequestId = prId,
                IterationId   = 1,
                FilePath      = filePaths[i],
                ChangeType    = "edit",
                LinesAdded    = 5,
                LinesDeleted  = 2,
                LinesModified = 0
            });
        }
        await _context.SaveChangesAsync();
    }

    private async Task AddTeamWithProductAsync(int teamId, int productId)
    {
        _context.Teams.Add(new TeamEntity
        {
            Id           = teamId,
            Name         = $"Team {teamId}",
            TeamAreaPath = $@"Area\{teamId}"
        });
        PersistenceTestGraph.EnsureProject(_context);
        _context.Products.Add(new ProductEntity
        {
            Id             = productId,
            Name           = $"Product {productId}",
            ProjectId      = PersistenceTestGraph.DefaultProjectId,
            ProductOwnerId = 1
        });
        _context.ProductTeamLinks.Add(new ProductTeamLinkEntity
        {
            TeamId    = teamId,
            ProductId = productId
        });
        await _context.SaveChangesAsync();
    }

    private async Task AddRepositoryAsync(int productId, string repoName)
    {
        _context.Repositories.Add(new RepositoryEntity
        {
            ProductId = productId,
            Name      = repoName,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private GetPullRequestInsightsQuery MakeQuery(
        int? teamId = null,
        string? repository = null,
        IReadOnlyList<int>? productIds = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to   = null) =>
        new(new PullRequestEffectiveFilter(
            new PullRequestFilterContext(
                productIds is { Count: > 0 } ? FilterSelection<int>.Selected(productIds) : FilterSelection<int>.All(),
                teamId.HasValue ? FilterSelection<int>.Selected([teamId.Value]) : FilterSelection<int>.All(),
                string.IsNullOrWhiteSpace(repository) ? FilterSelection<string>.All() : FilterSelection<string>.Selected([repository]),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterTimeSelection.DateRange(from ?? RangeFrom, to ?? RangeTo)),
            string.IsNullOrWhiteSpace(repository) ? ["Repo-A", "Repo-B", "Repo-Team10", "Backend", "Frontend"] : [repository],
            from ?? RangeFrom,
            to ?? RangeTo,
            null,
            Array.Empty<int>()));

    // ── Tests ─────────────────────────────────────────────────────────────────

    [TestMethod]
    [Description("When no PRs exist in the date range the handler returns an empty result")]
    public async Task Handle_NoPrsInRange_ReturnsEmptyResult()
    {
        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Summary.TotalPrs);
        Assert.HasCount(0, result.ScatterPoints);
        Assert.HasCount(0, result.Top3Problematic);
        Assert.HasCount(0, result.LongestPrs);
        Assert.HasCount(0, result.RepositoryBreakdown);
    }

    [TestMethod]
    [Description("PRs created before the from-date are excluded from results")]
    public async Task Handle_PrBeforeDateRange_IsExcluded()
    {
        var outsideRange = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(1, "completed", outsideRange, outsideRange.AddDays(2));

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.AreEqual(0, result.Summary.TotalPrs);
    }

    [TestMethod]
    [Description("When teamId is given, only PRs linked to the team's products are returned")]
    public async Task Handle_TeamFilter_ExcludesPrsFromOtherTeams()
    {
        await AddTeamWithProductAsync(teamId: 10, productId: 100);
        await AddRepositoryAsync(productId: 100, repoName: "Repo-Team10");

        var inRange = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(1, "completed", inRange, inRange.AddDays(3), repository: "Repo-Team10", productId: 100); // linked product
        await AddPrAsync(2, "completed", inRange, inRange.AddDays(3), repository: "Repo-Team10", productId: 200); // different product
        await AddPrAsync(3, "completed", inRange, inRange.AddDays(3), repository: "Repo-Unknown");                // unscoped PR

        var result = await _handler.Handle(MakeQuery(teamId: 10, productIds: [100]), CancellationToken.None);

        Assert.AreEqual(1, result.Summary.TotalPrs);
        Assert.HasCount(1, result.ScatterPoints);
        Assert.AreEqual("PR-1", result.ScatterPoints[0].Title);
    }

    [TestMethod]
    [Description("Team scoping uses ProductId even when no repository configuration is present")]
    public async Task Handle_TeamFilter_UsesProductIdScopeWithoutRepositoryConfiguration()
    {
        await AddTeamWithProductAsync(teamId: 10, productId: 100);

        var inRange = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(1, "completed", inRange, inRange.AddDays(3), productId: 100);
        await AddPrAsync(2, "completed", inRange, inRange.AddDays(2), productId: 999);

        var result = await _handler.Handle(MakeQuery(teamId: 10, productIds: [100]), CancellationToken.None);

        Assert.AreEqual(1, result.Summary.TotalPrs);
        Assert.AreEqual(1, result.ScatterPoints[0].Id);
    }

    [TestMethod]
    [Description("Repository filter returns only PRs from the specified repository")]
    public async Task Handle_RepositoryFilter_ReturnsOnlyMatchingRepository()
    {
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(1, "completed", d, d.AddDays(1), repository: "Backend");
        await AddPrAsync(2, "completed", d, d.AddDays(1), repository: "Frontend");

        var result = await _handler.Handle(MakeQuery(repository: "Backend"), CancellationToken.None);

        Assert.AreEqual(1, result.Summary.TotalPrs);
        Assert.HasCount(1, result.ScatterPoints);
        Assert.AreEqual("Backend", result.ScatterPoints[0].Repository);
    }

    [TestMethod]
    [Description("Summary rates are computed correctly for a mixed set of completed/abandoned/rework PRs")]
    public async Task Handle_SummaryRates_AreComputedCorrectly()
    {
        // 4 PRs: 2 completed (1 clean, 1 rework), 1 abandoned, 1 active
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(1, "completed", d, d.AddDays(2));   // clean (no iterations)
        await AddPrAsync(2, "completed", d, d.AddDays(3));   // rework (2 iterations)
        await AddPrAsync(3, "abandoned", d, d.AddDays(1));
        await AddPrAsync(4, "active",    d);                  // still open

        await AddIterationsAsync(2, 2); // PR-2: 2 iterations → rework

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.AreEqual(4, result.Summary.TotalPrs);
        Assert.AreEqual(50.0, result.Summary.MergeRatePct);            // 2/4
        Assert.AreEqual(25.0, result.Summary.AbandonRatePct);          // 1/4
        Assert.AreEqual(25.0, result.Summary.ChangesRequestedRatePct); // 1/4 (only PR-2 is rework)
    }

    [TestMethod]
    [Description("Color category: merged-clean for ≤1 iteration, merged-rework for >1, abandoned and active")]
    public async Task Handle_ColorCategory_IsAssignedCorrectly()
    {
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(1, "completed", d, d.AddDays(1));  // 0 iterations → clean
        await AddPrAsync(2, "completed", d, d.AddDays(2));  // 1 iteration  → clean
        await AddPrAsync(3, "completed", d, d.AddDays(3));  // 2 iterations → rework
        await AddPrAsync(4, "abandoned", d, d.AddDays(1));
        await AddPrAsync(5, "active",    d);

        await AddIterationsAsync(2, 1);
        await AddIterationsAsync(3, 2);

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        var points = result.ScatterPoints.ToDictionary(p => p.Id, p => p.ColorCategory);
        Assert.AreEqual("merged-clean",  points[1]);
        Assert.AreEqual("merged-clean",  points[2]);
        Assert.AreEqual("merged-rework", points[3]);
        Assert.AreEqual("abandoned",     points[4]);
        Assert.AreEqual("active",        points[5]);
    }

    [TestMethod]
    [Description("Top 3 problematic PRs are ranked by composite score; the PR with max in all dimensions ranks first")]
    public async Task Handle_Top3Problematic_RankedByCompositeScore()
    {
        var d = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        // PR-3 has both max lifetime AND max cycles/files/comments → composite score must be highest
        await AddPrAsync(1, "completed", d, d.AddDays(10)); // 240 h
        await AddPrAsync(2, "completed", d, d.AddDays(1));  // 24 h, many cycles/files/comments
        await AddPrAsync(3, "completed", d, d.AddDays(30)); // 720 h, many cycles/files/comments
        await AddPrAsync(4, "completed", d, d.AddDays(1));  // minimal

        await AddIterationsAsync(2, 5);
        await AddIterationsAsync(3, 5);
        await AddCommentsAsync(2, 10);
        await AddCommentsAsync(3, 10);
        await AddFileChangesAsync(2, "/a", "/b", "/c", "/d", "/e");
        await AddFileChangesAsync(3, "/a", "/b", "/c", "/d", "/e");

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.HasCount(3, result.Top3Problematic);
        Assert.AreEqual(3, result.Top3Problematic[0].Id, "PR-3 must be ranked #1 (max in all dimensions)");
    }

    [TestMethod]
    [Description("Longest PR table is capped at 20 entries and sorted by lifetime descending")]
    public async Task Handle_LongestPrsTable_CappedAt20AndSortedDescending()
    {
        var d = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Create 25 PRs with lifetimes 1..25 days
        for (var i = 1; i <= 25; i++)
            await AddPrAsync(i, "completed", d, d.AddDays(i));

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.HasCount(20, result.LongestPrs);
        Assert.AreEqual(25, result.LongestPrs[0].Id, "Longest PR (25 days) must be first");

        for (var i = 1; i < result.LongestPrs.Count; i++)
            Assert.IsLessThanOrEqualTo(result.LongestPrs[i - 1].LifetimeHours, result.LongestPrs[i].LifetimeHours);
    }

    [TestMethod]
    [Description("Repository breakdown is sorted by PR count descending")]
    public async Task Handle_RepositoryBreakdown_SortedByPrCountDescending()
    {
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(1, "completed", d, d.AddDays(1), repository: "Repo-A");
        await AddPrAsync(2, "completed", d, d.AddDays(2), repository: "Repo-A");
        await AddPrAsync(3, "abandoned", d, d.AddDays(1), repository: "Repo-A");
        await AddPrAsync(4, "completed", d, d.AddDays(3), repository: "Repo-B");

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.HasCount(2, result.RepositoryBreakdown);
        Assert.AreEqual("Repo-A", result.RepositoryBreakdown[0].Repository);
        Assert.AreEqual(3, result.RepositoryBreakdown[0].PrCount);
        Assert.AreEqual("Repo-B", result.RepositoryBreakdown[1].Repository);
        Assert.AreEqual(1, result.RepositoryBreakdown[1].PrCount);
    }

    [TestMethod]
    [Description("Repository breakdown merge% and abandon% are calculated correctly")]
    public async Task Handle_RepositoryBreakdown_RatesAreCorrect()
    {
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        // 2 completed, 1 abandoned → merge 66.7%, abandon 33.3%
        await AddPrAsync(1, "completed", d, d.AddDays(2), repository: "Repo-A");
        await AddPrAsync(2, "completed", d, d.AddDays(4), repository: "Repo-A");
        await AddPrAsync(3, "abandoned", d, d.AddDays(1), repository: "Repo-A");

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        var repo = result.RepositoryBreakdown.Single();
        Assert.AreEqual(3, repo.PrCount);
        Assert.AreEqual(66.7, repo.MergePct);
        Assert.AreEqual(33.3, repo.AbandonPct);
    }

    [TestMethod]
    [Description("Scatter points contain one entry per PR in scope")]
    public async Task Handle_ScatterPoints_OnePerPrInScope()
    {
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(1, "completed", d, d.AddDays(2));
        await AddPrAsync(2, "abandoned", d, d.AddDays(1));

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.HasCount(2, result.ScatterPoints);
        var ids = result.ScatterPoints.Select(p => p.Id).OrderBy(x => x).ToList();
        CollectionAssert.AreEqual(new[] { 1, 2 }, ids);
    }

    [TestMethod]
    [Description("P90 lifetime is null when fewer than 3 PRs have a positive lifetime")]
    public async Task Handle_P90Lifetime_NullWhenFewerThan3PositiveLifetimePrs()
    {
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(1, "completed", d, d.AddDays(1));
        await AddPrAsync(2, "completed", d, d.AddDays(2));

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.IsNull(result.Summary.P90LifetimeHours, "P90 must be null when fewer than 3 PRs have positive lifetime");
    }

    [TestMethod]
    [Description("P90 lifetime uses linear interpolation when 3 or more PRs have a positive lifetime")]
    public async Task Handle_P90Lifetime_UsesLinearInterpolationWhen3OrMorePrs()
    {
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(1, "completed", d, d.AddDays(1));
        await AddPrAsync(2, "completed", d, d.AddDays(2));
        await AddPrAsync(3, "completed", d, d.AddDays(3));

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.IsNotNull(result.Summary.P90LifetimeHours, "P90 must be non-null with ≥3 PRs");
        Assert.AreEqual(67.2, result.Summary.P90LifetimeHours!.Value, 0.001,
            "P90 of [24, 48, 72] hours should use linear interpolation, not nearest-rank.");
    }

    [TestMethod]
    [Description("Author breakdown contains one entry per distinct author, sorted by PR count descending")]
    public async Task Handle_AuthorBreakdown_SortedByPrCountDescending()
    {
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        // dev1: 3 PRs, dev2: 1 PR
        await AddPrAsync(1, "completed", d, d.AddDays(1), author: "dev1");
        await AddPrAsync(2, "completed", d, d.AddDays(2), author: "dev1");
        await AddPrAsync(3, "abandoned", d, d.AddDays(1), author: "dev1");
        await AddPrAsync(4, "completed", d, d.AddDays(3), author: "dev2");

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.HasCount(2, result.AuthorBreakdown);
        Assert.AreEqual("dev1", result.AuthorBreakdown[0].Author);
        Assert.AreEqual(3, result.AuthorBreakdown[0].PrCount);
        Assert.AreEqual("dev2", result.AuthorBreakdown[1].Author);
        Assert.AreEqual(1, result.AuthorBreakdown[1].PrCount);
    }

    [TestMethod]
    [Description("Author breakdown rates (merge%, abandon%, rework%) are computed correctly per author")]
    public async Task Handle_AuthorBreakdown_RatesAreCorrect()
    {
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        // dev1: 2 completed (1 clean, 1 rework), 1 abandoned → merge 66.7%, abandon 33.3%, rework 33.3%
        await AddPrAsync(1, "completed", d, d.AddDays(2), author: "dev1");   // clean
        await AddPrAsync(2, "completed", d, d.AddDays(3), author: "dev1");   // rework (2 iterations)
        await AddPrAsync(3, "abandoned", d, d.AddDays(1), author: "dev1");

        await AddIterationsAsync(2, 2); // PR-2: 2 iterations → rework

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        var author = result.AuthorBreakdown.Single();
        Assert.AreEqual("dev1", author.Author);
        Assert.AreEqual(3, author.PrCount);
        Assert.AreEqual(66.7, author.MergePct,   "merge %");
        Assert.AreEqual(33.3, author.AbandonPct, "abandon %");
        Assert.AreEqual(33.3, author.ReworkPct,  "rework %");
    }

    [TestMethod]
    [Description("Author breakdown is empty when no PRs exist in range")]
    public async Task Handle_AuthorBreakdown_EmptyWhenNoPrs()
    {
        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.HasCount(0, result.AuthorBreakdown);
    }

    // ── URL construction tests ────────────────────────────────────────────────

    [TestMethod]
    [Description("Scatter points contain a URL when TFS config is available")]
    public async Task Handle_ScatterPoints_ContainUrlWhenTfsConfigAvailable()
    {
        // Arrange
        _context.TfsConfigs.Add(new PoTool.Shared.Settings.TfsConfigEntity
        {
            Id      = 1,
            Url     = "http://tfsserver/DefaultCollection",
            Project = "MyProject"
        });
        await _context.SaveChangesAsync();

        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(42, "completed", d, d.AddDays(2), repository: "MyRepo");

        var result = await _handler.Handle(MakeQuery(repository: "MyRepo"), CancellationToken.None);

        var point = result.ScatterPoints.Single();
        Assert.AreEqual(
            "http://tfsserver/DefaultCollection/MyProject/_git/MyRepo/pullrequest/42",
            point.Url,
            "Scatter point URL must be constructed from TfsConfig.Url + Project + RepositoryName + Id");
    }

    [TestMethod]
    [Description("Scatter points have no URL when TFS config is unavailable")]
    public async Task Handle_ScatterPoints_NullUrlWhenNoTfsConfig()
    {
        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(10, "completed", d, d.AddDays(1), repository: "Repo-A");

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        Assert.IsNull(result.ScatterPoints.Single().Url,
            "URL must be null when no TFS config is present");
    }

    [TestMethod]
    [Description("Top 3 and longest PR entries include URL when TFS config is available")]
    public async Task Handle_ProblematicEntries_ContainUrlWhenTfsConfigAvailable()
    {
        _context.TfsConfigs.Add(new PoTool.Shared.Settings.TfsConfigEntity
        {
            Id      = 1,
            Url     = "https://dev.azure.com/myorg/",
            Project = "Alpha"
        });
        await _context.SaveChangesAsync();

        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(7, "completed", d, d.AddDays(5), repository: "Repo-B");

        var result = await _handler.Handle(MakeQuery(), CancellationToken.None);

        // Top 3
        Assert.AreEqual(
            "https://dev.azure.com/myorg/Alpha/_git/Repo-B/pullrequest/7",
            result.Top3Problematic.Single().Url,
            "Top 3 entry URL must be constructed correctly (trailing slash stripped from base URL)");

        // Longest PR table
        Assert.AreEqual(
            "https://dev.azure.com/myorg/Alpha/_git/Repo-B/pullrequest/7",
            result.LongestPrs.Single().Url,
            "Longest PR entry URL must match");
    }

    [TestMethod]
    [Description("TFS base URL trailing slash is stripped before URL construction")]
    public async Task Handle_ScatterPoints_UrlStripsTrailingSlashFromBaseUrl()
    {
        _context.TfsConfigs.Add(new PoTool.Shared.Settings.TfsConfigEntity
        {
            Id      = 1,
            Url     = "http://tfs/Collection/",   // trailing slash
            Project = "Proj"
        });
        await _context.SaveChangesAsync();

        var d = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        await AddPrAsync(5, "abandoned", d, d.AddDays(1), repository: "Svc");

        var result = await _handler.Handle(MakeQuery(repository: "Svc"), CancellationToken.None);

        Assert.AreEqual(
            "http://tfs/Collection/Proj/_git/Svc/pullrequest/5",
            result.ScatterPoints.Single().Url,
            "Trailing slash on TFS base URL must not produce double-slash");
    }
}
