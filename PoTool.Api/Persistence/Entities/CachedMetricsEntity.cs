using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for caching computed metrics per ProductOwner.
/// </summary>
public class CachedMetricsEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to ProfileEntity (ProductOwner).
    /// </summary>
    [Required]
    public int ProductOwnerId { get; set; }

    /// <summary>
    /// Name of the metric (e.g., "Velocity7d", "PrThroughput").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Computed metric value.
    /// </summary>
    [Required]
    public decimal MetricValue { get; set; }

    /// <summary>
    /// Unit of measurement (e.g., "points", "count", "percent").
    /// </summary>
    [MaxLength(50)]
    public string? Unit { get; set; }

    /// <summary>
    /// Timestamp when this metric was computed.
    /// </summary>
    public DateTimeOffset ComputedAt { get; set; }

    /// <summary>
    /// Navigation property to ProductOwner.
    /// </summary>
    public virtual ProfileEntity ProductOwner { get; set; } = null!;
}
