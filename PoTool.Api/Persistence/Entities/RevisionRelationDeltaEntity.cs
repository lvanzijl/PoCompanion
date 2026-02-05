using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for storing relation changes within a revision.
/// Each row represents a relation that was added or removed in a specific revision.
/// </summary>
public class RevisionRelationDeltaEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the revision header.
    /// </summary>
    [Required]
    public int RevisionHeaderId { get; set; }

    /// <summary>
    /// Type of relation change.
    /// </summary>
    [Required]
    public RelationChangeType ChangeType { get; set; }

    /// <summary>
    /// The relation type reference name (e.g., "System.LinkTypes.Hierarchy-Reverse" for parent).
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string RelationType { get; set; } = string.Empty;

    /// <summary>
    /// The target work item ID for the relation.
    /// </summary>
    [Required]
    public int TargetWorkItemId { get; set; }

    /// <summary>
    /// Navigation property to the revision header.
    /// </summary>
    public virtual RevisionHeaderEntity RevisionHeader { get; set; } = null!;
}

/// <summary>
/// Type of change to a work item relation.
/// </summary>
public enum RelationChangeType
{
    /// <summary>
    /// Relation was added.
    /// </summary>
    Added = 0,

    /// <summary>
    /// Relation was removed.
    /// </summary>
    Removed = 1
}
