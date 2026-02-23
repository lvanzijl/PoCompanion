namespace PoTool.Shared.Settings;

/// <summary>
/// Request to reset specific cache entity types.
/// </summary>
public class CacheResetRequest
{
    /// <summary>
    /// Entity types to reset. If empty or null, resets everything.
    /// </summary>
    public List<string> EntityTypes { get; set; } = new();
}

/// <summary>
/// Response from a cache reset operation.
/// </summary>
public class CacheResetResponse
{
    /// <summary>
    /// Whether the reset succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Summary of what was reset.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Per-type counts of deleted entities.
    /// </summary>
    public List<CacheEntityCountDto> DeletedCounts { get; set; } = new();
}

/// <summary>
/// Known cache entity type names for granular reset.
/// </summary>
public static class CacheEntityTypes
{
    public const string WorkItems = "WorkItems";
    public const string PullRequests = "PullRequests";
    public const string Pipelines = "Pipelines";
    public const string Metrics = "Metrics";
    public const string Validations = "Validations";
    public const string SprintProjections = "SprintProjections";
    public const string Relationships = "Relationships";

    /// <summary>
    /// All available entity types for reset.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        WorkItems,
        PullRequests,
        Pipelines,
        Metrics,
        Validations,
        SprintProjections,
        Relationships
    };
}
