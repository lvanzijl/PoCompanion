using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for tracking cache state per ProductOwner.
/// </summary>
public class ProductOwnerCacheStateEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to ProfileEntity (ProductOwner).
    /// </summary>
    [Required]
    public int ProductOwnerId { get; set; }

    /// <summary>
    /// Current sync status.
    /// </summary>
    [Required]
    public CacheSyncStatus SyncStatus { get; set; } = CacheSyncStatus.Idle;

    /// <summary>
    /// Timestamp of the last sync attempt (success or failure).
    /// </summary>
    public DateTimeOffset? LastAttemptSync { get; set; }

    /// <summary>
    /// Timestamp of the last successful sync completion.
    /// </summary>
    public DateTimeOffset? LastSuccessfulSync { get; set; }

    /// <summary>
    /// Count of cached work items after last successful sync.
    /// </summary>
    public int WorkItemCount { get; set; } = 0;

    /// <summary>
    /// Count of cached pull requests after last successful sync.
    /// </summary>
    public int PullRequestCount { get; set; } = 0;

    /// <summary>
    /// Count of cached pipeline definitions after last successful sync.
    /// </summary>
    public int PipelineCount { get; set; } = 0;

    /// <summary>
    /// Watermark for work item incremental sync (ChangedDate).
    /// </summary>
    public DateTimeOffset? WorkItemWatermark { get; set; }

    /// <summary>
    /// Watermark for pull request incremental sync (UpdatedDate).
    /// </summary>
    public DateTimeOffset? PullRequestWatermark { get; set; }

    /// <summary>
    /// Watermark for pipeline incremental sync (LastRunDate).
    /// </summary>
    public DateTimeOffset? PipelineWatermark { get; set; }

    /// <summary>
    /// Error message from last failed sync attempt.
    /// </summary>
    [MaxLength(2000)]
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// Current sync stage when SyncStatus is InProgress.
    /// </summary>
    [MaxLength(100)]
    public string? CurrentSyncStage { get; set; }

    /// <summary>
    /// Progress percentage within current stage (0-100).
    /// </summary>
    public int StageProgressPercent { get; set; } = 0;

    /// <summary>
    /// Navigation property to ProductOwner.
    /// </summary>
    public virtual ProfileEntity ProductOwner { get; set; } = null!;
}
