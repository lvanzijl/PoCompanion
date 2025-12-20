using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;

namespace PoTool.Tests.Integration.Support;

/// <summary>
/// Mock TFS client for integration testing.
/// Uses file-based test data instead of real TFS API calls.
/// </summary>
public class MockTfsClient : ITfsClient
{
    private readonly List<WorkItemDto> _mockWorkItems = new();
    private readonly List<PullRequestDto> _mockPullRequests = new();
    private readonly List<PullRequestIterationDto> _mockIterations = new();
    private readonly List<PullRequestCommentDto> _mockComments = new();
    private readonly List<PullRequestFileChangeDto> _mockFileChanges = new();

    public MockTfsClient()
    {
        // Initialize with sample test data
        _mockWorkItems = new List<WorkItemDto>
        {
            new WorkItemDto(
                TfsId: 1000,
                Type: "Goal",
                Title: "Test Goal",
                ParentTfsId: null,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null
            ),
            new WorkItemDto(
                TfsId: 1001,
                Type: "Objective",
                Title: "Test Objective",
                ParentTfsId: 1000,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null
            ),
            new WorkItemDto(
                TfsId: 1002,
                Type: "Epic",
                Title: "Test Epic",
                ParentTfsId: 1001,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: "New",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null
            )
        };

        // Initialize mock pull requests
        _mockPullRequests = new List<PullRequestDto>
        {
            new PullRequestDto(
                Id: 1,
                RepositoryName: "TestRepo",
                Title: "Add feature X",
                CreatedBy: "John Doe",
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-7),
                CompletedDate: DateTimeOffset.UtcNow.AddDays(-2),
                Status: "completed",
                IterationPath: "TestProject\\Sprint1",
                SourceBranch: "refs/heads/feature/x",
                TargetBranch: "refs/heads/main",
                RetrievedAt: DateTimeOffset.UtcNow
            ),
            new PullRequestDto(
                Id: 2,
                RepositoryName: "TestRepo",
                Title: "Fix bug Y",
                CreatedBy: "Jane Smith",
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-3),
                CompletedDate: null,
                Status: "active",
                IterationPath: "TestProject\\Sprint1",
                SourceBranch: "refs/heads/bugfix/y",
                TargetBranch: "refs/heads/main",
                RetrievedAt: DateTimeOffset.UtcNow
            )
        };

        // Initialize mock iterations
        _mockIterations = new List<PullRequestIterationDto>
        {
            new PullRequestIterationDto(
                PullRequestId: 1,
                IterationNumber: 1,
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-7),
                UpdatedDate: DateTimeOffset.UtcNow.AddDays(-6),
                CommitCount: 3,
                ChangeCount: 5
            ),
            new PullRequestIterationDto(
                PullRequestId: 1,
                IterationNumber: 2,
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-5),
                UpdatedDate: DateTimeOffset.UtcNow.AddDays(-4),
                CommitCount: 2,
                ChangeCount: 3
            )
        };

        // Initialize mock comments
        _mockComments = new List<PullRequestCommentDto>
        {
            new PullRequestCommentDto(
                Id: 1,
                PullRequestId: 1,
                ThreadId: 1,
                Author: "Reviewer One",
                Content: "Please fix the indentation",
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-6),
                UpdatedDate: null,
                IsResolved: true,
                ResolvedDate: DateTimeOffset.UtcNow.AddDays(-5),
                ResolvedBy: "John Doe"
            ),
            new PullRequestCommentDto(
                Id: 2,
                PullRequestId: 1,
                ThreadId: 2,
                Author: "Reviewer Two",
                Content: "Looks good!",
                CreatedDate: DateTimeOffset.UtcNow.AddDays(-3),
                UpdatedDate: null,
                IsResolved: false,
                ResolvedDate: null,
                ResolvedBy: null
            )
        };

        // Initialize mock file changes
        _mockFileChanges = new List<PullRequestFileChangeDto>
        {
            new PullRequestFileChangeDto(
                PullRequestId: 1,
                IterationId: 1,
                FilePath: "/src/feature.cs",
                ChangeType: "edit",
                LinesAdded: 50,
                LinesDeleted: 10,
                LinesModified: 5
            ),
            new PullRequestFileChangeDto(
                PullRequestId: 1,
                IterationId: 1,
                FilePath: "/tests/feature.test.cs",
                ChangeType: "add",
                LinesAdded: 100,
                LinesDeleted: 0,
                LinesModified: 0
            )
        };
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        // Always return true for integration tests
        return Task.FromResult(true);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        // Return mock work items
        return Task.FromResult<IEnumerable<WorkItemDto>>(_mockWorkItems);
    }

    /// <summary>
    /// Adds a mock work item for testing purposes.
    /// </summary>
    public void AddMockWorkItem(WorkItemDto workItem)
    {
        _mockWorkItems.Add(workItem);
    }

    /// <summary>
    /// Clears all mock work items.
    /// </summary>
    public void ClearMockWorkItems()
    {
        _mockWorkItems.Clear();
    }

    // Pull Request methods - return mock data for integration tests
    public Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var filtered = _mockPullRequests.AsEnumerable();

        if (!string.IsNullOrEmpty(repositoryName))
        {
            filtered = filtered.Where(pr => pr.RepositoryName == repositoryName);
        }

        if (fromDate.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate <= toDate.Value);
        }

        return Task.FromResult(filtered);
    }

    public Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var iterations = _mockIterations.Where(i => i.PullRequestId == pullRequestId);
        return Task.FromResult(iterations);
    }

    public Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var comments = _mockComments.Where(c => c.PullRequestId == pullRequestId);
        return Task.FromResult(comments);
    }

    public Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(
        int pullRequestId,
        string repositoryName,
        int iterationId,
        CancellationToken cancellationToken = default)
    {
        var changes = _mockFileChanges.Where(fc => fc.PullRequestId == pullRequestId && fc.IterationId == iterationId);
        return Task.FromResult(changes);
    }

    /// <summary>
    /// Adds a mock pull request for testing purposes.
    /// </summary>
    public void AddMockPullRequest(PullRequestDto pullRequest)
    {
        _mockPullRequests.Add(pullRequest);
    }

    /// <summary>
    /// Adds a mock iteration for testing purposes.
    /// </summary>
    public void AddMockIteration(PullRequestIterationDto iteration)
    {
        _mockIterations.Add(iteration);
    }

    /// <summary>
    /// Adds a mock comment for testing purposes.
    /// </summary>
    public void AddMockComment(PullRequestCommentDto comment)
    {
        _mockComments.Add(comment);
    }

    /// <summary>
    /// Adds a mock file change for testing purposes.
    /// </summary>
    public void AddMockFileChange(PullRequestFileChangeDto fileChange)
    {
        _mockFileChanges.Add(fileChange);
    }
}
