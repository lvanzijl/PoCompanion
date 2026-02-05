using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for storing field changes within a revision.
/// Each row represents a single field that changed in a specific revision.
/// </summary>
public class RevisionFieldDeltaEntity
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
    /// The field reference name (e.g., "System.State", "Microsoft.VSTS.Scheduling.Effort").
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// The previous value of the field (null if newly added).
    /// </summary>
    [MaxLength(4000)]
    public string? OldValue { get; set; }

    /// <summary>
    /// The new value of the field (null if removed).
    /// </summary>
    [MaxLength(4000)]
    public string? NewValue { get; set; }

    /// <summary>
    /// Navigation property to the revision header.
    /// </summary>
    public virtual RevisionHeaderEntity RevisionHeader { get; set; } = null!;
}
