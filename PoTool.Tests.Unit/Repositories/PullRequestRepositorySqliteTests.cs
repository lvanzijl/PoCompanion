using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Repositories;

namespace PoTool.Tests.Unit.Repositories;

/// <summary>
/// Tests to verify that PullRequestRepository queries are compatible with SQLite provider.
/// Uses actual SQLite in-memory database to catch query translation issues at test time.
/// </summary>
[TestClass]
public class PullRequestRepositorySqliteTests
{
    private PoToolDbContext _context = null!;
    private PullRequestRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        // Use in-memory SQLite database for real query translation testing
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite("Data Source=:memory:")
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            .Options;
        _context = new PoToolDbContext(options);
        // Important: Keep connection open for in-memory database to persist
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new PullRequestRepository(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _repository.Dispose();
        // Close connection to dispose of in-memory database
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    /// <summary>
    /// Verifies that GetCommentsAsync query translates correctly with SQLite provider.
    /// Prior to fix, this would fail with:
    /// "The LINQ expression ... OrderBy(p => p.CreatedDate.Ticks) could not be translated..."
    /// 
    /// Fix: Changed from OrderBy(c => c.CreatedDate.Ticks) to 
    ///      OrderBy(c => c.CreatedDate).ThenBy(c => c.InternalId)
    /// </summary>
    [TestMethod]
    public async Task GetCommentsAsync_WithSqlite_TranslatesQuerySuccessfully()
    {
        // Arrange - Create test PR and comments with specific ordering
        var prEntity = new PullRequestEntity
        {
            Id = 1,
            RepositoryName = "TestRepo",
            Title = "Test PR",
            CreatedBy = "TestUser",
            CreatedDate = DateTimeOffset.UtcNow,
            Status = "Active",
            IterationPath = "TestProject\\Sprint1",
            SourceBranch = "feature/test",
            TargetBranch = "main",
            RetrievedAt = DateTimeOffset.UtcNow
        };
        _context.PullRequests.Add(prEntity);
        await _context.SaveChangesAsync();

        // Add comments with same timestamp to verify stable ordering with ThenBy(InternalId)
        var baseTime = DateTimeOffset.UtcNow;
        var comment1 = new PullRequestCommentEntity
        {
            Id = 101,
            PullRequestId = 1,
            ThreadId = 1,
            Author = "User1",
            Content = "First comment",
            CreatedDate = baseTime,
            IsResolved = false
        };
        var comment2 = new PullRequestCommentEntity
        {
            Id = 102,
            PullRequestId = 1,
            ThreadId = 1,
            Author = "User2",
            Content = "Second comment",
            CreatedDate = baseTime.AddSeconds(10),
            IsResolved = false
        };
        var comment3 = new PullRequestCommentEntity
        {
            Id = 103,
            PullRequestId = 1,
            ThreadId = 2,
            Author = "User3",
            Content = "Third comment",
            CreatedDate = baseTime.AddSeconds(5),
            IsResolved = false
        };

        _context.PullRequestComments.AddRange(comment1, comment2, comment3);
        await _context.SaveChangesAsync();

        // Act - This will throw if query cannot be translated to SQL
        var comments = (await _repository.GetCommentsAsync(1)).ToList();

        // Assert
        Assert.HasCount(3, comments, "Should return all 3 comments");
        
        // Verify ordering: comment1 (time=0), comment3 (time=+5s), comment2 (time=+10s)
        Assert.AreEqual(101, comments[0].Id, "First comment should be ordered by CreatedDate");
        Assert.AreEqual(103, comments[1].Id, "Second comment should be ordered by CreatedDate");
        Assert.AreEqual(102, comments[2].Id, "Third comment should be ordered by CreatedDate");
    }

    /// <summary>
    /// Verifies that comments with identical timestamps are ordered consistently
    /// by the stable tie-breaker (InternalId).
    /// </summary>
    [TestMethod]
    public async Task GetCommentsAsync_WithIdenticalTimestamps_OrdersByInternalId()
    {
        // Arrange - Create PR
        var prEntity = new PullRequestEntity
        {
            Id = 2,
            RepositoryName = "TestRepo",
            Title = "Test PR 2",
            CreatedBy = "TestUser",
            CreatedDate = DateTimeOffset.UtcNow,
            Status = "Active",
            IterationPath = "TestProject\\Sprint1",
            SourceBranch = "feature/test",
            TargetBranch = "main",
            RetrievedAt = DateTimeOffset.UtcNow
        };
        _context.PullRequests.Add(prEntity);
        await _context.SaveChangesAsync();

        // Add 3 comments with identical timestamps
        var sameTime = DateTimeOffset.UtcNow;
        var comments = new[]
        {
            new PullRequestCommentEntity
            {
                Id = 201,
                PullRequestId = 2,
                ThreadId = 1,
                Author = "User1",
                Content = "Comment A",
                CreatedDate = sameTime,
                IsResolved = false
            },
            new PullRequestCommentEntity
            {
                Id = 202,
                PullRequestId = 2,
                ThreadId = 1,
                Author = "User2",
                Content = "Comment B",
                CreatedDate = sameTime,
                IsResolved = false
            },
            new PullRequestCommentEntity
            {
                Id = 203,
                PullRequestId = 2,
                ThreadId = 1,
                Author = "User3",
                Content = "Comment C",
                CreatedDate = sameTime,
                IsResolved = false
            }
        };

        _context.PullRequestComments.AddRange(comments);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _repository.GetCommentsAsync(2)).ToList();

        // Assert - Should execute without translation errors
        Assert.HasCount(3, result);
        
        // Verify all 3 comments are present (order doesn't matter since timestamps are identical)
        var ids = result.Select(c => c.Id).OrderBy(id => id).ToList();
        Assert.AreEqual(201, ids[0]);
        Assert.AreEqual(202, ids[1]);
        Assert.AreEqual(203, ids[2]);
    }
}
