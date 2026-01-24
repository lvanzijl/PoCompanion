using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Progress update during sync operation.
/// </summary>
public class SyncProgressUpdate
{
    /// <summary>
    /// Current sync stage name.
    /// </summary>
    public string CurrentStage { get; init; } = string.Empty;

    /// <summary>
    /// Progress percentage within current stage (0-100).
    /// </summary>
    public int StageProgressPercent { get; init; }

    /// <summary>
    /// Whether the sync has completed (success or failure).
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Whether the sync failed.
    /// </summary>
    public bool HasFailed { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Stage number (1-6).
    /// </summary>
    public int StageNumber { get; init; }

    /// <summary>
    /// Total number of stages.
    /// </summary>
    public int TotalStages { get; init; } = 6;
}

/// <summary>
/// Service interface for managing ProductOwner cache sync operations.
/// </summary>
public interface ICacheSyncService
{
    /// <summary>
    /// Triggers a cache sync for a ProductOwner.
    /// If a sync is already in progress, attaches to the existing sync.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An observable for progress updates.</returns>
    Task<IAsyncEnumerable<SyncProgressUpdate>> TriggerSyncAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current cache state for a ProductOwner.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cache state DTO.</returns>
    Task<CacheStateDto> GetCacheStateAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels any running sync for a ProductOwner.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelSyncAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all cached data for a ProductOwner and resets the cache state.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteCacheAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a sync is currently in progress for a ProductOwner.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if sync is in progress.</returns>
    Task<bool> IsSyncInProgressAsync(int productOwnerId, CancellationToken cancellationToken = default);
}
