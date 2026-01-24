using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for work item persistence.
/// </summary>
public class WorkItemEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// TFS work item ID (unique).
    /// </summary>
    [Required]
    public int TfsId { get; set; }

    /// <summary>
    /// Parent TFS work item ID (nullable) for hierarchy.
    /// </summary>
    public int? ParentTfsId { get; set; }

    /// <summary>
    /// Work item type (Goal, Objective, Epic, Feature, PBI, Task, etc.).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Work item title.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Area path from TFS.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string AreaPath { get; set; } = string.Empty;

    /// <summary>
    /// Iteration path from TFS.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string IterationPath { get; set; } = string.Empty;

    /// <summary>
    /// Current state (Active, Closed, etc.).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Full JSON payload from TFS for detail view.
    /// </summary>
    [Required]
    public string JsonPayload { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this work item was retrieved from TFS.
    /// </summary>
    [Required]
    public DateTimeOffset RetrievedAt { get; set; }

    /// <summary>
    /// Effort estimate in hours (nullable).
    /// </summary>
    public int? Effort { get; set; }

    /// <summary>
    /// Work item description (nullable).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// TFS revision number for optimistic concurrency.
    /// Used for future write-back functionality.
    /// </summary>
    public int TfsRevision { get; set; }

    /// <summary>
    /// TFS changed date for conflict detection.
    /// Used for future write-back functionality.
    /// </summary>
    public DateTimeOffset TfsChangedDate { get; set; }

    /// <summary>
    /// HTTP ETag from TFS for PATCH preconditions.
    /// Used for future write-back functionality.
    /// </summary>
    [MaxLength(100)]
    public string? TfsETag { get; set; }
}
