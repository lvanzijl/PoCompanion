using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for storing work item revision headers.
/// Each row represents a single revision of a work item.
/// </summary>
public class RevisionHeaderEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// TFS work item ID.
    /// </summary>
    [Required]
    public int WorkItemId { get; set; }

    /// <summary>
    /// Revision number within the work item.
    /// </summary>
    [Required]
    public int RevisionNumber { get; set; }

    /// <summary>
    /// Work item type (e.g., "Epic", "Feature", "Product Backlog Item", "Bug", "Task").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string WorkItemType { get; set; } = string.Empty;

    /// <summary>
    /// Work item title at this revision.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Work item state at this revision (e.g., "New", "Active", "Done").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Reason for state change (if applicable).
    /// </summary>
    [MaxLength(200)]
    public string? Reason { get; set; }

    /// <summary>
    /// Iteration path at this revision.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string IterationPath { get; set; } = string.Empty;

    /// <summary>
    /// Area path at this revision.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string AreaPath { get; set; } = string.Empty;

    /// <summary>
    /// When the revision was created (System.CreatedDate from first revision).
    /// </summary>
    public DateTimeOffset? CreatedDate { get; set; }

    /// <summary>
    /// When this revision was made (System.ChangedDate).
    /// </summary>
    [Required]
    public DateTimeOffset ChangedDate { get; set; }

    /// <summary>
    /// When the work item was closed (if applicable).
    /// </summary>
    public DateTimeOffset? ClosedDate { get; set; }

    /// <summary>
    /// Effort value at this revision (Microsoft.VSTS.Scheduling.Effort).
    /// </summary>
    public double? Effort { get; set; }

    /// <summary>
    /// Tags at this revision (semicolon-separated).
    /// </summary>
    [MaxLength(2000)]
    public string? Tags { get; set; }

    /// <summary>
    /// Severity at this revision (for bugs).
    /// </summary>
    [MaxLength(100)]
    public string? Severity { get; set; }

    /// <summary>
    /// Display name of who made this change.
    /// </summary>
    [MaxLength(256)]
    public string? ChangedBy { get; set; }

    /// <summary>
    /// When this revision was ingested into the local database.
    /// </summary>
    [Required]
    public DateTimeOffset IngestedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Navigation property to field deltas for this revision.
    /// </summary>
    public virtual ICollection<RevisionFieldDeltaEntity> FieldDeltas { get; set; } = new List<RevisionFieldDeltaEntity>();

    /// <summary>
    /// Navigation property to relation deltas for this revision.
    /// </summary>
    public virtual ICollection<RevisionRelationDeltaEntity> RelationDeltas { get; set; } = new List<RevisionRelationDeltaEntity>();
}
