using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for effort estimation settings persistence.
/// </summary>
public class EffortEstimationSettingsEntity
{
    private DateTimeOffset _lastModified;

    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Default effort for Task work items.
    /// </summary>
    public int DefaultEffortTask { get; set; } = 3;

    /// <summary>
    /// Default effort for Bug work items.
    /// </summary>
    public int DefaultEffortBug { get; set; } = 3;

    /// <summary>
    /// Default effort for User Story work items.
    /// </summary>
    public int DefaultEffortUserStory { get; set; } = 5;

    /// <summary>
    /// Default effort for PBI (Product Backlog Item) work items.
    /// </summary>
    public int DefaultEffortPBI { get; set; } = 5;

    /// <summary>
    /// Default effort for Feature work items.
    /// </summary>
    public int DefaultEffortFeature { get; set; } = 13;

    /// <summary>
    /// Default effort for Epic work items.
    /// </summary>
    public int DefaultEffortEpic { get; set; } = 21;

    /// <summary>
    /// Default effort for unrecognized work item types.
    /// </summary>
    public int DefaultEffortGeneric { get; set; } = 5;

    /// <summary>
    /// Whether to enable proactive notifications for work items without effort.
    /// </summary>
    public bool EnableProactiveNotifications { get; set; } = true;

    /// <summary>
    /// Timestamp when settings were last modified.
    /// </summary>
    [Required]
    public DateTimeOffset LastModified
    {
        get => _lastModified;
        set
        {
            _lastModified = value;
            LastModifiedUtc = value.UtcDateTime;
        }
    }

    /// <summary>
    /// UTC timestamp used for SQL-translatable ordering on SQLite.
    /// </summary>
    [Required]
    public DateTime LastModifiedUtc { get; set; }
}
