using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for Sprint (Iteration) persistence.
/// A Sprint represents a TFS team iteration with name, dates, and timeframe.
/// </summary>
public class SprintEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the Team this sprint belongs to.
    /// </summary>
    [Required]
    public int TeamId { get; set; }

    /// <summary>
    /// TFS iteration ID (GUID or stable identifier from TFS).
    /// May be null if TFS doesn't provide a stable ID.
    /// </summary>
    [MaxLength(100)]
    public string? TfsIterationId { get; set; }

    /// <summary>
    /// Full iteration path from TFS (e.g., "\Project\Sprint 12").
    /// Used as a stable key for upsert operations.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Sprint name (e.g., "Sprint 12").
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Sprint start date (UTC).
    /// Nullable because TFS may not provide attributes for all iterations.
    /// </summary>
    public DateTimeOffset? StartUtc { get; set; }

    /// <summary>
    /// Sprint end date (UTC).
    /// Nullable because TFS may not provide attributes for all iterations.
    /// </summary>
    public DateTimeOffset? EndUtc { get; set; }

    /// <summary>
    /// Time frame indicator from TFS: "past", "current", or "future".
    /// Nullable because TFS may not provide this field.
    /// </summary>
    [MaxLength(50)]
    public string? TimeFrame { get; set; }

    /// <summary>
    /// Timestamp when this sprint was last synced from TFS.
    /// </summary>
    [Required]
    public DateTimeOffset LastSyncedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Navigation property to the Team.
    /// </summary>
    public virtual TeamEntity Team { get; set; } = null!;
}
