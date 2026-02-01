using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Represents a tag in the triage tag catalog.
/// These tags are available for use in the Bugs Triage UI.
/// </summary>
public class TriageTagEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Tag name (e.g., "Needs Investigation", "Customer Reported", "Regression").
    /// Must be unique (case-insensitive).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this tag is enabled/visible in the Bugs Triage UI.
    /// Disabled tags are hidden but not deleted.
    /// </summary>
    [Required]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Display order in the triage UI (lower values appear first).
    /// </summary>
    [Required]
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Timestamp when this tag was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; }
}
