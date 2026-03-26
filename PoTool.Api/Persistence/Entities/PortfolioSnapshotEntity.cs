using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Durable CDC portfolio snapshot header for one product at one captured source/timestamp.
/// </summary>
public class PortfolioSnapshotEntity
{
    /// <summary>
    /// Stable snapshot identifier and primary key.
    /// </summary>
    [Key]
    public long SnapshotId { get; set; }

    /// <summary>
    /// Queryable UTC snapshot timestamp.
    /// </summary>
    [Required]
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Product scope of the persisted snapshot header.
    /// </summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Source label used to explain where the snapshot came from.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Optional creator identity if available.
    /// </summary>
    [MaxLength(200)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Archived snapshots remain queryable but are excluded from default selection.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Navigation to the scoped product.
    /// </summary>
    public ProductEntity Product { get; set; } = null!;

    /// <summary>
    /// Immutable snapshot rows captured with this header.
    /// </summary>
    public ICollection<PortfolioSnapshotItemEntity> Items { get; set; } = [];
}
