namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Current sync status for ProductOwner cache.
/// </summary>
public enum CacheSyncStatus
{
    /// <summary>
    /// No sync operation is running.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// Sync operation is currently in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Last sync completed successfully.
    /// </summary>
    Success = 2,

    /// <summary>
    /// Last sync failed.
    /// </summary>
    Failed = 3
}
