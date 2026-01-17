using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Repositories;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Repositories;

/// <summary>
/// Tests for timeframe iteration assignment in PullRequestRepository.
/// </summary>
[TestClass]
public class PullRequestRepositoryTimeframeTests
{
    private PoToolDbContext _context = null!;
    private PullRequestRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        // Use in-memory SQLite database for real query translation testing
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite("Data Source=:memory:")
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

    [TestMethod]
    public async Task GetOrCreateTimeframeIterationIdAsync_NewIteration_CreatesIteration()
    {
        // Arrange
        var date = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero); // Wednesday in Week 3

        // Act
        var iterationId = await _repository.GetOrCreateTimeframeIterationIdAsync(date);

        // Assert
        // Check that the iteration was created and has a valid ID
        var iterations = await _context.TimeframeIterations.ToListAsync();
        Assert.HasCount(1, iterations);
        Assert.AreEqual(2025, iterations[0].Year);
        Assert.AreEqual(3, iterations[0].WeekNumber);
        Assert.AreEqual("2025-W03", iterations[0].IterationKey);
        Assert.AreEqual(iterations[0].Id, iterationId);
    }

    [TestMethod]
    public async Task GetOrCreateTimeframeIterationIdAsync_ExistingIteration_ReturnsExisting()
    {
        // Arrange
        var date1 = new DateTimeOffset(2025, 1, 13, 8, 0, 0, TimeSpan.Zero); // Monday
        var date2 = new DateTimeOffset(2025, 1, 17, 18, 0, 0, TimeSpan.Zero); // Friday, same week

        // Act
        var iterationId1 = await _repository.GetOrCreateTimeframeIterationIdAsync(date1);
        var iterationId2 = await _repository.GetOrCreateTimeframeIterationIdAsync(date2);

        // Assert
        Assert.AreEqual(iterationId1, iterationId2, "Same week should return same iteration ID");
        
        var iterations = await _context.TimeframeIterations.ToListAsync();
        Assert.HasCount(1, iterations, "Should only create one iteration for same week");
    }

    [TestMethod]
    public async Task SaveBulkAsync_AssignsTimeframeIterations()
    {
        // Arrange
        var pr1 = new PullRequestDto(
            Id: 1,
            RepositoryName: "TestRepo",
            Title: "PR 1",
            CreatedBy: "User1",
            CreatedDate: new DateTimeOffset(2025, 1, 6, 10, 0, 0, TimeSpan.Zero), // Week 2
            CompletedDate: null,
            Status: "Active",
            IterationPath: "Project\\Sprint1",
            SourceBranch: "feature/1",
            TargetBranch: "main",
            RetrievedAt: DateTimeOffset.UtcNow
        );

        var pr2 = new PullRequestDto(
            Id: 2,
            RepositoryName: "TestRepo",
            Title: "PR 2",
            CreatedBy: "User2",
            CreatedDate: new DateTimeOffset(2025, 1, 15, 14, 0, 0, TimeSpan.Zero), // Week 3
            CompletedDate: null,
            Status: "Active",
            IterationPath: "Project\\Sprint1",
            SourceBranch: "feature/2",
            TargetBranch: "main",
            RetrievedAt: DateTimeOffset.UtcNow
        );

        // Act
        await _repository.SaveBulkAsync(
            new[] { pr1, pr2 },
            Enumerable.Empty<PullRequestIterationDto>(),
            Enumerable.Empty<PullRequestCommentDto>(),
            Enumerable.Empty<PullRequestFileChangeDto>());

        // Assert
        var savedPrs = await _context.PullRequests.ToListAsync();
        Assert.HasCount(2, savedPrs);
        
        Assert.IsNotNull(savedPrs[0].TimeframeIterationId, "PR1 should have timeframe iteration assigned");
        Assert.IsNotNull(savedPrs[1].TimeframeIterationId, "PR2 should have timeframe iteration assigned");
        Assert.AreNotEqual(savedPrs[0].TimeframeIterationId, savedPrs[1].TimeframeIterationId, 
            "PRs in different weeks should have different iterations");

        var iterations = await _context.TimeframeIterations.ToListAsync();
        Assert.HasCount(2, iterations, "Should create 2 iterations for 2 different weeks");
    }

    [TestMethod]
    public async Task BackfillTimeframeIterationsAsync_AssignsToExistingPRs()
    {
        // Arrange - Add PRs without timeframe iterations
        var pr1 = new PullRequestEntity
        {
            Id = 1,
            RepositoryName = "TestRepo",
            Title = "Old PR 1",
            CreatedBy = "User1",
            CreatedDate = new DateTimeOffset(2025, 1, 6, 10, 0, 0, TimeSpan.Zero),
            Status = "Active",
            IterationPath = "Project\\Sprint1",
            SourceBranch = "feature/1",
            TargetBranch = "main",
            RetrievedAt = DateTimeOffset.UtcNow,
            TimeframeIterationId = null // Not assigned yet
        };

        var pr2 = new PullRequestEntity
        {
            Id = 2,
            RepositoryName = "TestRepo",
            Title = "Old PR 2",
            CreatedBy = "User2",
            CreatedDate = new DateTimeOffset(2025, 1, 8, 14, 0, 0, TimeSpan.Zero),
            Status = "Active",
            IterationPath = "Project\\Sprint1",
            SourceBranch = "feature/2",
            TargetBranch = "main",
            RetrievedAt = DateTimeOffset.UtcNow,
            TimeframeIterationId = null // Not assigned yet
        };

        _context.PullRequests.AddRange(pr1, pr2);
        await _context.SaveChangesAsync();

        // Act
        await _repository.BackfillTimeframeIterationsAsync();

        // Assert
        var updatedPrs = await _context.PullRequests.ToListAsync();
        Assert.IsTrue(updatedPrs.All(pr => pr.TimeframeIterationId.HasValue), 
            "All PRs should have timeframe iterations after backfill");
        
        // Both PRs are in same week, so should have same iteration
        Assert.AreEqual(updatedPrs[0].TimeframeIterationId, updatedPrs[1].TimeframeIterationId,
            "PRs in same week should have same iteration");

        var iterations = await _context.TimeframeIterations.ToListAsync();
        Assert.HasCount(1, iterations, "Should create only 1 iteration for same week");
        Assert.AreEqual("2025-W02", iterations[0].IterationKey);
    }

    [TestMethod]
    public async Task BackfillTimeframeIterationsAsync_SkipsPRsWithIterationsAlready()
    {
        // Arrange - Create an iteration manually
        var existingIteration = new TimeframeIterationEntity
        {
            Year = 2025,
            WeekNumber = 3,
            StartUtc = new DateTimeOffset(2025, 1, 13, 0, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2025, 1, 19, 23, 59, 59, TimeSpan.Zero),
            IterationKey = "2025-W03"
        };
        _context.TimeframeIterations.Add(existingIteration);
        await _context.SaveChangesAsync();

        var pr1 = new PullRequestEntity
        {
            Id = 1,
            RepositoryName = "TestRepo",
            Title = "PR with iteration",
            CreatedBy = "User1",
            CreatedDate = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero),
            Status = "Active",
            IterationPath = "Project\\Sprint1",
            SourceBranch = "feature/1",
            TargetBranch = "main",
            RetrievedAt = DateTimeOffset.UtcNow,
            TimeframeIterationId = existingIteration.Id // Already assigned
        };

        var pr2 = new PullRequestEntity
        {
            Id = 2,
            RepositoryName = "TestRepo",
            Title = "PR without iteration",
            CreatedBy = "User2",
            CreatedDate = new DateTimeOffset(2025, 1, 20, 14, 0, 0, TimeSpan.Zero),
            Status = "Active",
            IterationPath = "Project\\Sprint1",
            SourceBranch = "feature/2",
            TargetBranch = "main",
            RetrievedAt = DateTimeOffset.UtcNow,
            TimeframeIterationId = null // Not assigned yet
        };

        _context.PullRequests.AddRange(pr1, pr2);
        await _context.SaveChangesAsync();

        // Act
        await _repository.BackfillTimeframeIterationsAsync();

        // Assert
        var updatedPrs = await _context.PullRequests.ToListAsync();
        Assert.AreEqual(existingIteration.Id, updatedPrs[0].TimeframeIterationId, 
            "PR1 should keep its existing iteration");
        Assert.IsNotNull(updatedPrs[1].TimeframeIterationId, 
            "PR2 should have iteration assigned");
        Assert.AreNotEqual(updatedPrs[0].TimeframeIterationId, updatedPrs[1].TimeframeIterationId,
            "PRs in different weeks should have different iterations");
    }

    [TestMethod]
    public async Task SaveBulkAsync_GroupsPRsByWeek_MinimizesIterationCreation()
    {
        // Arrange - Multiple PRs in same week
        var prs = new[]
        {
            new PullRequestDto(1, "Repo", "PR 1", "User1", 
                new DateTimeOffset(2025, 1, 13, 8, 0, 0, TimeSpan.Zero), null, "Active", 
                "Project\\Sprint1", "feature/1", "main", DateTimeOffset.UtcNow),
            new PullRequestDto(2, "Repo", "PR 2", "User2", 
                new DateTimeOffset(2025, 1, 14, 10, 0, 0, TimeSpan.Zero), null, "Active", 
                "Project\\Sprint1", "feature/2", "main", DateTimeOffset.UtcNow),
            new PullRequestDto(3, "Repo", "PR 3", "User3", 
                new DateTimeOffset(2025, 1, 17, 16, 0, 0, TimeSpan.Zero), null, "Active", 
                "Project\\Sprint1", "feature/3", "main", DateTimeOffset.UtcNow),
        };

        // Act
        await _repository.SaveBulkAsync(
            prs,
            Enumerable.Empty<PullRequestIterationDto>(),
            Enumerable.Empty<PullRequestCommentDto>(),
            Enumerable.Empty<PullRequestFileChangeDto>());

        // Assert
        var iterations = await _context.TimeframeIterations.ToListAsync();
        Assert.HasCount(1, iterations, "Should create only 1 iteration for all PRs in same week");
        Assert.AreEqual("2025-W03", iterations[0].IterationKey);

        var savedPrs = await _context.PullRequests.ToListAsync();
        Assert.IsTrue(savedPrs.All(pr => pr.TimeframeIterationId == iterations[0].Id),
            "All PRs should reference the same iteration");
    }
}
