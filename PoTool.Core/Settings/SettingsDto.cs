namespace PoTool.Core.Settings;

/// <summary>
/// Immutable DTO for application settings.
/// </summary>
public sealed record SettingsDto(
    int Id,
    DataMode DataMode,
    List<int> ConfiguredGoalIds,
    DateTimeOffset LastModified
);
