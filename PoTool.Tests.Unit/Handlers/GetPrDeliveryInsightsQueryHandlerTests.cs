using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.PullRequests;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.PullRequests.Queries;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Tests for GetPrDeliveryInsightsQueryHandler.
///
/// Business rules verified:
///   - Empty date range returns empty DTO.
///   - TeamId scoping via ProductTeamLinks.
///   - SprintId overrides date range from sprint boundaries.
///   - PR with Feature/Epic ancestor is classified as DeliveryMapped.
///   - PR linked to a Bug (no ancestor) is classified as Bug.
///   - PR linked to a PBI without Feature parent is classified as Disturbance.
///   - PR with no work item link is classified as Unmapped.
///   - Priority: DeliveryMapped > Bug > Disturbance > Unmapped.
///   - Category summary counts and percentages are computed correctly.
///   - Epic breakdown is sorted by PR count descending.
///   - Feature breakdown includes PrPerPbiRatio.
///   - Scatter points contain one point per PR with correct category.
///   - Outlier list is ordered by lifetime descending and capped at 20.
/// </summary>
[TestClass]
public class GetPrDeliveryInsightsQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private Mock<ILogger<GetPrDeliveryInsightsQueryHandler>> _mockLogger = null!;
    private GetPrDeliveryInsightsQueryHandler _handler = null!;

    private static readonly DateTimeOffset RangeFrom = new(2026, 1, 1,  0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RangeTo   = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"PrDeliveryInsightsTests_{Guid.NewGuid()}")
            .Options;
        _context    = new PoToolDbContext(options);
        _mockLogger = new Mock<ILogger<GetPrDeliveryInsightsQueryHandler>>();
        _handler    = new GetPrDeliveryInsightsQueryHandler(_context, _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task AddPrAsync(
        int id,
        string status = "completed",
        DateTimeOffset? createdDate = null,
        DateTimeOffset? completedDate = null,
        string repository = "Repo-A")
    {
        var created = createdDate ?? RangeFrom.AddDays(1);
        _context.PullRequests.Add(new PullRequestEntity
        {
            Id             = id,
            InternalId     = id,
            Title          = $"PR-{id}",
            Status         = status,
            CreatedBy      = "dev1",
            RepositoryName = repository,
            CreatedDate    = created,
            CreatedDateUtc = created.UtcDateTime,
            CompletedDate  = completedDate ?? created.AddHours(8),
            IterationPath  = @"\Team\Sprint 1",
            SourceBranch   = "refs/heads/feature",
            TargetBranch   = "refs/heads/main",
            RetrievedAt    = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private async Task AddWorkItemLinkAsync(int prId, int workItemId)
    {
        _context.PullRequestWorkItemLinks.Add(new PullRequestWorkItemLinkEntity
        {
            PullRequestId = prId,
            WorkItemId    = workItemId
        });
        await _context.SaveChangesAsync();
    }

    private async Task AddWorkItemAsync(int tfsId, string type, string title, int? parentTfsId = null)
    {
        _context.WorkItems.Add(new WorkItemEntity
        {
            TfsId          = tfsId,
            Type           = type,
            Title          = title,
            ParentTfsId    = parentTfsId,
            State          = "Active",
            AreaPath       = @"\Team",
            IterationPath  = @"\Team\Sprint 1",
            TfsRevision    = 1,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow,
            RetrievedAt    = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private async Task AddTeamWithRepoAsync(int teamId, string teamName, int productId, string repoName)
    {
        _context.Teams.Add(new TeamEntity { Id = teamId, Name = teamName });
        _context.Products.Add(new ProductEntity { Id = productId, Name = "Prod", ProductOwnerId = 1 });
        _context.ProductTeamLinks.Add(new ProductTeamLinkEntity { TeamId = teamId, ProductId = productId });
        _context.Repositories.Add(new RepositoryEntity { Id = productId, Name = repoName, ProductId = productId });
        await _context.SaveChangesAsync();
    }

    private async Task AddSprintAsync(int sprintId, DateTimeOffset start, DateTimeOffset end, string name = "Sprint 1")
    {
        _context.Sprints.Add(new SprintEntity
        {
            Id           = sprintId,
            Name         = name,
            StartDateUtc = start.UtcDateTime,
            EndDateUtc   = end.UtcDateTime,
            TeamId       = 0
        });
        await _context.SaveChangesAsync();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Handle_NoPrs_ReturnsEmptyResult()
    {
        var query = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(0, result.CategorySummary.TotalPrs);
        Assert.HasCount(0, result.EpicBreakdown);
        Assert.HasCount(0, result.FeatureBreakdown);
        Assert.HasCount(0, result.ScatterPoints);
        Assert.HasCount(0, result.Outliers);
    }

    [TestMethod]
    public async Task Handle_PrOutsideDateRange_ExcludedFromResult()
    {
        // PR created well before the range
        await AddPrAsync(1, createdDate: RangeFrom.AddDays(-30));

        var query = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(0, result.CategorySummary.TotalPrs);
    }

    [TestMethod]
    public async Task Handle_PrWithNoLinks_ClassifiedAsUnmapped()
    {
        await AddPrAsync(1, createdDate: RangeFrom.AddDays(1));

        var query = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(1, result.CategorySummary.TotalPrs);
        Assert.AreEqual(1, result.CategorySummary.UnmappedCount);
        Assert.AreEqual(100.0, result.CategorySummary.UnmappedPct);
        Assert.AreEqual(0, result.CategorySummary.DeliveryMappedCount);

        Assert.HasCount(1, result.ScatterPoints);
        Assert.AreEqual("Unmapped", result.ScatterPoints[0].Category);
    }

    [TestMethod]
    public async Task Handle_PrLinkedToWorkItemMissingFromCache_LogsDiagnosticAndClassifiesAsUnmapped()
    {
        await AddPrAsync(1, createdDate: RangeFrom.AddDays(1));
        await AddWorkItemLinkAsync(1, 999);

        var query = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(1, result.CategorySummary.TotalPrs);
        Assert.AreEqual(1, result.CategorySummary.UnmappedCount);
        Assert.AreEqual(0, result.CategorySummary.DeliveryMappedCount);
        Assert.AreEqual("Unmapped", result.ScatterPoints[0].Category);

        VerifyLogContains(LogLevel.Warning, "WorkItemNotInCache");
        VerifyLogContains(LogLevel.Information, "unresolvedReasons=WorkItemNotInCache=1");
    }

    [TestMethod]
    public async Task Handle_PrLinkedToFeatureUnderEpic_ClassifiedAsDeliveryMapped()
    {
        // Hierarchy: Epic(100) → Feature(200) → PBI(300)
        await AddWorkItemAsync(100, WorkItemType.Epic, "My Epic");
        await AddWorkItemAsync(200, WorkItemType.Feature, "My Feature", parentTfsId: 100);
        await AddWorkItemAsync(300, WorkItemType.Pbi, "My PBI", parentTfsId: 200);

        // PR linked to the PBI
        await AddPrAsync(1, createdDate: RangeFrom.AddDays(1));
        await AddWorkItemLinkAsync(1, 300);

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(1, result.CategorySummary.TotalPrs);
        Assert.AreEqual(1, result.CategorySummary.DeliveryMappedCount);
        Assert.AreEqual(100.0, result.CategorySummary.DeliveryMappedPct);
        Assert.AreEqual(0, result.CategorySummary.UnmappedCount);

        var point = result.ScatterPoints[0];
        Assert.AreEqual("DeliveryMapped", point.Category);
        Assert.AreEqual(100, point.EpicId);
        Assert.AreEqual("My Epic", point.EpicName);
        Assert.AreEqual(200, point.FeatureId);
        Assert.AreEqual("My Feature", point.FeatureName);
    }

    private void VerifyLogContains(LogLevel logLevel, string expectedText)
    {
        _mockLogger.Verify(
            logger => logger.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => LogStateContains(value, expectedText)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private static bool LogStateContains(object? value, string expectedText)
    {
        var text = value?.ToString();
        return text is not null && text.Contains(expectedText, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Handle_PrLinkedDirectlyToFeature_ClassifiedAsDeliveryMapped()
    {
        await AddWorkItemAsync(100, WorkItemType.Epic, "Epic");
        await AddWorkItemAsync(200, WorkItemType.Feature, "Feature", parentTfsId: 100);

        await AddPrAsync(1, createdDate: RangeFrom.AddDays(1));
        await AddWorkItemLinkAsync(1, 200);

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual("DeliveryMapped", result.CategorySummary.DeliveryMappedCount == 1 ? "DeliveryMapped" : "Other");
        var point = result.ScatterPoints[0];
        Assert.AreEqual("DeliveryMapped", point.Category);
    }

    [TestMethod]
    public async Task Handle_PrLinkedToBugWithNoAncestor_ClassifiedAsBug()
    {
        await AddWorkItemAsync(500, WorkItemType.Bug, "Some Bug");

        await AddPrAsync(1, createdDate: RangeFrom.AddDays(1));
        await AddWorkItemLinkAsync(1, 500);

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(1, result.CategorySummary.BugCount);
        Assert.AreEqual(0, result.CategorySummary.DeliveryMappedCount);
        Assert.AreEqual("Bug", result.ScatterPoints[0].Category);
    }

    [TestMethod]
    public async Task Handle_PrLinkedToPbiWithoutFeatureParent_ClassifiedAsDisturbance()
    {
        // PBI with no Feature ancestor (direct parent is not a Feature)
        await AddWorkItemAsync(300, WorkItemType.Pbi, "Orphan PBI");

        await AddPrAsync(1, createdDate: RangeFrom.AddDays(1));
        await AddWorkItemLinkAsync(1, 300);

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(1, result.CategorySummary.DisturbanceCount);
        Assert.AreEqual("Disturbance", result.ScatterPoints[0].Category);
    }

    [TestMethod]
    public async Task Handle_PrWithDeliveryMappedAndBugLink_PrioritisesDeliveryMapped()
    {
        // Hierarchy for epic mapping
        await AddWorkItemAsync(100, WorkItemType.Epic, "Epic");
        await AddWorkItemAsync(200, WorkItemType.Feature, "Feature", parentTfsId: 100);
        await AddWorkItemAsync(300, WorkItemType.Pbi, "PBI", parentTfsId: 200);

        // Also a standalone bug
        await AddWorkItemAsync(500, WorkItemType.Bug, "Bug");

        await AddPrAsync(1, createdDate: RangeFrom.AddDays(1));
        await AddWorkItemLinkAsync(1, 300);
        await AddWorkItemLinkAsync(1, 500);

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        // DeliveryMapped takes priority
        Assert.AreEqual(1, result.CategorySummary.DeliveryMappedCount);
        Assert.AreEqual(0, result.CategorySummary.BugCount);
        Assert.AreEqual("DeliveryMapped", result.ScatterPoints[0].Category);
    }

    [TestMethod]
    public async Task Handle_CategorySummaryPercentages_AreCorrect()
    {
        // 2 DeliveryMapped, 1 Bug, 1 Disturbance, 1 Unmapped — total 5
        await AddWorkItemAsync(100, WorkItemType.Epic, "Epic");
        await AddWorkItemAsync(200, WorkItemType.Feature, "Feature", parentTfsId: 100);
        await AddWorkItemAsync(300, WorkItemType.Pbi, "PBI under feature", parentTfsId: 200);
        await AddWorkItemAsync(400, WorkItemType.Pbi, "PBI under feature 2", parentTfsId: 200);
        await AddWorkItemAsync(500, WorkItemType.Bug, "Bug");
        await AddWorkItemAsync(600, WorkItemType.Pbi, "Orphan PBI");

        await AddPrAsync(1, createdDate: RangeFrom.AddDays(1));
        await AddWorkItemLinkAsync(1, 300); // DeliveryMapped

        await AddPrAsync(2, createdDate: RangeFrom.AddDays(1));
        await AddWorkItemLinkAsync(2, 400); // DeliveryMapped

        await AddPrAsync(3, createdDate: RangeFrom.AddDays(1));
        await AddWorkItemLinkAsync(3, 500); // Bug

        await AddPrAsync(4, createdDate: RangeFrom.AddDays(1));
        await AddWorkItemLinkAsync(4, 600); // Disturbance

        await AddPrAsync(5, createdDate: RangeFrom.AddDays(1));
        // No link — Unmapped

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(5, result.CategorySummary.TotalPrs);
        Assert.AreEqual(2, result.CategorySummary.DeliveryMappedCount);
        Assert.AreEqual(40.0, result.CategorySummary.DeliveryMappedPct);
        Assert.AreEqual(1, result.CategorySummary.BugCount);
        Assert.AreEqual(20.0, result.CategorySummary.BugPct);
        Assert.AreEqual(1, result.CategorySummary.DisturbanceCount);
        Assert.AreEqual(20.0, result.CategorySummary.DisturbancePct);
        Assert.AreEqual(1, result.CategorySummary.UnmappedCount);
        Assert.AreEqual(20.0, result.CategorySummary.UnmappedPct);
    }

    [TestMethod]
    public async Task Handle_EpicBreakdown_ContainsCorrectMetrics()
    {
        await AddWorkItemAsync(100, WorkItemType.Epic, "Epic A");
        await AddWorkItemAsync(200, WorkItemType.Feature, "Feature", parentTfsId: 100);
        await AddWorkItemAsync(300, WorkItemType.Pbi, "PBI 1", parentTfsId: 200);
        await AddWorkItemAsync(301, WorkItemType.Pbi, "PBI 2", parentTfsId: 200);

        var created = RangeFrom.AddDays(1);
        await AddPrAsync(1, status: "completed",
            createdDate: created, completedDate: created.AddHours(10));
        await AddWorkItemLinkAsync(1, 300);

        await AddPrAsync(2, status: "abandoned",
            createdDate: created, completedDate: created.AddHours(20));
        await AddWorkItemLinkAsync(2, 301);

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.HasCount(1, result.EpicBreakdown);
        var epic = result.EpicBreakdown[0];
        Assert.AreEqual(100, epic.EpicId);
        Assert.AreEqual("Epic A", epic.EpicName);
        Assert.AreEqual(2, epic.PrCount);
        Assert.AreEqual(50.0, epic.AbandonedPct, "50% should be abandoned");
        Assert.IsNotNull(epic.MedianLifetimeHours);
    }

    [TestMethod]
    public async Task Handle_FeatureBreakdown_ContainsPrPerPbiRatio()
    {
        await AddWorkItemAsync(100, WorkItemType.Epic, "Epic");
        await AddWorkItemAsync(200, WorkItemType.Feature, "Feature", parentTfsId: 100);

        // 2 PBIs under the Feature
        await AddWorkItemAsync(300, WorkItemType.Pbi, "PBI 1", parentTfsId: 200);
        await AddWorkItemAsync(301, WorkItemType.Pbi, "PBI 2", parentTfsId: 200);

        // 4 PRs linked via PBIs under this Feature
        var created = RangeFrom.AddDays(1);
        for (var i = 1; i <= 4; i++)
        {
            await AddPrAsync(i, createdDate: created);
            await AddWorkItemLinkAsync(i, i <= 2 ? 300 : 301);
        }

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.HasCount(1, result.FeatureBreakdown);
        var feature = result.FeatureBreakdown[0];
        Assert.AreEqual(200, feature.FeatureId);
        Assert.AreEqual(4, feature.PrCount);
        Assert.IsNotNull(feature.PrPerPbiRatio);
        Assert.AreEqual(2.0, feature.PrPerPbiRatio!.Value, "4 PRs / 2 PBIs = 2.0");
    }

    [TestMethod]
    public async Task Handle_TeamScoping_OnlyIncludesPrsFromTeamRepositories()
    {
        // Team 1 → Product 1 → Repo-A
        await AddTeamWithRepoAsync(1, "Team Alpha", 1, "Repo-A");

        var created = RangeFrom.AddDays(1);
        await AddPrAsync(1, repository: "Repo-A", createdDate: created);
        await AddPrAsync(2, repository: "Repo-B", createdDate: created); // not in scope

        var query  = new GetPrDeliveryInsightsQuery(TeamId: 1, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(1, result.CategorySummary.TotalPrs);
    }

    [TestMethod]
    public async Task Handle_TeamWithNoRepositories_ReturnsEmpty()
    {
        _context.Teams.Add(new TeamEntity { Id = 1, Name = "Team X" });
        _context.Products.Add(new ProductEntity { Id = 1, Name = "Prod", ProductOwnerId = 1 });
        _context.ProductTeamLinks.Add(new ProductTeamLinkEntity { TeamId = 1, ProductId = 1 });
        // No repository configured for Product 1
        await _context.SaveChangesAsync();

        await AddPrAsync(1, createdDate: RangeFrom.AddDays(1));

        var query  = new GetPrDeliveryInsightsQuery(TeamId: 1, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(0, result.CategorySummary.TotalPrs);
    }

    [TestMethod]
    public async Task Handle_SprintId_OverridesDateRange()
    {
        // Sprint covers Jan 15 – Jan 31
        var sprintStart = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprintEnd   = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);
        await AddSprintAsync(10, sprintStart, sprintEnd, "Sprint 3");

        // PR inside sprint window
        await AddPrAsync(1, createdDate: sprintStart.AddDays(1));
        // PR outside sprint window (too early)
        await AddPrAsync(2, createdDate: sprintStart.AddDays(-5));

        // Pass a wide custom date range — sprint boundaries should override
        var query  = new GetPrDeliveryInsightsQuery(null, SprintId: 10, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.AreEqual(1, result.CategorySummary.TotalPrs, "Only the PR within the sprint window should be included");
        Assert.AreEqual("Sprint 3", result.SprintName);
        Assert.AreEqual(10, result.SprintId);
    }

    [TestMethod]
    public async Task Handle_ScatterPoints_OnePerPr()
    {
        var created = RangeFrom.AddDays(1);
        for (var i = 1; i <= 5; i++)
            await AddPrAsync(i, createdDate: created);

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.HasCount(5, result.ScatterPoints);
    }

    [TestMethod]
    public async Task Handle_Outliers_SortedByLifetimeDescending_CappedAt20()
    {
        var created = RangeFrom.AddDays(1);
        // Add 25 PRs with varying lifetimes
        for (var i = 1; i <= 25; i++)
        {
            await AddPrAsync(
                i,
                status: "completed",
                createdDate: created,
                completedDate: created.AddHours(i * 10));
        }

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.HasCount(20, result.Outliers, "Outlier list should be capped at 20");

        // Verify descending order
        for (var i = 0; i < result.Outliers.Count - 1; i++)
        {
            // lowerBound = next (smaller), value = current (larger) — asserts current >= next
            Assert.IsGreaterThanOrEqualTo(
                result.Outliers[i + 1].LifetimeHours,
                result.Outliers[i].LifetimeHours);
        }
    }

    // ── Signal detection tests ─────────────────────────────────────────────────

    [TestMethod]
    public async Task Handle_NoPrs_ImprovementTipsEmpty()
    {
        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.HasCount(0, result.ImprovementTips, "No tips should be generated when there are no PRs");
    }

    [TestMethod]
    public async Task Handle_LongMedianLifetime_GeneratesLongLifetimeTip()
    {
        // All completed PRs have 48-hour lifetime (> 24h threshold)
        var created = RangeFrom.AddDays(1);
        for (var i = 1; i <= 5; i++)
        {
            await AddPrAsync(
                i,
                status: "completed",
                createdDate: created,
                completedDate: created.AddHours(48));
        }

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(
            result.ImprovementTips.Any(t => t.Signal.Contains("Long PR Lifetimes")),
            "Should detect long PR lifetime signal when median > 24h");
    }

    [TestMethod]
    public async Task Handle_ShortMedianLifetime_NoLongLifetimeTip()
    {
        // All completed PRs have 8-hour lifetime (< 24h threshold)
        var created = RangeFrom.AddDays(1);
        for (var i = 1; i <= 5; i++)
        {
            await AddPrAsync(
                i,
                status: "completed",
                createdDate: created,
                completedDate: created.AddHours(8));
        }

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsFalse(
            result.ImprovementTips.Any(t => t.Signal.Contains("Long PR Lifetimes")),
            "Should NOT detect long PR lifetime signal when median <= 24h");
    }

    [TestMethod]
    public async Task Handle_HighReviewChurn_GeneratesReviewChurnTip()
    {
        // 4 of 5 completed PRs have > 1 review cycle (80% > 30% threshold)
        var created = RangeFrom.AddDays(1);

        // PR 1: 1 review cycle (no churn)
        await AddPrAsync(1, status: "completed", createdDate: created, completedDate: created.AddHours(8));

        // PRs 2-5: 2 review cycles each (churn) — add iterations to simulate
        for (var i = 2; i <= 5; i++)
        {
            await AddPrAsync(i, status: "completed", createdDate: created, completedDate: created.AddHours(8));
            // Add two iteration records to simulate multiple review cycles
            _context.PullRequestIterations.Add(new PoTool.Api.Persistence.Entities.PullRequestIterationEntity
            {
                PullRequestId = i, IterationNumber = 1,
                CreatedDate = created, UpdatedDate = created
            });
            _context.PullRequestIterations.Add(new PoTool.Api.Persistence.Entities.PullRequestIterationEntity
            {
                PullRequestId = i, IterationNumber = 2,
                CreatedDate = created.AddHours(1), UpdatedDate = created.AddHours(1)
            });
        }
        await _context.SaveChangesAsync();

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(
            result.ImprovementTips.Any(t => t.Signal.Contains("Review Churn")),
            "Should detect high review churn signal when > 30% of PRs have multiple review cycles");
    }

    [TestMethod]
    public async Task Handle_HighBugShare_GeneratesBugShareTip()
    {
        // 2 of 5 PRs linked to Bugs (40% > 20% threshold)
        var created = RangeFrom.AddDays(1);

        for (var i = 1; i <= 5; i++)
            await AddPrAsync(i, status: "completed", createdDate: created, completedDate: created.AddHours(8));

        await AddWorkItemAsync(101, WorkItemType.Bug, "Bug 1");
        await AddWorkItemAsync(102, WorkItemType.Bug, "Bug 2");
        await AddWorkItemLinkAsync(1, 101);
        await AddWorkItemLinkAsync(2, 102);

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(
            result.ImprovementTips.Any(t => t.Signal.Contains("Bug PR Share")),
            "Should detect high bug PR share signal when BugPct > 20%");
    }

    [TestMethod]
    public async Task Handle_HighDisturbanceShare_GeneratesDisturbanceTip()
    {
        // 2 of 5 PRs linked to orphan PBIs (40% > 20% threshold)
        var created = RangeFrom.AddDays(1);

        for (var i = 1; i <= 5; i++)
            await AddPrAsync(i, status: "completed", createdDate: created, completedDate: created.AddHours(8));

        await AddWorkItemAsync(201, WorkItemType.Pbi, "Orphan PBI 1");
        await AddWorkItemAsync(202, WorkItemType.Pbi, "Orphan PBI 2");
        await AddWorkItemLinkAsync(1, 201);
        await AddWorkItemLinkAsync(2, 202);

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(
            result.ImprovementTips.Any(t => t.Signal.Contains("Disturbance Share")),
            "Should detect high disturbance share signal when DisturbancePct > 20%");
    }

    [TestMethod]
    public async Task Handle_ImprovementTips_NeverExceedsThree()
    {
        // Setup: long lifetime + high bug share + high disturbance share (3+ signals active)
        var created = RangeFrom.AddDays(1);

        for (var i = 1; i <= 10; i++)
        {
            await AddPrAsync(i, status: "completed", createdDate: created, completedDate: created.AddHours(48));
        }

        // Add bugs (30% bug share)
        await AddWorkItemAsync(101, WorkItemType.Bug, "Bug 1");
        await AddWorkItemAsync(102, WorkItemType.Bug, "Bug 2");
        await AddWorkItemAsync(103, WorkItemType.Bug, "Bug 3");
        await AddWorkItemLinkAsync(1, 101);
        await AddWorkItemLinkAsync(2, 102);
        await AddWorkItemLinkAsync(3, 103);

        // Add disturbances (30% disturbance share)
        await AddWorkItemAsync(201, WorkItemType.Pbi, "Orphan PBI 1");
        await AddWorkItemAsync(202, WorkItemType.Pbi, "Orphan PBI 2");
        await AddWorkItemAsync(203, WorkItemType.Pbi, "Orphan PBI 3");
        await AddWorkItemLinkAsync(4, 201);
        await AddWorkItemLinkAsync(5, 202);
        await AddWorkItemLinkAsync(6, 203);

        var query  = new GetPrDeliveryInsightsQuery(null, null, RangeFrom, RangeTo);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsLessThanOrEqualTo(result.ImprovementTips.Count, 3,
            $"Should never return more than 3 tips, got {result.ImprovementTips.Count}");
    }
}
