using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;
using Microsoft.Extensions.Logging;

namespace PoTool.Api.Services;

/// <summary>
/// Mock TFS client for development and testing purposes.
/// Returns predefined mock data instead of connecting to a real TFS/Azure DevOps instance.
/// </summary>
public class MockTfsClient : ITfsClient
{
    private readonly MockDataProvider _workItemDataProvider;
    private readonly MockPullRequestDataProvider _pullRequestDataProvider;
    private readonly ILogger<MockTfsClient> _logger;

    public MockTfsClient(
        MockDataProvider workItemDataProvider,
        MockPullRequestDataProvider pullRequestDataProvider,
        ILogger<MockTfsClient> logger)
    {
        _workItemDataProvider = workItemDataProvider;
        _pullRequestDataProvider = pullRequestDataProvider;
        _logger = logger;
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: ValidateConnectionAsync called - always returns true");
        return Task.FromResult(true);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        return GetWorkItemsAsync(areaPath, since: null, cancellationToken);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetWorkItemsAsync called for areaPath={AreaPath}, since={Since}", areaPath, since);
        
        // Get all mock work items from the provider
        var allWorkItems = _workItemDataProvider.GetMockHierarchy();
        
        // Filter by area path (simple contains check for mock purposes)
        var filtered = allWorkItems.Where(wi => wi.AreaPath.Contains(areaPath, StringComparison.OrdinalIgnoreCase));
        
        // Filter by date if specified (incremental sync)
        if (since.HasValue)
        {
            filtered = filtered.Where(wi => wi.RetrievedAt >= since.Value);
        }
        
        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} work items", result.Count);
        
        return Task.FromResult<IEnumerable<WorkItemDto>>(result);
    }

    public Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetPullRequestsAsync called for repository={Repository}", repositoryName);
        
        // Get all mock pull requests from the provider
        var allPullRequests = _pullRequestDataProvider.GetMockPullRequests();
        
        var filtered = allPullRequests.AsEnumerable();

        if (!string.IsNullOrEmpty(repositoryName))
        {
            filtered = filtered.Where(pr => pr.RepositoryName.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));
        }

        if (fromDate.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate <= toDate.Value);
        }

        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} pull requests", result.Count);
        
        return Task.FromResult<IEnumerable<PullRequestDto>>(result);
    }

    public Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetPullRequestIterationsAsync called for PR {PullRequestId}", pullRequestId);
        
        var allIterations = _pullRequestDataProvider.GetMockIterations();
        var filtered = allIterations.Where(i => i.PullRequestId == pullRequestId);
        
        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} iterations", result.Count);
        return Task.FromResult<IEnumerable<PullRequestIterationDto>>(result);
    }

    public Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetPullRequestCommentsAsync called for PR {PullRequestId}", pullRequestId);
        
        var allComments = _pullRequestDataProvider.GetMockComments();
        var filtered = allComments.Where(c => c.PullRequestId == pullRequestId);
        
        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} comments", result.Count);
        return Task.FromResult<IEnumerable<PullRequestCommentDto>>(result);
    }

    public Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(
        int pullRequestId,
        string repositoryName,
        int iterationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetPullRequestFileChangesAsync called for PR {PullRequestId}, iteration {IterationId}", 
            pullRequestId, iterationId);
        
        var allFileChanges = _pullRequestDataProvider.GetMockFileChanges();
        var filtered = allFileChanges.Where(fc => fc.PullRequestId == pullRequestId && fc.IterationId == iterationId);
        
        var result = filtered.ToList();
        _logger.LogInformation("Mock TFS client: Returning {Count} file changes", result.Count);
        return Task.FromResult<IEnumerable<PullRequestFileChangeDto>>(result);
    }

    public Task<IEnumerable<WorkItemRevisionDto>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: GetWorkItemRevisionsAsync called for work item {WorkItemId}", workItemId);
        
        // Return sample revision history
        var mockRevisions = new List<WorkItemRevisionDto>
        {
            new WorkItemRevisionDto(
                RevisionNumber: 1,
                WorkItemId: workItemId,
                ChangedBy: "Mock User",
                ChangedDate: DateTimeOffset.UtcNow.AddDays(-10),
                FieldChanges: new Dictionary<string, WorkItemFieldChange>
                {
                    ["System.Title"] = new WorkItemFieldChange("System.Title", null, "Initial Title"),
                    ["System.State"] = new WorkItemFieldChange("System.State", null, "New")
                },
                Comment: "Work item created"
            ),
            new WorkItemRevisionDto(
                RevisionNumber: 2,
                WorkItemId: workItemId,
                ChangedBy: "Mock User 2",
                ChangedDate: DateTimeOffset.UtcNow.AddDays(-5),
                FieldChanges: new Dictionary<string, WorkItemFieldChange>
                {
                    ["System.State"] = new WorkItemFieldChange("System.State", "New", "Active"),
                    ["System.AssignedTo"] = new WorkItemFieldChange("System.AssignedTo", null, "Mock User 2")
                },
                Comment: "Started work on this item"
            )
        };

        _logger.LogInformation("Mock TFS client: Returning {Count} revisions", mockRevisions.Count);
        return Task.FromResult<IEnumerable<WorkItemRevisionDto>>(mockRevisions);
    }

    public Task<bool> UpdateWorkItemStateAsync(int workItemId, string newState, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: UpdateWorkItemStateAsync called for workItemId={WorkItemId}, newState={NewState}", 
            workItemId, newState);
        
        // Mock implementation always succeeds for valid state values
        var validStates = new[] { "New", "Active", "In Progress", "Resolved", "Closed", "Done", "Removed" };
        var isValidState = validStates.Contains(newState, StringComparer.OrdinalIgnoreCase);
        
        if (!isValidState)
        {
            _logger.LogWarning("Mock TFS client: Invalid state '{NewState}' provided", newState);
            return Task.FromResult(false);
        }
        
        _logger.LogInformation("Mock TFS client: Successfully 'updated' work item {WorkItemId} to state '{NewState}'", 
            workItemId, newState);
        return Task.FromResult(true);
    }

    public Task<bool> UpdateWorkItemEffortAsync(int workItemId, int effort, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock TFS client: UpdateWorkItemEffortAsync called for workItemId={WorkItemId}, effort={Effort}", 
            workItemId, effort);
        
        // Mock implementation always succeeds for valid effort values
        if (effort < 0)
        {
            _logger.LogWarning("Mock TFS client: Invalid effort value {Effort} provided (must be >= 0)", effort);
            return Task.FromResult(false);
        }
        
        _logger.LogInformation("Mock TFS client: Successfully 'updated' work item {WorkItemId} effort to {Effort}", 
            workItemId, effort);
        return Task.FromResult(true);
    }
}
