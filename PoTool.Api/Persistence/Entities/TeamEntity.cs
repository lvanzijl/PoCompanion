using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for Team persistence.
/// A Team represents a delivery team that can work on multiple products.
/// </summary>
public class TeamEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Team name.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The area path that defines the team's work area.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TeamAreaPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the team is archived.
    /// Archived teams remain available for historical classification.
    /// </summary>
    public bool IsArchived { get; set; } = false;

    /// <summary>
    /// Type of team picture: 0 = Default, 1 = Custom.
    /// </summary>
    public int PictureType { get; set; } = 0;

    /// <summary>
    /// Index of the default picture (0-63) when PictureType is Default.
    /// </summary>
    public int DefaultPictureId { get; set; } = 0;

    /// <summary>
    /// Path to custom picture when PictureType is Custom.
    /// </summary>
    [MaxLength(512)]
    public string? CustomPicturePath { get; set; }

    /// <summary>
    /// Timestamp when this team was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this team was last modified.
    /// </summary>
    [Required]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// TFS/Azure DevOps project name that contains this team.
    /// Nullable - required only when linking to a real TFS team for sprint sync.
    /// </summary>
    [MaxLength(256)]
    public string? ProjectName { get; set; }

    /// <summary>
    /// TFS Team ID (GUID string) for stable reference.
    /// Nullable - required only when linking to a real TFS team for sprint sync.
    /// </summary>
    [MaxLength(100)]
    public string? TfsTeamId { get; set; }

    /// <summary>
    /// TFS Team Name for human readability.
    /// Nullable - required only when linking to a real TFS team for sprint sync.
    /// </summary>
    [MaxLength(200)]
    public string? TfsTeamName { get; set; }

    /// <summary>
    /// Timestamp when sprints (iterations) were last synced for this team.
    /// Nullable - null if never synced. Used for TTL-based incremental sprint refresh.
    /// </summary>
    public DateTimeOffset? LastSyncedIterationsUtc { get; set; }

    /// <summary>
    /// Navigation property to linked products (many-to-many).
    /// </summary>
    public virtual ICollection<ProductTeamLinkEntity> ProductTeamLinks { get; set; } = new List<ProductTeamLinkEntity>();

    /// <summary>
    /// Navigation property to sprints (iterations) for this team.
    /// </summary>
    public virtual ICollection<SprintEntity> Sprints { get; set; } = new List<SprintEntity>();
}
