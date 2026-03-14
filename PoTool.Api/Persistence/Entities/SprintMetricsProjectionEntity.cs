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
    /// Resolved story-point total for planned PBIs.
    /// Includes real, fallback, and derived estimates.
    /// </summary>
    [Required]
    public double PlannedStoryPoints { get; set; }

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
    /// Number of PBIs that transitioned to Done during the sprint.
    /// </summary>
    public int CompletedPbiCount { get; set; }

    /// <summary>
    /// Sum of effort of PBIs that transitioned to Done during the sprint.
    /// </summary>
    public int CompletedPbiEffort { get; set; }

    /// <summary>
    /// Delivered story points for PBIs whose first Done transition occurred during the sprint.
    /// Derived or missing estimates are excluded from this total.
    /// </summary>
    public double CompletedPbiStoryPoints { get; set; }

    /// <summary>
    /// Number of committed PBIs that spilled directly into the next sprint unfinished.
    /// </summary>
    public int SpilloverCount { get; set; }

    /// <summary>
    /// Sum of effort of committed PBIs that spilled directly into the next sprint unfinished.
    /// </summary>
    public int SpilloverEffort { get; set; }

    /// <summary>
    /// Resolved story-point total for committed PBIs that spilled unfinished into the next sprint.
    /// Includes real, fallback, and derived estimates when available.
    /// </summary>
    public double SpilloverStoryPoints { get; set; }

    /// <summary>
    /// Feature/Epic progression delta percentage (0-100 scale) within the sprint.
    /// Represents measurable forward movement.
    /// </summary>
    public double ProgressionDelta { get; set; }

    /// <summary>
    /// Number of new bugs created during the sprint timeframe.
    /// </summary>
    public int BugsCreatedCount { get; set; }

    /// <summary>
    /// Number of bugs closed during the sprint timeframe.
    /// </summary>
    public int BugsClosedCount { get; set; }

    /// <summary>
    /// Number of PBIs with missing effort that affected computation.
    /// </summary>
    public int MissingEffortCount { get; set; }

    /// <summary>
    /// Number of planned PBIs that still have no resolved story-point estimate.
    /// </summary>
    public int MissingStoryPointCount { get; set; }

    /// <summary>
    /// Number of planned PBIs whose story-point estimate was derived from sibling estimates.
    /// </summary>
    public int DerivedStoryPointCount { get; set; }

    /// <summary>
    /// Aggregate derived story points used for planned scope sizing.
    /// </summary>
    public double DerivedStoryPoints { get; set; }

    /// <summary>
    /// Number of delivered PBIs without an authoritative story-point estimate.
    /// Includes missing and derived deliveries.
    /// </summary>
    public int UnestimatedDeliveryCount { get; set; }

    /// <summary>
    /// Whether sibling-average approximation was used for missing effort.
    /// </summary>
    public bool IsApproximate { get; set; }

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
