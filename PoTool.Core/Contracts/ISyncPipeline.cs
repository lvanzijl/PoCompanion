namespace PoTool.Core.Contracts;

/// <summary>
/// Represents the sync pipeline that executes stages in order.
/// </summary>
public interface ISyncPipeline
{
    /// <summary>
    /// Executes all sync stages for a ProductOwner.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID to sync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of progress updates.</returns>
    IAsyncEnumerable<SyncProgressUpdate> ExecuteAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels any running sync for a ProductOwner.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    void CancelSync(int productOwnerId);

    /// <summary>
    /// Checks if a sync is currently running for a ProductOwner.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <returns>True if sync is running.</returns>
    bool IsSyncRunning(int productOwnerId);
}
