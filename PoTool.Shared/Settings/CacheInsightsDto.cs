namespace PoTool.Shared.Settings;

/// <summary>
/// Detailed cache insights for a ProductOwner showing counts per entity type.
/// </summary>
public class CacheInsightsDto
{
    /// <summary>
    /// Product Owner ID.
    /// </summary>
    public int ProductOwnerId { get; set; }

    /// <summary>
    /// Counts per cached entity type.
    /// </summary>
    public List<CacheEntityCountDto> EntityCounts { get; set; } = new();

    /// <summary>
    /// Current sync status.
    /// </summary>
    public CacheSyncStatusDto SyncStatus { get; set; }

    /// <summary>
    /// Last successful sync timestamp.
    /// </summary>
    public DateTimeOffset? LastSuccessfulSync { get; set; }

    /// <summary>
    /// Last sync attempt timestamp.
    /// </summary>
    public DateTimeOffset? LastAttemptSync { get; set; }

    /// <summary>
    /// Last error message (if any).
    /// </summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// Current sync stage (when in progress).
    /// </summary>
    public string? CurrentSyncStage { get; set; }
}

/// <summary>
/// Count of cached entities for a specific type.
/// </summary>
public class CacheEntityCountDto
{
    /// <summary>
    /// Entity type name (e.g., "WorkItems", "Revisions").
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Total count of cached entities.
    /// </summary>
    public int TotalCount { get; set; }
}
