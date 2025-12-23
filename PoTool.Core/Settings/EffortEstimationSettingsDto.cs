namespace PoTool.Core.Settings;

/// <summary>
/// Configuration for default effort estimation values by work item type.
/// Used when no historical data is available for a work item type.
/// </summary>
public sealed record EffortEstimationSettingsDto(
    int DefaultEffortTask,
    int DefaultEffortBug,
    int DefaultEffortUserStory,
    int DefaultEffortPBI,
    int DefaultEffortFeature,
    int DefaultEffortEpic,
    int DefaultEffortGeneric,
    bool EnableProactiveNotifications
)
{
    /// <summary>
    /// Gets the default settings with standard Fibonacci-based effort values.
    /// </summary>
    public static EffortEstimationSettingsDto Default => new(
        DefaultEffortTask: 3,
        DefaultEffortBug: 3,
        DefaultEffortUserStory: 5,
        DefaultEffortPBI: 5,
        DefaultEffortFeature: 13,
        DefaultEffortEpic: 21,
        DefaultEffortGeneric: 5,
        EnableProactiveNotifications: true
    );

    /// <summary>
    /// Gets the default effort for a given work item type.
    /// </summary>
    public int GetDefaultEffortForType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "task" => DefaultEffortTask,
            "bug" => DefaultEffortBug,
            "user story" => DefaultEffortUserStory,
            "product backlog item" => DefaultEffortPBI,
            "pbi" => DefaultEffortPBI,
            "feature" => DefaultEffortFeature,
            "epic" => DefaultEffortEpic,
            _ => DefaultEffortGeneric
        };
    }
}
