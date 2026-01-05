namespace PoTool.Core.Settings;

/// <summary>
/// Immutable DTO for application settings.
/// </summary>
public sealed record SettingsDto(
    int Id,
    int? ActiveProfileId,
    DateTimeOffset LastModified
);
