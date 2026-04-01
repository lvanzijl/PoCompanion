using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Persisted forecast projection variants for an Epic or Feature work item.
/// </summary>
public class ForecastProjectionEntity
{
    [Key]
    public int WorkItemId { get; set; }

    [Required]
    [MaxLength(100)]
    public string WorkItemType { get; set; } = string.Empty;

    [Required]
    public int SprintsRemaining { get; set; }

    public DateTimeOffset? EstimatedCompletionDate { get; set; }

    [Required]
    [MaxLength(20)]
    public string Confidence { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public string ProjectionVariantsJson { get; set; } = "[]";
}
