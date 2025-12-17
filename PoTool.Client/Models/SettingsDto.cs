namespace PoTool.Client.Models;

/// <summary>
/// Data transfer object for application settings.
/// NOTE: This is intentionally duplicated from Core.Settings.SettingsDto
/// as the Client layer cannot reference Core directly per architecture rules.
/// This serves as the API contract boundary. Keep in sync with Core definition.
/// </summary>
public record SettingsDto(
    int Id,
    DataMode DataMode,
    List<int> ConfiguredGoalIds,
    DateTimeOffset LastModified
);
