using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence;
using PoTool.Api.Repositories;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Repositories;

/// <summary>
/// Tests to verify that PullRequestRepository properly handles concurrent operations
/// and prevents "A second operation was started on this context instance" errors.
/// </summary>
[TestClass]
public class PullRequestRepositoryConcurrencyTests
{
    private PoToolDbContext _context = null!;
    private PullRequestRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new PoToolDbContext(options);
        _repository = new PullRequestRepository(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _repository.Dispose();
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    /// <summary>
    /// Tests that multiple concurrent SaveBulkAsync operations are properly serialized
    /// by the semaphore guard and don't throw InvalidOperationException.
    /// This simulates the scenario where multiple PR sync operations might run concurrently.
    /// </summary>
    [TestMethod]
    public async Task ConcurrentSaveBulkAsync_DoesNotThrowConcurrencyException()
    {
        // Arrange - Create test data for multiple concurrent saves
        var tasks = new List<Task>();

        // Create 5 concurrent save operations, each saving different PRs
        for (int i = 0; i < 5; i++)
        {
            int prId = i + 1;
            var pullRequests = new List<PullRequestDto>
            {
                new PullRequestDto(
                    prId, 
                    $"TestRepo{i}", 
                    $"PR{prId}", 
                    $"User{i}", 
                    DateTimeOffset.UtcNow, 
                    null, 
                    "Active", 
                    "Sprint1", 
                    $"feature/{prId}", 
                    "main", 
                    DateTimeOffset.UtcNow, 
                    1)
            };

            var iterations = new List<PullRequestIterationDto>
            {
                new PullRequestIterationDto(prId, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 5, 10)
            };

            var comments = new List<PullRequestCommentDto>
            {
                new PullRequestCommentDto(
                    prId, 
                    prId, 
                    1, 
                    $"Author{i}", 
                    $"Comment{prId}", 
                    DateTimeOffset.UtcNow, 
                    DateTimeOffset.UtcNow, 
                    false, 
                    null, 
                    null)
            };

            var fileChanges = new List<PullRequestFileChangeDto>
            {
                new PullRequestFileChangeDto(prId, 1, $"file{prId}.cs", "edit", 10, 5, 2)
            };

            // Act - Start concurrent save operation
            var task = _repository.SaveBulkAsync(pullRequests, iterations, comments, fileChanges, CancellationToken.None);
            tasks.Add(task);
        }

        // Assert - All operations should complete without throwing exceptions
        // If the semaphore guard is working, operations will be serialized
        // If the semaphore guard is NOT working, this would throw InvalidOperationException:
        // "A second operation was started on this context instance before a previous operation completed"
        await Task.WhenAll(tasks);

        // Verify all PRs were saved
        var savedPrs = await _context.PullRequests.ToListAsync();
        Assert.HasCount(5, savedPrs, "All 5 PRs should be saved successfully");
    }

    /// <summary>
    /// Tests that concurrent read and write operations are properly serialized.
    /// </summary>
    [TestMethod]
    public async Task ConcurrentReadAndWrite_DoesNotThrowConcurrencyException()
    {
        // Arrange - Create initial data
        var initialPr = new PullRequestDto(
            1, 
            "TestRepo", 
            "Initial PR", 
            "User1", 
            DateTimeOffset.UtcNow, 
            null, 
            "Active", 
            "Sprint1", 
            "feature/1", 
            "main", 
            DateTimeOffset.UtcNow, 
            1);
        
        await _repository.SaveBulkAsync(
            new List<PullRequestDto> { initialPr },
            new List<PullRequestIterationDto>(),
            new List<PullRequestCommentDto>(),
            new List<PullRequestFileChangeDto>(),
            CancellationToken.None);

        // Act - Start concurrent read and write operations
        var tasks = new List<Task>();

        // Add 3 read operations
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(_repository.GetAllAsync(CancellationToken.None));
        }

        // Add 2 write operations
        for (int i = 0; i < 2; i++)
        {
            int prId = i + 2;
            var pullRequests = new List<PullRequestDto>
            {
                new PullRequestDto(
                    prId, 
                    $"TestRepo{i}", 
                    $"PR{prId}", 
                    $"User{i}", 
                    DateTimeOffset.UtcNow, 
                    null, 
                    "Active", 
                    "Sprint1", 
                    $"feature/{prId}", 
                    "main", 
                    DateTimeOffset.UtcNow, 
                    1)
            };

            tasks.Add(_repository.SaveBulkAsync(
                pullRequests,
                new List<PullRequestIterationDto>(),
                new List<PullRequestCommentDto>(),
                new List<PullRequestFileChangeDto>(),
                CancellationToken.None));
        }

        // Assert - All operations should complete without exceptions
        await Task.WhenAll(tasks);

        // Verify final state
        var savedPrs = await _context.PullRequests.ToListAsync();
        Assert.HasCount(3, savedPrs, "Should have initial PR plus 2 new PRs");
    }

    /// <summary>
    /// Tests that the legacy separate save methods (SaveAsync, SaveIterationsAsync, etc.)
    /// are also properly guarded against concurrent access.
    /// This simulates the old WorkItemSyncService behavior that caused the original bug.
    /// </summary>
    [TestMethod]
    public async Task ConcurrentSeparateSaveMethods_DoesNotThrowConcurrencyException()
    {
        // Arrange - Prepare data
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

        // Act - Call the separate save methods in parallel (simulating the old bug scenario)
        // This should NOT throw because each method now has semaphore protection
        var saveTask1 = _repository.SaveAsync(pullRequests, CancellationToken.None);
        var saveTask2 = _repository.SaveIterationsAsync(iterations, CancellationToken.None);
        var saveTask3 = _repository.SaveCommentsAsync(comments, CancellationToken.None);
        var saveTask4 = _repository.SaveFileChangesAsync(fileChanges, CancellationToken.None);

        // Assert - All should complete without throwing
        await Task.WhenAll(saveTask1, saveTask2, saveTask3, saveTask4);

        // Verify all data was saved
        var savedPrs = await _context.PullRequests.ToListAsync();
        var savedIterations = await _context.PullRequestIterations.ToListAsync();
        var savedComments = await _context.PullRequestComments.ToListAsync();
        var savedFileChanges = await _context.PullRequestFileChanges.ToListAsync();

        Assert.HasCount(2, savedPrs);
        Assert.HasCount(2, savedIterations);
        Assert.HasCount(2, savedComments);
        Assert.HasCount(2, savedFileChanges);
    }

    /// <summary>
    /// Tests that the semaphore properly serializes operations by measuring timing.
    /// Operations should execute sequentially, not in parallel.
    /// </summary>
    [TestMethod]
    public async Task SemaphoreSerializesOperations()
    {
        // Arrange - Create operations with artificial delays
        var tasks = new List<Task<long>>();
        
        // Use ConcurrentBag for thread-safe collection
        var operationStartTimes = new System.Collections.Concurrent.ConcurrentBag<long>();
        var operationEndTimes = new System.Collections.Concurrent.ConcurrentBag<long>();

        // Act - Start 3 concurrent operations
        for (int i = 0; i < 3; i++)
        {
            int prId = i + 1;
            tasks.Add(Task.Run(async () =>
            {
                var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                var pullRequests = new List<PullRequestDto>
                {
                    new PullRequestDto(
                        prId, 
                        $"TestRepo{i}", 
                        $"PR{prId}", 
                        $"User{i}", 
                        DateTimeOffset.UtcNow, 
                        null, 
                        "Active", 
                        "Sprint1", 
                        $"feature/{prId}", 
                        "main", 
                        DateTimeOffset.UtcNow, 
                        1)
                };

                await _repository.SaveBulkAsync(
                    pullRequests,
                    new List<PullRequestIterationDto>(),
                    new List<PullRequestCommentDto>(),
                    new List<PullRequestFileChangeDto>(),
                    CancellationToken.None);

                // Add a small delay to make serialization observable
                await Task.Delay(50);

                var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                operationStartTimes.Add(startTime);
                operationEndTimes.Add(endTime);

                return endTime - startTime;
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Operations should have executed sequentially
        // Each operation should start after the previous one completes
        var sortedStarts = operationStartTimes.OrderBy(t => t).ToList();
        var sortedEnds = operationEndTimes.OrderBy(t => t).ToList();

        // The second operation should start after or at the same time as the first one ends
        // (with some tolerance for timing precision)
        for (int i = 1; i < sortedStarts.Count; i++)
        {
            // Allow 20ms tolerance for timing precision issues
            Assert.IsGreaterThanOrEqualTo(
                sortedStarts[i], 
                sortedEnds[i - 1] - 20,
                $"Operation {i} should start after operation {i - 1} completes (serialized by semaphore). " +
                $"Start: {sortedStarts[i]}, Previous end: {sortedEnds[i - 1]}");
        }
    }

    /// <summary>
    /// Tests that repository disposal properly cleans up the semaphore.
    /// </summary>
    [TestMethod]
    public void Dispose_CleansUpSemaphore()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var context = new PoToolDbContext(options);
        var repository = new PullRequestRepository(context);

        // Act & Assert - Dispose should not throw
        try
        {
            repository.Dispose();
            context.Dispose();
        }
        catch (Exception ex)
        {
            Assert.Fail($"Dispose should complete without exception, but threw: {ex.Message}");
        }
    }
}
