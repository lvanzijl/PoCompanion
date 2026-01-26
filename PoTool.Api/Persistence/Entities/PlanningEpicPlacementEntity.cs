using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Entity representing an Epic's placement on the new Planning Board.
/// An Epic can only appear in the column matching its Product.
/// </summary>
public class PlanningEpicPlacementEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The TFS ID of the Epic work item.
    /// </summary>
    [Required]
    public int EpicId { get; set; }

    /// <summary>
    /// The Product ID this Epic belongs to (determines column).
    /// </summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Foreign key to the Product.
    /// </summary>
    [ForeignKey(nameof(ProductId))]
    public virtual ProductEntity? Product { get; set; }

    /// <summary>
    /// Foreign key to the BoardRow.
    /// </summary>
    [Required]
    public int RowId { get; set; }

    /// <summary>
    /// Navigation property to the BoardRow.
    /// </summary>
    [ForeignKey(nameof(RowId))]
    public virtual BoardRowEntity? Row { get; set; }

    /// <summary>
    /// Order within the cell (for multiple epics in same cell).
    /// </summary>
    [Required]
    public int OrderInCell { get; set; }

    /// <summary>
    /// Timestamp when this placement was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this placement was last modified.
    /// </summary>
    [Required]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
}
