using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for storing canonical PortfolioFlow stock/flow projections per sprint and product.
/// </summary>
public class PortfolioFlowProjectionEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }

    [Required]
    public int SprintId { get; set; }

    [Required]
    public double StockStoryPoints { get; set; }

    [Required]
    public double RemainingScopeStoryPoints { get; set; }

    [Required]
    public double InflowStoryPoints { get; set; }

    [Required]
    public double ThroughputStoryPoints { get; set; }

    public double? CompletionPercent { get; set; }

    [Required]
    public DateTimeOffset ProjectionTimestamp { get; set; } = DateTimeOffset.UtcNow;

    public virtual ProductEntity Product { get; set; } = null!;

    public virtual SprintEntity Sprint { get; set; } = null!;
}
