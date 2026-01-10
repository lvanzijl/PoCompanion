using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for user profile persistence.
/// A profile represents a set of area paths, a team, and selected goals.
/// </summary>
public class ProfileEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Profile name (user-defined, e.g., "Product A - Team Alpha").
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of area paths for this profile.
    /// User can select one or more area paths representing their products.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string AreaPaths { get; set; } = string.Empty;

    /// <summary>
    /// The team responsible for the area paths in this profile.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string TeamName { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of Goal IDs applicable to this profile.
    /// Empty string means all goals are applicable.
    /// </summary>
    [MaxLength(1000)]
    public string GoalIds { get; set; } = string.Empty;

    /// <summary>
    /// Type of profile picture: 0 = Default, 1 = Custom.
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
    /// Timestamp when this profile was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this profile was last modified.
    /// </summary>
    [Required]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Navigation property to products owned by this Product Owner.
    /// </summary>
    public virtual ICollection<ProductEntity> Products { get; set; } = new List<ProductEntity>();
}
