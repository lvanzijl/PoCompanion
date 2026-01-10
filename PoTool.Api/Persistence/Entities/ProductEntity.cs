using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for Product persistence.
/// A Product represents a single Scrum product with a product backlog.
/// </summary>
public class ProductEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Product Owner (ProfileEntity).
    /// </summary>
    public int ProductOwnerId { get; set; }

    /// <summary>
    /// Product name.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The area path that defines the backlog scope.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string ProductAreaPath { get; set; } = string.Empty;

    /// <summary>
    /// Optional root work item ID for the backlog.
    /// Null means the product is "backlog-less" while being configured.
    /// </summary>
    public int? BacklogRootWorkItemId { get; set; }

    /// <summary>
    /// Explicit ordering of products within a Product Owner's list.
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Type of product picture: 0 = Default, 1 = Custom.
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
    /// Timestamp when this product was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this product was last modified.
    /// </summary>
    [Required]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Navigation property to the Product Owner.
    /// </summary>
    public virtual ProfileEntity ProductOwner { get; set; } = null!;

    /// <summary>
    /// Navigation property to linked teams (many-to-many).
    /// </summary>
    public virtual ICollection<ProductTeamLinkEntity> ProductTeamLinks { get; set; } = new List<ProductTeamLinkEntity>();
}
