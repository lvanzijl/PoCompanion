using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class CachedPullRequestReadProviderSqliteTests
{
    private SqliteConnection _connection = null!;
    private PoToolDbContext _context = null!;
    private CachedPullRequestReadProvider _provider = null!;

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
}
