using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Settings;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetHomeProductBarMetricsQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private GetHomeProductBarMetricsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new PoToolDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        _handler = new GetHomeProductBarMetricsQueryHandler(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [TestMethod]
    public async Task Handle_AllProductsAggregatesOpenBugsAndChangesToday()
    {
        // Arrange
        var seed = await SeedDashboardMetricsAsync();

        // Act
        var result = await _handler.Handle(
            new GetHomeProductBarMetricsQuery(
                DeliveryFilterTestFactory.MultiSprint([seed.ProductAId, seed.ProductBId], [])),
            CancellationToken.None);

        // Assert
        Assert.AreEqual(seed.ExpectedSprintProgressPercentage, result.SprintProgressPercentage);
        Assert.AreEqual(2, result.BugCount);
        Assert.AreEqual(2, result.ChangesTodayCount);
    }

    [TestMethod]
    public async Task Handle_SelectedProductFiltersSprintProgressBugAndChangeMetrics()
    {
        // Arrange
        var seed = await SeedDashboardMetricsAsync();

        // Act
        var result = await _handler.Handle(
            new GetHomeProductBarMetricsQuery(
                DeliveryFilterTestFactory.MultiSprint([seed.ProductAId], [])),
            CancellationToken.None);

        // Assert
        Assert.AreEqual(seed.ExpectedSelectedProductSprintProgressPercentage, result.SprintProgressPercentage);
        Assert.AreEqual(1, result.BugCount);
        Assert.AreEqual(1, result.ChangesTodayCount);
    }

    private async Task<(int ProductOwnerId, int ProductAId, int ProductBId, int ExpectedSprintProgressPercentage, int ExpectedSelectedProductSprintProgressPercentage)> SeedDashboardMetricsAsync()
    {
        const int productOwnerId = 7;
        const int productAId = 10;
        const int productBId = 11;
        const int teamAId = 100;
        const int teamBId = 101;
        var now = DateTimeOffset.UtcNow;

        _context.Profiles.Add(new ProfileEntity
        {
            Id = productOwnerId,
            Name = "PO"
        });

        _context.Products.AddRange(
            new ProductEntity
            {
                Id = productAId,
                ProductOwnerId = productOwnerId,
                Name = "Alpha",
                CreatedAt = now,
                LastModified = now
            },
            new ProductEntity
            {
                Id = productBId,
                ProductOwnerId = productOwnerId,
                Name = "Beta",
                CreatedAt = now,
                LastModified = now
            });

        _context.Teams.AddRange(
            new TeamEntity { Id = teamAId, Name = "Team Alpha", TeamAreaPath = "Area/Alpha" },
            new TeamEntity { Id = teamBId, Name = "Team Beta", TeamAreaPath = "Area/Beta" });

        _context.ProductTeamLinks.AddRange(
            new ProductTeamLinkEntity { ProductId = productAId, TeamId = teamAId },
            new ProductTeamLinkEntity { ProductId = productBId, TeamId = teamBId });

        _context.Sprints.AddRange(
            CreateCurrentSprint(1000, teamAId, now.AddDays(-4), now.AddDays(6)),
            CreateCurrentSprint(1001, teamBId, now.AddDays(-8), now.AddDays(2)));

        _context.WorkItemStateClassifications.Add(new WorkItemStateClassificationEntity
        {
            TfsProjectName = "Project",
            WorkItemType = "Bug",
            StateName = "Closed",
            Classification = (int)StateClassification.Done
        });

        _context.WorkItems.AddRange(
            CreateWorkItem(2000, "Bug", "Open Alpha Bug", "Active", now),
            CreateWorkItem(2001, "Bug", "Closed Alpha Bug", "Closed", now),
            CreateWorkItem(2002, "Bug", "Open Beta Bug", "Active", now));

        _context.ResolvedWorkItems.AddRange(
            CreateResolvedWorkItem(2000, productAId),
            CreateResolvedWorkItem(2001, productAId),
            CreateResolvedWorkItem(2002, productBId));

        _context.ActivityEventLedgerEntries.AddRange(
            CreateActivity(productOwnerId, 2000, 1, now.AddHours(-1)),
            CreateActivity(productOwnerId, 2002, 2, now.AddHours(-2)),
            CreateActivity(productOwnerId, 2002, 3, now.AddDays(-1)));

        await _context.SaveChangesAsync();

        return (productOwnerId, productAId, productBId, 60, 40);
    }

    private static SprintEntity CreateCurrentSprint(int id, int teamId, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        return new SprintEntity
        {
            Id = id,
            TeamId = teamId,
            Path = $"Project\\Sprint {id}",
            Name = $"Sprint {id}",
            StartUtc = startUtc,
            StartDateUtc = startUtc.UtcDateTime,
            EndUtc = endUtc,
            EndDateUtc = endUtc.UtcDateTime,
            TimeFrame = "current",
            LastSyncedUtc = DateTimeOffset.UtcNow,
            LastSyncedDateUtc = DateTime.UtcNow
        };
    }

    private static WorkItemEntity CreateWorkItem(int tfsId, string type, string title, string state, DateTimeOffset now)
    {
        return new WorkItemEntity
        {
            TfsId = tfsId,
            Type = type,
            Title = title,
            AreaPath = "Area",
            IterationPath = "Iteration",
            State = state,
            RetrievedAt = now,
            TfsRevision = 1,
            TfsChangedDate = now,
            TfsChangedDateUtc = now.UtcDateTime
        };
    }

    private static ResolvedWorkItemEntity CreateResolvedWorkItem(int workItemId, int productId)
    {
        return new ResolvedWorkItemEntity
        {
            WorkItemId = workItemId,
            WorkItemType = "Bug",
            ResolvedProductId = productId,
            ResolutionStatus = ResolutionStatus.Resolved,
            LastResolvedAt = DateTimeOffset.UtcNow,
            ResolvedAtRevision = 1
        };
    }

    private static ActivityEventLedgerEntryEntity CreateActivity(
        int productOwnerId,
        int workItemId,
        int updateId,
        DateTimeOffset timestamp)
    {
        return new ActivityEventLedgerEntryEntity
        {
            ProductOwnerId = productOwnerId,
            WorkItemId = workItemId,
            UpdateId = updateId,
            FieldRefName = "System.State",
            OldValue = "New",
            NewValue = "Active",
            EventTimestamp = timestamp,
            EventTimestampUtc = timestamp.UtcDateTime
        };
    }
}
