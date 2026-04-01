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
    /// Null indicates an orphaned product (not currently owned by any Product Owner).
    /// </summary>
    public int? ProductOwnerId { get; set; }

    /// <summary>
    /// Product name.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to the project that owns the product.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string ProjectId { get; set; } = string.Empty;

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
    /// Timestamp of the last successful sync for this product.
    /// Null if the product has never been synced.
    /// Used to determine whether to perform Full Sync (null) or Incremental Sync (has value).
    /// </summary>
    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>
    /// Explicit configured estimation mode for the product.
    /// </summary>
    public int EstimationMode { get; set; } = (int)Shared.Settings.EstimationMode.StoryPoints;

    /// <summary>
    /// Navigation property to the Product Owner.
    /// Null if product is orphaned.
    /// </summary>
    public virtual ProfileEntity? ProductOwner { get; set; }

    /// <summary>
    /// Navigation property to the project that owns this product.
    /// </summary>
    public virtual ProjectEntity? Project { get; set; }

    /// <summary>
    /// Navigation property to linked teams (many-to-many).
    /// </summary>
    public virtual ICollection<ProductTeamLinkEntity> ProductTeamLinks { get; set; } = new List<ProductTeamLinkEntity>();

    /// <summary>
    /// Navigation property to configured repositories.
    /// </summary>
    public virtual ICollection<RepositoryEntity> Repositories { get; set; } = new List<RepositoryEntity>();

    /// <summary>
    /// Navigation property to backlog root work item IDs.
    /// A product may have one or more backlog roots that define the scope of its product backlog.
    /// </summary>
    public virtual ICollection<ProductBacklogRootEntity> BacklogRoots { get; set; } = new List<ProductBacklogRootEntity>();
}
