using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;

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
    /// Validates that the TFS connection is working with the configured PAT.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection is valid, false otherwise.</returns>
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);

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
    /// Retrieves the revision history for a specific work item.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of work item revision DTOs ordered by revision number.</returns>
    Task<IEnumerable<WorkItemRevisionDto>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default);
}
