using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Filters;
using PoTool.Core.PullRequests.Filters;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class CachedPullRequestReadProviderSqliteTests
{
    private SqliteConnection _connection = null!;
    private PoToolDbContext _context = null!;
    private CachedPullRequestReadProvider _provider = null!;
    private EfPullRequestQueryStore _queryStore = null!;

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
        _provider = new CachedPullRequestReadProvider(_context, NullLogger<CachedPullRequestReadProvider>.Instance);
        _queryStore = new EfPullRequestQueryStore(_context);
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [TestMethod]
    public async Task GetCommentsAsync_WithSqlite_UsesServerSideUtcOrderingAndStableTieBreak()
    {
        var pullRequest = new PullRequestEntity
        {
            Id = 1,
            RepositoryName = "Repo",
            Title = "PR",
            CreatedBy = "User",
            CreatedDate = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero),
            CreatedDateUtc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            Status = "Active",
            IterationPath = "Sprint 1",
            SourceBranch = "feature/test",
            TargetBranch = "main",
            RetrievedAt = DateTimeOffset.UtcNow
        };

        _context.PullRequests.Add(pullRequest);
        _context.PullRequestComments.AddRange(
            new PullRequestCommentEntity
            {
                InternalId = 20,
                Id = 102,
                PullRequestId = 1,
                ThreadId = 1,
                Author = "User 2",
                Content = "Later",
                CreatedDate = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
                CreatedDateUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
                IsResolved = false
            },
            new PullRequestCommentEntity
            {
                InternalId = 11,
                Id = 101,
                PullRequestId = 1,
                ThreadId = 1,
                Author = "User 1",
                Content = "Same time A",
                CreatedDate = new DateTimeOffset(2026, 3, 1, 8, 30, 0, TimeSpan.Zero),
                CreatedDateUtc = new DateTime(2026, 3, 1, 8, 30, 0, DateTimeKind.Utc),
                IsResolved = false
            },
            new PullRequestCommentEntity
            {
                InternalId = 12,
                Id = 103,
                PullRequestId = 1,
                ThreadId = 2,
                Author = "User 3",
                Content = "Same time B",
                CreatedDate = new DateTimeOffset(2026, 3, 1, 8, 30, 0, TimeSpan.Zero),
                CreatedDateUtc = new DateTime(2026, 3, 1, 8, 30, 0, DateTimeKind.Utc),
                IsResolved = false
            });
        await _context.SaveChangesAsync();

        var comments = (await _provider.GetCommentsAsync(1, CancellationToken.None)).ToList();

        CollectionAssert.AreEqual(new[] { 101, 103, 102 }, comments.Select(comment => comment.Id).ToArray());
    }

    [TestMethod]
    public async Task GetMetricsDataAsync_WithSqlite_GroupsByPullRequestAndPreservesOrdering()
    {
        _context.PullRequests.AddRange(
            new PullRequestEntity
            {
                Id = 1,
                RepositoryName = "Repo",
                Title = "PR 1",
                CreatedBy = "User",
                CreatedDate = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero),
                CreatedDateUtc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
                Status = "Active",
                IterationPath = "Sprint 1",
                SourceBranch = "feature/test",
                TargetBranch = "main",
                RetrievedAt = DateTimeOffset.UtcNow
            },
            new PullRequestEntity
            {
                Id = 2,
                RepositoryName = "Repo",
                Title = "PR 2",
                CreatedBy = "User",
                CreatedDate = new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero),
                CreatedDateUtc = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc),
                Status = "Completed",
                IterationPath = "Sprint 1",
                SourceBranch = "feature/test-2",
                TargetBranch = "main",
                RetrievedAt = DateTimeOffset.UtcNow
            });
        _context.PullRequestIterations.AddRange(
            new PullRequestIterationEntity { PullRequestId = 1, IterationNumber = 2, CreatedDate = DateTimeOffset.UtcNow, UpdatedDate = DateTimeOffset.UtcNow },
            new PullRequestIterationEntity { PullRequestId = 1, IterationNumber = 1, CreatedDate = DateTimeOffset.UtcNow.AddHours(-1), UpdatedDate = DateTimeOffset.UtcNow.AddHours(-1) },
            new PullRequestIterationEntity { PullRequestId = 2, IterationNumber = 1, CreatedDate = DateTimeOffset.UtcNow, UpdatedDate = DateTimeOffset.UtcNow });
        _context.PullRequestComments.AddRange(
            new PullRequestCommentEntity
            {
                InternalId = 3,
                Id = 201,
                PullRequestId = 1,
                ThreadId = 10,
                Author = "Reviewer",
                Content = "Later",
                CreatedDate = new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero),
                CreatedDateUtc = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc),
                IsResolved = false
            },
            new PullRequestCommentEntity
            {
                InternalId = 2,
                Id = 200,
                PullRequestId = 1,
                ThreadId = 10,
                Author = "Reviewer",
                Content = "Earlier",
                CreatedDate = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero),
                CreatedDateUtc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
                IsResolved = false
            },
            new PullRequestCommentEntity
            {
                InternalId = 1,
                Id = 300,
                PullRequestId = 2,
                ThreadId = 20,
                Author = "Reviewer",
                Content = "Only",
                CreatedDate = new DateTimeOffset(2026, 3, 3, 8, 0, 0, TimeSpan.Zero),
                CreatedDateUtc = new DateTime(2026, 3, 3, 8, 0, 0, DateTimeKind.Utc),
                IsResolved = true
            });
        _context.PullRequestFileChanges.AddRange(
            new PullRequestFileChangeEntity { PullRequestId = 1, IterationId = 2, FilePath = "/b.cs", ChangeType = "edit", LinesAdded = 1, LinesDeleted = 0, LinesModified = 0 },
            new PullRequestFileChangeEntity { PullRequestId = 1, IterationId = 1, FilePath = "/a.cs", ChangeType = "add", LinesAdded = 2, LinesDeleted = 0, LinesModified = 0 },
            new PullRequestFileChangeEntity { PullRequestId = 2, IterationId = 1, FilePath = "/c.cs", ChangeType = "edit", LinesAdded = 3, LinesDeleted = 1, LinesModified = 0 });
        await _context.SaveChangesAsync();

        var filter = new PullRequestEffectiveFilter(
            new PullRequestFilterContext(
                FilterSelection<int>.All(),
                FilterSelection<int>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterTimeSelection.None()),
            ["Repo"],
            null,
            null,
            null,
            Array.Empty<int>());
        var result = await _queryStore.GetMetricsDataAsync(filter, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 1, 2 }, result.IterationsByPullRequestId[1].Select(iteration => iteration.IterationNumber).ToArray());
        CollectionAssert.AreEqual(new[] { 200, 201 }, result.CommentsByPullRequestId[1].Select(comment => comment.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "/a.cs", "/b.cs" }, result.FileChangesByPullRequestId[1].Select(fileChange => fileChange.FilePath).ToArray());
        CollectionAssert.AreEqual(new[] { 300 }, result.CommentsByPullRequestId[2].Select(comment => comment.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "/c.cs" }, result.FileChangesByPullRequestId[2].Select(fileChange => fileChange.FilePath).ToArray());
    }
}
