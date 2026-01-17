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
    Task<IEnumerable<PullRequestDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pull requests filtered by product IDs from the configured data source.
    /// </summary>
    Task<IEnumerable<PullRequestDto>> GetByProductIdsAsync(List<int>? productIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific pull request by ID from the configured data source.
    /// </summary>
    Task<PullRequestDto?> GetByIdAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves iterations for a pull request from the configured data source.
    /// </summary>
    Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves comments for a pull request from the configured data source.
    /// </summary>
    Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves file changes for a pull request from the configured data source.
    /// </summary>
    Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, CancellationToken cancellationToken = default);
}
