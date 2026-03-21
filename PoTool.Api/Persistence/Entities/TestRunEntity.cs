using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Raw test run facts linked to a cached pipeline run build anchor.
/// </summary>
public class TestRunEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the cached pipeline run build anchor.
    /// </summary>
    [Required]
    public int BuildId { get; set; }

    /// <summary>
    /// Stable external identifier when the source exposes one.
    /// </summary>
    public int? ExternalId { get; set; }

    [Required]
    public int TotalTests { get; set; }

    [Required]
    public int PassedTests { get; set; }

    [Required]
    public int NotApplicableTests { get; set; }

    /// <summary>
    /// Optional UTC timestamp from the raw test run payload.
    /// </summary>
    public DateTime? Timestamp { get; set; }

    [Required]
    public DateTimeOffset CachedAt { get; set; }

    public virtual CachedPipelineRunEntity Build { get; set; } = null!;
}
