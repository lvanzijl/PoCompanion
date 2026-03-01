namespace PoTool.Shared.Settings;

/// <summary>
/// Current sync status for ProductOwner cache.
/// </summary>
public enum CacheSyncStatusDto
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
    Failed = 3,

    /// <summary>
    /// Last sync completed with warnings but data is available.
    /// </summary>
    SuccessWithWarnings = 4
}

/// <summary>
/// DTO for ProductOwner cache state information.
/// </summary>
public class CacheStateDto
{
    /// <summary>
    /// The ProductOwner ID this cache state belongs to.
    /// </summary>
    public int ProductOwnerId { get; set; }

    /// <summary>
    /// Current sync status.
    /// </summary>
    public CacheSyncStatusDto SyncStatus { get; set; }

    /// <summary>
    /// Timestamp of the last sync attempt (success or failure).
    /// </summary>
    public DateTimeOffset? LastAttemptSync { get; set; }

    /// <summary>
    /// Timestamp of the last successful sync completion.
    /// </summary>
    public DateTimeOffset? LastSuccessfulSync { get; set; }

    /// <summary>
    /// Timestamp of the successful sync before the most recent one.
    /// Used to compute what changed since the last sync.
    /// </summary>
    public DateTimeOffset? PreviousSuccessfulSync { get; set; }

    /// <summary>
    /// Count of cached work items after last successful sync.
    /// </summary>
    public int WorkItemCount { get; set; }

    /// <summary>
    /// Count of cached pull requests after last successful sync.
    /// </summary>
    public int PullRequestCount { get; set; }

    /// <summary>
    /// Count of cached pipeline runs after last successful sync.
    /// </summary>
    public int PipelineCount { get; set; }

    /// <summary>
    /// Error message from last failed sync attempt.
    /// </summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// Current sync stage when SyncStatus is InProgress.
    /// </summary>
    public string? CurrentSyncStage { get; set; }

    /// <summary>
    /// Progress percentage within current stage (0-100).
    /// </summary>
    public int StageProgressPercent { get; set; }
}
