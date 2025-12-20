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

    // Pull Request methods - return empty collections for integration tests
    public Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<PullRequestDto>());
    }

    public Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<PullRequestIterationDto>());
    }

    public Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<PullRequestCommentDto>());
    }

    public Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(
        int pullRequestId,
        string repositoryName,
        int iterationId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<PullRequestFileChangeDto>());
    }
}
