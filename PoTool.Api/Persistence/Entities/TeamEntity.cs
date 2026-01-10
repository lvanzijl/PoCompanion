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
    /// Navigation property to linked products (many-to-many).
    /// </summary>
    public virtual ICollection<ProductTeamLinkEntity> ProductTeamLinks { get; set; } = new List<ProductTeamLinkEntity>();
}
