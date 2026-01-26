using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Scope of the planning board view.
/// </summary>
public enum PlanningBoardScope
{
    /// <summary>
    /// Show all visible products as columns.
    /// </summary>
    AllProducts = 0,

    /// <summary>
    /// Show only a single selected product.
    /// </summary>
    SingleProduct = 1
}

/// <summary>
/// Entity storing Planning Board settings for a Product Owner.
/// </summary>
public class PlanningBoardSettingsEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Product Owner profile.
    /// </summary>
    [Required]
    public int ProductOwnerId { get; set; }

    /// <summary>
    /// Navigation property to the Product Owner.
    /// </summary>
    [ForeignKey(nameof(ProductOwnerId))]
    public virtual ProfileEntity? ProductOwner { get; set; }

    /// <summary>
    /// Current scope of the board view.
    /// </summary>
    [Required]
    public PlanningBoardScope Scope { get; set; } = PlanningBoardScope.AllProducts;

    /// <summary>
    /// When scope is SingleProduct, which product is selected.
    /// </summary>
    public int? SelectedProductId { get; set; }

    /// <summary>
    /// JSON-serialized list of hidden product IDs (for AllProducts scope).
    /// </summary>
    [MaxLength(4000)]
    public string? HiddenProductIdsJson { get; set; }

    /// <summary>
    /// Timestamp when settings were last modified.
    /// </summary>
    [Required]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
}
