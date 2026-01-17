using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity representing a time bucket (weekly iteration) for filtering PRs.
/// Uses ISO 8601 week numbering for consistent week boundaries.
/// </summary>
public class TimeframeIterationEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Year (e.g., 2025).
    /// </summary>
    [Required]
    public int Year { get; set; }

    /// <summary>
    /// ISO week number (1-53).
    /// </summary>
    [Required]
    public int WeekNumber { get; set; }

    /// <summary>
    /// Start of the week (Monday, UTC).
    /// </summary>
    [Required]
    public DateTimeOffset StartUtc { get; set; }

    /// <summary>
    /// End of the week (Sunday 23:59:59, UTC).
    /// </summary>
    [Required]
    public DateTimeOffset EndUtc { get; set; }

    /// <summary>
    /// Stable identifier for this iteration (format: "YYYY-Wnn", e.g., "2025-W03").
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string IterationKey { get; set; } = string.Empty;
}
