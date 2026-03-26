using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities.UiRoadmap;

/// <summary>
/// A single epic entry within a roadmap snapshot.
/// Not part of the CDC / analytics domain.
/// </summary>
public class RoadmapSnapshotItemEntity
{
    /// <summary>
    /// Primary key (auto-generated).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the parent snapshot.
    /// </summary>
    [Required]
    public int SnapshotId { get; set; }

    /// <summary>
    /// Product name at the time of snapshot.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// TFS ID of the epic.
    /// </summary>
    [Required]
    public int EpicTfsId { get; set; }

    /// <summary>
    /// Epic title at the time of snapshot.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string EpicTitle { get; set; } = string.Empty;

    /// <summary>
    /// Epic ordering position within its product (1-based).
    /// </summary>
    [Required]
    public int EpicOrder { get; set; }

    /// <summary>
    /// Navigation property to parent snapshot.
    /// </summary>
    public RoadmapSnapshotEntity Snapshot { get; set; } = null!;
}
