using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for storing resolved hierarchical identifiers for work items.
/// This provides pre-computed parent-chain information for efficient metrics calculation.
/// </summary>
public class ResolvedWorkItemEntity
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
    /// Work item type (e.g., "Epic", "Feature", "Product Backlog Item", "Bug", "Task").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string WorkItemType { get; set; } = string.Empty;

    /// <summary>
    /// Resolved Product ID (internal database ID).
    /// Null if the work item could not be resolved to a product.
    /// </summary>
    public int? ResolvedProductId { get; set; }

    /// <summary>
    /// Resolved Epic TFS ID (if applicable).
    /// Null if work item is not under an Epic.
    /// </summary>
    public int? ResolvedEpicId { get; set; }

    /// <summary>
    /// Resolved Feature TFS ID (if applicable).
    /// Null if work item is not under a Feature.
    /// </summary>
    public int? ResolvedFeatureId { get; set; }

    /// <summary>
    /// Resolved Sprint ID (internal database ID).
    /// Based on the current iteration path of the work item.
    /// </summary>
    public int? ResolvedSprintId { get; set; }

    /// <summary>
    /// Status of the resolution process.
    /// </summary>
    [Required]
    public ResolutionStatus ResolutionStatus { get; set; }

    /// <summary>
    /// When this resolution was last computed.
    /// </summary>
    [Required]
    public DateTimeOffset LastResolvedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The revision number this resolution is based on.
    /// </summary>
    [Required]
    public int ResolvedAtRevision { get; set; }
}

/// <summary>
/// Status of the hierarchical resolution process.
/// </summary>
public enum ResolutionStatus
{
    /// <summary>
    /// Resolution has not been attempted yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Work item was successfully resolved to a product.
    /// </summary>
    Resolved = 1,

    /// <summary>
    /// Work item could not be resolved to a product (orphan).
    /// </summary>
    Orphan = 2,

    /// <summary>
    /// Resolution failed due to an error.
    /// </summary>
    Error = 3
}
