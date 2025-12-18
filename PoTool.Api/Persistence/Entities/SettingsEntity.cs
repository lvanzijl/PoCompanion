using System.ComponentModel.DataAnnotations;
using PoTool.Core.Settings;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for application settings persistence.
/// </summary>
public class SettingsEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The active data mode (Mock or TFS).
    /// </summary>
    [Required]
    public DataMode DataMode { get; set; }

    /// <summary>
    /// Comma-separated list of configured Goal IDs.
    /// </summary>
    [MaxLength(1000)]
    public string ConfiguredGoalIds { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when settings were last modified.
    /// </summary>
    [Required]
    public DateTimeOffset LastModified { get; set; }
}
