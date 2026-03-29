using PoTool.Shared.PullRequests;

namespace PoTool.Core.Contracts;

/// <summary>
/// Provider for reading pull request data from the configured data source.
/// Implementations select between Live (TFS direct) or Cached (repository) based on mode.
/// </summary>
public interface IPullRequestReadProvider
{
    /// <summary>
    /// Retrieves all pull requests from the configured data source.
    /// </summary>
    /// <param name="fromDate">Optional start date filter to retrieve PRs created on or after this date.</param>
    Task<IEnumerable<PullRequestDto>> GetAllAsync(DateTimeOffset? fromDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pull requests filtered by product IDs from the configured data source.
    /// </summary>
    /// <param name="productIds">Optional list of product IDs to filter by.</param>
    /// <param name="fromDate">Optional start date filter to retrieve PRs created on or after this date.</param>
    Task<IEnumerable<PullRequestDto>> GetByProductIdsAsync(List<int>? productIds, DateTimeOffset? fromDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pull requests scoped to the provided repositories and optional date range.
    /// </summary>
    Task<IEnumerable<PullRequestDto>> GetByRepositoryNamesAsync(
        IReadOnlyList<string> repositoryNames,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific pull request by ID from the configured data source.
    /// </summary>
    Task<PullRequestDto?> GetByIdAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves iterations for a pull request from the configured data source.
    /// </summary>
    Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves iterations for multiple pull requests from the configured data source.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<PullRequestIterationDto>>> GetIterationsForPullRequestsAsync(
        IReadOnlyList<int> pullRequestIds,
        IReadOnlyDictionary<int, string>? repositoryNamesByPullRequestId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves iterations for a pull request from the configured data source.
    /// Optimized version that accepts repository name to avoid redundant lookups.
    /// </summary>
    Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves comments for a pull request from the configured data source.
    /// </summary>
    Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves comments for multiple pull requests from the configured data source.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<PullRequestCommentDto>>> GetCommentsForPullRequestsAsync(
        IReadOnlyList<int> pullRequestIds,
        IReadOnlyDictionary<int, string>? repositoryNamesByPullRequestId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves comments for a pull request from the configured data source.
    /// Optimized version that accepts repository name to avoid redundant lookups.
    /// </summary>
    Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves file changes for a pull request from the configured data source.
    /// </summary>
    Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves file changes for multiple pull requests from the configured data source.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<PullRequestFileChangeDto>>> GetFileChangesForPullRequestsAsync(
        IReadOnlyList<int> pullRequestIds,
        IReadOnlyDictionary<int, string>? repositoryNamesByPullRequestId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves file changes for a pull request from the configured data source.
    /// Optimized version that accepts repository name to avoid redundant lookups.
    /// </summary>
    Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default);
}
