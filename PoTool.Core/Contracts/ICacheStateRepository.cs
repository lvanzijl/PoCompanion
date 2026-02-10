using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Repository interface for ProductOwner cache state persistence.
/// </summary>
public interface ICacheStateRepository
{
    /// <summary>
    /// Gets the cache state for a ProductOwner.
    /// Creates a new empty state if none exists.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cache state DTO.</returns>
    Task<CacheStateDto> GetOrCreateCacheStateAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cache state for a ProductOwner if it exists.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cache state DTO or null if not found.</returns>
    Task<CacheStateDto?> GetCacheStateAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the cache state for a ProductOwner.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="syncStatus">The sync status.</param>
    /// <param name="currentStage">Current sync stage name (optional).</param>
    /// <param name="stageProgressPercent">Progress percentage (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateSyncStatusAsync(
        int productOwnerId,
        CacheSyncStatusDto syncStatus,
        string? currentStage = null,
        int stageProgressPercent = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks sync as successful and updates watermarks and counts.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="workItemCount">Count of work items in cache.</param>
    /// <param name="pullRequestCount">Count of pull requests in cache.</param>
    /// <param name="pipelineCount">Count of pipeline runs in cache.</param>
    /// <param name="workItemWatermark">New work item watermark.</param>
    /// <param name="pullRequestWatermark">New pull request watermark.</param>
    /// <param name="pipelineWatermark">New pipeline watermark.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkSyncSuccessAsync(
        int productOwnerId,
        int workItemCount,
        int pullRequestCount,
        int pipelineCount,
        DateTimeOffset? workItemWatermark,
        DateTimeOffset? pullRequestWatermark,
        DateTimeOffset? pipelineWatermark,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks sync as successful but with warnings and updates watermarks and counts.
    /// </summary>
    Task MarkSyncSuccessWithWarningsAsync(
        int productOwnerId,
        int workItemCount,
        int pullRequestCount,
        int pipelineCount,
        DateTimeOffset? workItemWatermark,
        DateTimeOffset? pullRequestWatermark,
        DateTimeOffset? pipelineWatermark,
        string? warningMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks sync as failed with an error message.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="errorMessage">Error description.</param>
    /// <param name="failedStage">Stage where failure occurred.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkSyncFailedAsync(
        int productOwnerId,
        string errorMessage,
        string failedStage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the cache state for a ProductOwner (for delete cache operation).
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResetCacheStateAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current watermarks for incremental sync.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of watermarks (workItem, pullRequest, pipeline).</returns>
    Task<(DateTimeOffset? WorkItem, DateTimeOffset? PullRequest, DateTimeOffset? Pipeline)> GetWatermarksAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default);
}
