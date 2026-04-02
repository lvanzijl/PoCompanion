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

[TestClass]
public sealed class GetPrSprintTrendsQueryHandlerTests
{
    private PoToolDbContext _context = null!;
    private GetPrSprintTrendsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"GetPrSprintTrendsQueryHandlerTests-{Guid.NewGuid()}")
            .Options;

        _context = new PoToolDbContext(options);
        _handler = new GetPrSprintTrendsQueryHandler(
            new EfPullRequestQueryStore(_context),
            new Mock<ILogger<GetPrSprintTrendsQueryHandler>>().Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    public async Task Handle_NoValidSprints_ReturnsSuccessfulEmptyResponse()
    {
        var result = await _handler.Handle(MakeQuery([42]), CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsEmpty(result.Sprints);
    }

    [TestMethod]
    public async Task Handle_ComputesSprintMetrics_FromCachedQueryStoreData()
    {
        var sprintStart = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var sprintEnd = sprintStart.AddDays(14);
        await AddSprintAsync(10, sprintStart, sprintEnd, "Sprint 10");

        await AddPullRequestAsync(1, "alice", sprintStart.AddDays(1), sprintStart.AddDays(3));
        await AddPullRequestAsync(2, "bob", sprintStart.AddDays(2), sprintStart.AddDays(4));
        await AddFileChangeAsync(1, "/src/A.cs", 10, 5);
        await AddFileChangeAsync(2, "/src/B.cs", 30, 10);
        await AddCommentAsync(1, "reviewer", sprintStart.AddDays(1).AddHours(4));
        await AddCommentAsync(2, "reviewer", sprintStart.AddDays(2).AddHours(8));

        var result = await _handler.Handle(MakeQuery([10]), CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.HasCount(1, result.Sprints);

        var sprint = result.Sprints[0];
        Assert.AreEqual(10, sprint.SprintId);
        Assert.AreEqual(2, sprint.TotalPrs);
        Assert.AreEqual(27.5, sprint.MedianPrSize);
        Assert.IsTrue(sprint.PrSizeIsLinesChanged);
        Assert.AreEqual(6.0, sprint.MedianTimeToFirstReviewHours);
        Assert.AreEqual(48.0, sprint.MedianTimeToMergeHours);
        Assert.IsNull(sprint.P90TimeToMergeHours);
    }

    private GetPrSprintTrendsQuery MakeQuery(IReadOnlyList<int> sprintIds)
    {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero);

        return new GetPrSprintTrendsQuery(new PullRequestEffectiveFilter(
            new PullRequestFilterContext(
                FilterSelection<int>.All(),
                FilterSelection<int>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterTimeSelection.DateRange(from, to)),
            ["Repo-A"],
            from,
            to,
            null,
            sprintIds));
    }

    private async Task AddSprintAsync(int sprintId, DateTimeOffset start, DateTimeOffset end, string name)
    {
        PersistenceTestGraph.EnsureTeam(_context, 1);
        _context.Sprints.Add(new SprintEntity
        {
            Id = sprintId,
            Name = name,
            StartDateUtc = start.UtcDateTime,
            EndDateUtc = end.UtcDateTime,
            TeamId = 1
        });

        await _context.SaveChangesAsync();
    }

    private async Task AddPullRequestAsync(int id, string createdBy, DateTimeOffset createdDate, DateTimeOffset completedDate)
    {
        _context.PullRequests.Add(new PullRequestEntity
        {
            Id = id,
            InternalId = id,
            RepositoryName = "Repo-A",
            Title = $"PR-{id}",
            CreatedBy = createdBy,
            CreatedDate = createdDate,
            CreatedDateUtc = createdDate.UtcDateTime,
            CompletedDate = completedDate,
            Status = "completed",
            IterationPath = @"\Team\Sprint 10",
            SourceBranch = "refs/heads/feature",
            TargetBranch = "refs/heads/main",
            RetrievedAt = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    private async Task AddFileChangeAsync(int pullRequestId, string path, int linesAdded, int linesDeleted)
    {
        _context.PullRequestFileChanges.Add(new PullRequestFileChangeEntity
        {
            Id = pullRequestId * 100,
            PullRequestId = pullRequestId,
            IterationId = 1,
            FilePath = path,
            ChangeType = "edit",
            LinesAdded = linesAdded,
            LinesDeleted = linesDeleted,
            LinesModified = 0
        });

        await _context.SaveChangesAsync();
    }

    private async Task AddCommentAsync(int pullRequestId, string author, DateTimeOffset createdDate)
    {
        _context.PullRequestComments.Add(new PullRequestCommentEntity
        {
            Id = pullRequestId * 1000,
            InternalId = pullRequestId * 1000,
            PullRequestId = pullRequestId,
            ThreadId = 1,
            Author = author,
            Content = "review",
            CreatedDate = createdDate,
            CreatedDateUtc = createdDate.UtcDateTime,
            IsResolved = false
        });

        await _context.SaveChangesAsync();
    }
}
