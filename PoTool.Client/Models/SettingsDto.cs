namespace PoTool.Client.Models;

/// <summary>
/// Client-side DTO for application settings.
/// </summary>
public sealed record SettingsDto(
    int Id,
    DataMode DataMode,
    List<int> ConfiguredGoalIds,
    DateTimeOffset LastModified
);
