using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Tracks triage state for bugs locally (not persisted to TFS).
/// Used to determine if a bug has been triaged based on user actions in the Bugs Triage UI.
/// </summary>
public class BugTriageStateEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// TFS Bug ID that this triage state relates to.
    /// </summary>
    [Required]
    public int BugId { get; set; }

    /// <summary>
    /// Timestamp when this bug was first seen/loaded in the triage UI.
    /// </summary>
    [Required]
    public DateTimeOffset FirstSeenAt { get; set; }

    /// <summary>
    /// The criticality value observed when the bug was first loaded.
    /// Used to detect if the user has changed criticality from the initial value.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string FirstObservedCriticality { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this bug has been triaged by the user.
    /// Set to true when the user changes criticality or toggles any triage tag.
    /// </summary>
    [Required]
    public bool IsTriaged { get; set; }

    /// <summary>
    /// Timestamp of the last triage action (criticality change or tag toggle).
    /// Nullable if the bug has never been triaged.
    /// </summary>
    public DateTimeOffset? LastTriageActionAt { get; set; }
}
