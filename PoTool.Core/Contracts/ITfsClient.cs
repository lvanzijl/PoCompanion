using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;
using PoTool.Core.Contracts.TfsVerification;

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
}
