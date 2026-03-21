using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Raw coverage facts linked to a cached pipeline run build anchor.
/// </summary>
public class CoverageEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the cached pipeline run build anchor.
    /// </summary>
    [Required]
    public int BuildId { get; set; }

    [Required]
    public int CoveredLines { get; set; }

    [Required]
    public int TotalLines { get; set; }

    /// <summary>
    /// Optional UTC timestamp from the raw coverage payload.
    /// </summary>
    public DateTime? Timestamp { get; set; }

    [Required]
    public DateTimeOffset CachedAt { get; set; }

    public virtual CachedPipelineRunEntity Build { get; set; } = null!;
}
