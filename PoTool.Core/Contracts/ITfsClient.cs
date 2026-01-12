using PoTool.Shared.WorkItems;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Pipelines;
using PoTool.Shared.Contracts.TfsVerification;
using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;
using PoTool.Core.Pipelines;

namespace PoTool.Core.Contracts;

/// <summary>
/// Interface for TFS/Azure DevOps integration.
/// Abstracts all TFS communication from the application.
/// </summary>
public interface ITfsClient
{
    /// <summary>
    /// Retrieves work items under the specified area path.
    /// </summary>
    /// <param name="areaPath">The area path to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of work item DTOs.</returns>
    Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves work items under the specified area path, optionally filtered by last modified date.
    /// </summary>
    /// <param name="areaPath">The area path to query.</param>
    /// <param name="since">Optional date to retrieve only work items modified since this date (incremental sync).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of work item DTOs.</returns>
    Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, DateTimeOffset? since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves work items starting from specified root work item IDs and their entire hierarchy.
    /// This method is used for product-scoped sync operations.
    /// </summary>
    /// <param name="rootWorkItemIds">The root work item IDs to start from.</param>
    /// <param name="since">Optional date to retrieve only work items modified since this date (incremental sync).</param>
    /// <param name="progressCallback">Optional callback for progress reporting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of work item DTOs including root items and their descendants.</returns>
    Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsAsync(
        int[] rootWorkItemIds,
        DateTimeOffset? since = null,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves work items starting from specified root work item IDs and their entire hierarchy.
    /// This method is used for product-scoped sync operations with detailed progress reporting.
    /// </summary>
    /// <param name="rootWorkItemIds">The root work item IDs to start from.</param>
    /// <param name="since">Optional date to retrieve only work items modified since this date (incremental sync).</param>
    /// <param name="detailedProgressCallback">Optional callback for detailed structured progress reporting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of work item DTOs including root items and their descendants.</returns>
    Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsWithDetailedProgressAsync(
        int[] rootWorkItemIds,
        DateTimeOffset? since = null,
        Action<SyncProgressDto>? detailedProgressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the TFS connection is working with the configured PAT.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection is valid, false otherwise.</returns>
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves area paths from TFS using the Classification Nodes API.
    /// This method returns area paths directly from TFS metadata without fetching work items.
    /// </summary>
    /// <param name="depth">Optional depth for hierarchical area paths. If not specified, retrieves all levels.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of area path strings (full paths).</returns>
    Task<IEnumerable<string>> GetAreaPathsAsync(int? depth = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pull requests from TFS/Azure DevOps.
    /// </summary>
    /// <param name="repositoryName">Optional repository name to filter by.</param>
    /// <param name="fromDate">Optional start date for filtering pull requests.</param>
    /// <param name="toDate">Optional end date for filtering pull requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of pull request DTOs.</returns>
    Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves iterations for a specific pull request.
    /// </summary>
    /// <param name="pullRequestId">The pull request ID.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of pull request iteration DTOs.</returns>
    Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves comments for a specific pull request.
    /// </summary>
    /// <param name="pullRequestId">The pull request ID.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of pull request comment DTOs.</returns>
    Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves file changes for a specific pull request iteration.
    /// </summary>
    /// <param name="pullRequestId">The pull request ID.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="iterationId">The iteration ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of pull request file change DTOs.</returns>
    Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(
        int pullRequestId,
        string repositoryName,
        int iterationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single work item by ID directly from TFS (bypasses cache).
    /// Used for validation of work item existence.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Work item DTO if found, null if not found.</returns>
    Task<WorkItemDto?> GetWorkItemByIdAsync(
        int workItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the revision history for a specific work item.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of work item revision DTOs ordered by revision number.</returns>
    Task<IEnumerable<WorkItemRevisionDto>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the state of a work item in TFS.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="newState">The new state value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the update was successful, false otherwise.</returns>
    Task<bool> UpdateWorkItemStateAsync(
        int workItemId,
        string newState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the effort (story points) of a work item in TFS.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="effort">The new effort value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the update was successful, false otherwise.</returns>
    Task<bool> UpdateWorkItemEffortAsync(
        int workItemId,
        int effort,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies TFS API capabilities by running diagnostic checks.
    /// </summary>
    /// <param name="includeWriteChecks">Whether to include write capability checks.</param>
    /// <param name="workItemIdForWriteCheck">Optional work item ID to use for write checks (user-provided).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete verification report with check results.</returns>
    Task<TfsVerificationReport> VerifyCapabilitiesAsync(
        bool includeWriteChecks = false,
        int? workItemIdForWriteCheck = null,
        CancellationToken cancellationToken = default);

    // ============================================
    // BULK METHODS - Prevent N+1 query patterns
    // ============================================

    /// <summary>
    /// Retrieves pull requests with their related details (iterations, comments, file changes) in a single batch.
    /// This prevents the N+1 pattern of fetching each PR's details in separate calls.
    /// </summary>
    /// <param name="repositoryName">Optional repository name to filter by.</param>
    /// <param name="fromDate">Optional start date for filtering pull requests.</param>
    /// <param name="toDate">Optional end date for filtering pull requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete pull request sync result with all related data.</returns>
    Task<PullRequestSyncResult> GetPullRequestsWithDetailsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates effort (story points) for multiple work items in a single batch operation.
    /// This prevents the N+1 pattern of updating each work item's effort in separate calls.
    /// </summary>
    /// <param name="updates">Collection of work item ID to effort value mappings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success/failure for each work item update.</returns>
    Task<BulkUpdateResult> UpdateWorkItemsEffortAsync(
        IEnumerable<WorkItemEffortUpdate> updates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates state for multiple work items in a single batch operation.
    /// This prevents the N+1 pattern of updating each work item's state in separate calls.
    /// </summary>
    /// <param name="updates">Collection of work item ID to new state mappings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success/failure for each work item update.</returns>
    Task<BulkUpdateResult> UpdateWorkItemsStateAsync(
        IEnumerable<WorkItemStateUpdate> updates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves revision history for multiple work items in a single batch operation.
    /// This prevents the N+1 pattern of fetching each work item's revisions in separate calls.
    /// </summary>
    /// <param name="workItemIds">Collection of work item IDs to get revisions for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping work item IDs to their revision history.</returns>
    Task<IDictionary<int, IEnumerable<WorkItemRevisionDto>>> GetWorkItemRevisionsBatchAsync(
        IEnumerable<int> workItemIds,
        CancellationToken cancellationToken = default);

    // ============================================
    // WORK ITEM CREATION
    // ============================================

    /// <summary>
    /// Creates a new work item in TFS/Azure DevOps.
    /// </summary>
    /// <param name="request">The work item creation request containing type, title, and optional fields.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the created work item ID or error information.</returns>
    Task<WorkItemCreateResult> CreateWorkItemAsync(
        WorkItemCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the parent link of a work item in TFS/Azure DevOps.
    /// </summary>
    /// <param name="workItemId">The work item ID to update.</param>
    /// <param name="newParentId">The new parent work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> UpdateWorkItemParentAsync(
        int workItemId,
        int newParentId,
        CancellationToken cancellationToken = default);

    // ============================================
    // PIPELINE METHODS
    // ============================================

    /// <summary>
    /// Retrieves pipeline definitions from TFS/Azure DevOps.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of pipeline DTOs.</returns>
    Task<IEnumerable<PipelineDto>> GetPipelinesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pipeline runs for a specific pipeline.
    /// </summary>
    /// <param name="pipelineId">The pipeline ID.</param>
    /// <param name="top">Maximum number of runs to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of pipeline run DTOs.</returns>
    Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(
        int pipelineId,
        int top = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all pipelines with their runs in a single batch operation.
    /// </summary>
    /// <param name="runsPerPipeline">Maximum number of runs to retrieve per pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete pipeline sync result with all related data.</returns>
    Task<PipelineSyncResult> GetPipelinesWithRunsAsync(
        int runsPerPipeline = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for creating a new work item.
/// </summary>
public record WorkItemCreateRequest
{
    /// <summary>
    /// Work item type (e.g., "Epic", "Feature", "User Story").
    /// </summary>
    public required string WorkItemType { get; init; }

    /// <summary>
    /// The title for the new work item.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Optional parent work item ID.
    /// </summary>
    public int? ParentId { get; init; }

    /// <summary>
    /// Optional effort value.
    /// </summary>
    public int? Effort { get; init; }

    /// <summary>
    /// Optional area path. If not specified, uses default from configuration.
    /// </summary>
    public string? AreaPath { get; init; }

    /// <summary>
    /// Optional iteration path.
    /// </summary>
    public string? IterationPath { get; init; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Result of a work item creation operation.
/// </summary>
public record WorkItemCreateResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The created work item ID if successful.
    /// </summary>
    public int? WorkItemId { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
