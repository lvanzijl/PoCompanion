using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for storing sprint-level metrics projections.
/// Pre-computed metrics for efficient Sprint Trend display.
/// </summary>
public class SprintMetricsProjectionEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Sprint.
    /// </summary>
    [Required]
    public int SprintId { get; set; }

    /// <summary>
    /// Foreign key to the Product.
    /// </summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Number of work items planned for this sprint-product combination.
    /// </summary>
    [Required]
    public int PlannedCount { get; set; }

    /// <summary>
    /// Total effort of planned work items.
    /// </summary>
    [Required]
    public int PlannedEffort { get; set; }

    /// <summary>
    /// Number of work items with activity in this sprint-product combination.
    /// </summary>
    [Required]
    public int WorkedCount { get; set; }

    /// <summary>
    /// Total effort of worked work items.
    /// </summary>
    [Required]
    public int WorkedEffort { get; set; }

    /// <summary>
    /// Number of bugs planned for this sprint-product combination.
    /// </summary>
    [Required]
    public int BugsPlannedCount { get; set; }

    /// <summary>
    /// Number of bugs with activity in this sprint-product combination.
    /// </summary>
    [Required]
    public int BugsWorkedCount { get; set; }

    /// <summary>
    /// When this projection was last computed.
    /// </summary>
    [Required]
    public DateTimeOffset LastComputedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The highest revision number included in this projection.
    /// Used for incremental recomputation.
    /// </summary>
    [Required]
    public int IncludedUpToRevisionId { get; set; }

    /// <summary>
    /// Navigation property to the Sprint.
    /// </summary>
    public virtual SprintEntity Sprint { get; set; } = null!;

    /// <summary>
    /// Navigation property to the Product.
    /// </summary>
    public virtual ProductEntity Product { get; set; } = null!;
}
