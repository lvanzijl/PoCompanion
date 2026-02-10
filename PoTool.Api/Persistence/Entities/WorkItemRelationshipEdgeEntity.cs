using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Snapshot edge representing a work item relationship at a point in time.
/// Captured from the Work Items API (relations) and scoped to a ProductOwner.
/// </summary>
public class WorkItemRelationshipEdgeEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// ProductOwner (Profile) scope for this snapshot edge.
    /// </summary>
    [Required]
    public int ProductOwnerId { get; set; }

    /// <summary>
    /// The work item that owns the relation (source).
    /// </summary>
    [Required]
    public int SourceWorkItemId { get; set; }

    /// <summary>
    /// Target work item ID for the relation (may be null for relations without a work item target).
    /// </summary>
    public int? TargetWorkItemId { get; set; }

    /// <summary>
    /// Relation type reference name (e.g., System.LinkTypes.Hierarchy-Reverse).
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string RelationType { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp indicating when this snapshot edge was captured.
    /// </summary>
    [Required]
    public DateTimeOffset SnapshotAsOfUtc { get; set; }
}
