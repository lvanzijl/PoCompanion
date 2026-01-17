using PoTool.Shared.PullRequests;

namespace PoTool.Core.Contracts;

/// <summary>
/// Interface for pull request repository operations.
/// Handles local caching and persistence of pull request data.
/// </summary>
public interface IPullRequestRepository : IDisposable
{
    /// <summary>
    /// Retrieves all cached pull requests.
    /// </summary>
    Task<IEnumerable<PullRequestDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pull requests filtered by product IDs.
    /// </summary>
    /// <param name="productIds">List of product IDs to filter by. If null or empty, returns all PRs.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IEnumerable<PullRequestDto>> GetByProductIdsAsync(List<int>? productIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific pull request by ID.
    /// </summary>
    Task<PullRequestDto?> GetByIdAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores or updates pull requests in the cache.
    /// </summary>
    Task SaveAsync(IEnumerable<PullRequestDto> pullRequests, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores or updates pull request iterations.
    /// </summary>
    Task SaveIterationsAsync(IEnumerable<PullRequestIterationDto> iterations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves iterations for a pull request.
    /// </summary>
    Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores or updates pull request comments.
    /// </summary>
    Task SaveCommentsAsync(IEnumerable<PullRequestCommentDto> comments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves comments for a pull request.
    /// </summary>
    Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores or updates pull request file changes.
    /// </summary>
    Task SaveFileChangesAsync(IEnumerable<PullRequestFileChangeDto> fileChanges, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves file changes for a pull request.
    /// </summary>
    Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached pull request data.
    /// </summary>
    Task ClearAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically saves pull requests with all their related data (iterations, comments, file changes)
    /// in a single database transaction. This prevents concurrent database operations and ensures
    /// consistency during PR sync operations.
    /// </summary>
    /// <param name="pullRequests">Pull requests to save or update.</param>
    /// <param name="iterations">Pull request iterations to save or update.</param>
    /// <param name="comments">Pull request comments to save or update.</param>
    /// <param name="fileChanges">Pull request file changes to save or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveBulkAsync(
        IEnumerable<PullRequestDto> pullRequests,
        IEnumerable<PullRequestIterationDto> iterations,
        IEnumerable<PullRequestCommentDto> comments,
        IEnumerable<PullRequestFileChangeDto> fileChanges,
        CancellationToken cancellationToken = default);
}
