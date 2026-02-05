using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for tracking revision ingestion progress.
/// Stores watermarks for incremental synchronization.
/// </summary>
public class RevisionIngestionWatermarkEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Product Owner (ProfileEntity).
    /// Watermarks are tracked per Product Owner.
    /// </summary>
    [Required]
    public int ProductOwnerId { get; set; }

    /// <summary>
    /// The continuation token from the last successful ingestion.
    /// Used for paging across multiple ingestion calls.
    /// </summary>
    [MaxLength(2000)]
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// The start datetime for the next incremental sync.
    /// This is updated after each successful ingestion to enable incremental sync.
    /// </summary>
    public DateTimeOffset? LastSyncStartDateTime { get; set; }

    /// <summary>
    /// When the last successful ingestion started.
    /// </summary>
    public DateTimeOffset? LastIngestionStartedAt { get; set; }

    /// <summary>
    /// When the last successful ingestion completed.
    /// </summary>
    public DateTimeOffset? LastIngestionCompletedAt { get; set; }

    /// <summary>
    /// Total number of revisions ingested in the last ingestion.
    /// </summary>
    public int? LastIngestionRevisionCount { get; set; }

    /// <summary>
    /// Whether the initial historical backfill has been completed.
    /// </summary>
    [Required]
    public bool IsInitialBackfillComplete { get; set; }

    /// <summary>
    /// Last error message if ingestion failed.
    /// </summary>
    [MaxLength(2000)]
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// When the last error occurred.
    /// </summary>
    public DateTimeOffset? LastErrorAt { get; set; }

    /// <summary>
    /// Navigation property to the Product Owner.
    /// </summary>
    public virtual ProfileEntity ProductOwner { get; set; } = null!;
}
