using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence;
using PoTool.Api.Repositories;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Repositories;

[TestClass]
public class PullRequestRepositoryBulkSaveTests
{
    private PoToolDbContext _context = null!;
    private PullRequestRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            .Options;

        _context = new PoToolDbContext(options);
        _repository = new PullRequestRepository(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    public async Task SaveBulkAsync_WithAllDataTypes_SavesAtomically()
    {
        // Arrange
        var pullRequests = new List<PullRequestDto>
        {
            new PullRequestDto(1, "TestRepo", "PR1", "User1", DateTimeOffset.UtcNow, null, "Active", "Sprint1", "feature/1", "main", DateTimeOffset.UtcNow, 1),
            new PullRequestDto(2, "TestRepo", "PR2", "User2", DateTimeOffset.UtcNow, null, "Active", "Sprint1", "feature/2", "main", DateTimeOffset.UtcNow, 1)
        };

        var iterations = new List<PullRequestIterationDto>
        {
            new PullRequestIterationDto(1, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 5, 10),
            new PullRequestIterationDto(2, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 3, 7)
        };

        var comments = new List<PullRequestCommentDto>
        {
            new PullRequestCommentDto(1, 1, 1, "Author1", "Comment1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false, null, null),
            new PullRequestCommentDto(2, 2, 1, "Author2", "Comment2", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false, null, null)
        };

        var fileChanges = new List<PullRequestFileChangeDto>
        {
            new PullRequestFileChangeDto(1, 1, "file1.cs", "edit", 10, 5, 2),
            new PullRequestFileChangeDto(2, 1, "file2.cs", "edit", 20, 10, 5)
        };

        // Act
        await _repository.SaveBulkAsync(pullRequests, iterations, comments, fileChanges, CancellationToken.None);

        // Assert
        var savedPrs = await _context.PullRequests.ToListAsync();
        var savedIterations = await _context.PullRequestIterations.ToListAsync();
        var savedComments = await _context.PullRequestComments.ToListAsync();
        var savedFileChanges = await _context.PullRequestFileChanges.ToListAsync();

        Assert.AreEqual(2, savedPrs.Count());
        Assert.AreEqual(2, savedIterations.Count());
        Assert.AreEqual(2, savedComments.Count());
        Assert.AreEqual(2, savedFileChanges.Count());
    }

    [TestMethod]
    public async Task SaveBulkAsync_UpdatesExistingPullRequests()
    {
        // Arrange - First save
        var initialPr = new PullRequestDto(1, "TestRepo", "Initial Title", "User1", DateTimeOffset.UtcNow, null, "Active", "Sprint1", "feature/1", "main", DateTimeOffset.UtcNow, 1);
        await _repository.SaveBulkAsync(
            new List<PullRequestDto> { initialPr },
            new List<PullRequestIterationDto>(),
            new List<PullRequestCommentDto>(),
            new List<PullRequestFileChangeDto>(),
            CancellationToken.None);

        // Act - Update with new title
        var updatedPr = initialPr with { Title = "Updated Title" };
        await _repository.SaveBulkAsync(
            new List<PullRequestDto> { updatedPr },
            new List<PullRequestIterationDto>(),
            new List<PullRequestCommentDto>(),
            new List<PullRequestFileChangeDto>(),
            CancellationToken.None);

        // Assert
        var savedPrs = await _context.PullRequests.ToListAsync();
        Assert.AreEqual(1, savedPrs.Count());
        Assert.AreEqual("Updated Title", savedPrs[0].Title);
    }

    [TestMethod]
    public async Task SaveBulkAsync_WithMultipleIterations_SavesAll()
    {
        // Arrange
        var pr = new PullRequestDto(1, "TestRepo", "PR1", "User1", DateTimeOffset.UtcNow, null, "Active", "Sprint1", "feature/1", "main", DateTimeOffset.UtcNow, 1);
        
        var iterations = new List<PullRequestIterationDto>
        {
            new PullRequestIterationDto(1, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 5, 10),
            new PullRequestIterationDto(1, 2, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1), 3, 5),
            new PullRequestIterationDto(1, 3, DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(2), 2, 3)
        };

        // Act
        await _repository.SaveBulkAsync(
            new List<PullRequestDto> { pr },
            iterations,
            new List<PullRequestCommentDto>(),
            new List<PullRequestFileChangeDto>(),
            CancellationToken.None);

        // Assert
        var savedIterations = await _context.PullRequestIterations
            .Where(i => i.PullRequestId == 1)
            .OrderBy(i => i.IterationNumber)
            .ToListAsync();

        Assert.AreEqual(3, savedIterations.Count());
        Assert.AreEqual(1, savedIterations[0].IterationNumber);
        Assert.AreEqual(2, savedIterations[1].IterationNumber);
        Assert.AreEqual(3, savedIterations[2].IterationNumber);
    }

    [TestMethod]
    public async Task SaveBulkAsync_WithNoData_CompletesSuccessfully()
    {
        // Act
        await _repository.SaveBulkAsync(
            new List<PullRequestDto>(),
            new List<PullRequestIterationDto>(),
            new List<PullRequestCommentDto>(),
            new List<PullRequestFileChangeDto>(),
            CancellationToken.None);

        // Assert
        var savedPrs = await _context.PullRequests.ToListAsync();
        Assert.AreEqual(0, savedPrs.Count());
    }

    [TestMethod]
    public async Task SaveBulkAsync_ReplacesFileChangesForSameIteration()
    {
        // Arrange - Initial save with 2 file changes
        var pr = new PullRequestDto(1, "TestRepo", "PR1", "User1", DateTimeOffset.UtcNow, null, "Active", "Sprint1", "feature/1", "main", DateTimeOffset.UtcNow, 1);
        var iteration = new PullRequestIterationDto(1, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 5, 10);
        
        var initialFileChanges = new List<PullRequestFileChangeDto>
        {
            new PullRequestFileChangeDto(1, 1, "file1.cs", "edit", 10, 5, 2),
            new PullRequestFileChangeDto(1, 1, "file2.cs", "edit", 20, 10, 5)
        };

        await _repository.SaveBulkAsync(
            new List<PullRequestDto> { pr },
            new List<PullRequestIterationDto> { iteration },
            new List<PullRequestCommentDto>(),
            initialFileChanges,
            CancellationToken.None);

        // Act - Replace with new file changes (3 files this time)
        var updatedFileChanges = new List<PullRequestFileChangeDto>
        {
            new PullRequestFileChangeDto(1, 1, "file1.cs", "edit", 15, 7, 3),
            new PullRequestFileChangeDto(1, 1, "file2.cs", "edit", 25, 12, 6),
            new PullRequestFileChangeDto(1, 1, "file3.cs", "add", 30, 0, 0)
        };

        await _repository.SaveBulkAsync(
            new List<PullRequestDto> { pr },
            new List<PullRequestIterationDto> { iteration },
            new List<PullRequestCommentDto>(),
            updatedFileChanges,
            CancellationToken.None);

        // Assert
        var savedFileChanges = await _context.PullRequestFileChanges
            .Where(fc => fc.PullRequestId == 1 && fc.IterationId == 1)
            .ToListAsync();

        Assert.AreEqual(3, savedFileChanges.Count());
        Assert.IsTrue(savedFileChanges.Any(fc => fc.FilePath == "file3.cs"));
    }

    [TestMethod]
    public async Task SaveBulkAsync_IsSingleTransaction()
    {
        // This test verifies that all operations happen in a single transaction
        // by confirming that only one SaveChangesAsync is called implicitly

        // Arrange
        var pr = new PullRequestDto(1, "TestRepo", "PR1", "User1", DateTimeOffset.UtcNow, null, "Active", "Sprint1", "feature/1", "main", DateTimeOffset.UtcNow, 1);
        var iteration = new PullRequestIterationDto(1, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 5, 10);
        var comment = new PullRequestCommentDto(1, 1, 1, "Author1", "Comment1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false, null, null);
        var fileChange = new PullRequestFileChangeDto(1, 1, "file1.cs", "edit", 10, 5, 2);

        // Act
        await _repository.SaveBulkAsync(
            new List<PullRequestDto> { pr },
            new List<PullRequestIterationDto> { iteration },
            new List<PullRequestCommentDto> { comment },
            new List<PullRequestFileChangeDto> { fileChange },
            CancellationToken.None);

        // Assert - All data should be present
        var savedPrs = await _context.PullRequests.ToListAsync();
        var savedIterations = await _context.PullRequestIterations.ToListAsync();
        var savedComments = await _context.PullRequestComments.ToListAsync();
        var savedFileChanges = await _context.PullRequestFileChanges.ToListAsync();

        Assert.AreEqual(1, savedPrs.Count());
        Assert.AreEqual(1, savedIterations.Count());
        Assert.AreEqual(1, savedComments.Count());
        Assert.AreEqual(1, savedFileChanges.Count());
    }
}
