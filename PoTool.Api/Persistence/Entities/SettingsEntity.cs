using System.ComponentModel.DataAnnotations;

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
    /// The ID of the currently active profile.
    /// Nullable to support cases where no profile is selected.
    /// </summary>
    public int? ActiveProfileId { get; set; }

    /// <summary>
    /// Timestamp when settings were last modified.
    /// </summary>
    [Required]
    public DateTimeOffset LastModified { get; set; }
}
