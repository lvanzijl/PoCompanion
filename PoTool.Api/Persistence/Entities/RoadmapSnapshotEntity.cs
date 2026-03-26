using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities.UiRoadmap;

/// <summary>
/// A point-in-time capture of the roadmap state.
/// Stored in the application database. Never modifies TFS.
/// Not part of the CDC / analytics domain.
/// </summary>
public class RoadmapSnapshotEntity
{
    /// <summary>
    /// Primary key (auto-generated).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Timestamp when the snapshot was created.
    /// </summary>
    [Required]
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Optional description (e.g. "Q1 planning session").
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Child items captured in this snapshot.
    /// </summary>
    public ICollection<RoadmapSnapshotItemEntity> Items { get; set; } = [];
}
