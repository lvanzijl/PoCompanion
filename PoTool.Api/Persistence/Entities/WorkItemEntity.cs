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
    /// Timestamp when this work item was retrieved from TFS.
    /// </summary>
    [Required]
    public DateTimeOffset RetrievedAt { get; set; }

    /// <summary>
    /// Effort estimate in hours (nullable).
    /// </summary>
    public int? Effort { get; set; }

    /// <summary>
    /// Business value from TFS (Microsoft.VSTS.Common.BusinessValue).
    /// </summary>
    public int? BusinessValue { get; set; }

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

    /// <summary>
    /// Work item creation date from TFS (System.CreatedDate).
    /// Used for trend analysis and reporting.
    /// </summary>
    public DateTimeOffset? CreatedDate { get; set; }

    /// <summary>
    /// Work item closed date from TFS (Microsoft.VSTS.Common.ClosedDate).
    /// Used for tracking when bugs were fixed/completed.
    /// </summary>
    public DateTimeOffset? ClosedDate { get; set; }

    /// <summary>
    /// Work item severity from TFS (Microsoft.VSTS.Common.Severity).
    /// Used for bugs: "1 - Critical", "2 - High", "3 - Medium", "4 - Low".
    /// Nullable - only applicable to work items that support severity (e.g., Bugs).
    /// </summary>
    [MaxLength(50)]
    public string? Severity { get; set; }

    /// <summary>
    /// Work item tags from TFS (System.Tags).
    /// Tags are stored as a semicolon-separated string (e.g., "Tag1; Tag2; Tag3").
    /// Nullable - work items may not have any tags.
    /// </summary>
    [MaxLength(1000)]
    public string? Tags { get; set; }

    /// <summary>
    /// Indicates if the work item is blocked.
    /// Extracted from TFS custom field (e.g., "Microsoft.VSTS.CMMI.Blocked" = "Yes").
    /// Nullable - not all work items have blocking status.
    /// </summary>
    public bool? IsBlocked { get; set; }

    /// <summary>
    /// Work item relations (links to other work items).
    /// Stored as JSON array: [{"LinkType": "...", "TargetWorkItemId": 123, "Url": "..."}]
    /// Nullable - work items may not have any relations.
    /// </summary>
    public string? Relations { get; set; }
}
