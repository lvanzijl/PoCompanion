using PoTool.Core.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Repository interface for settings persistence.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Gets the current settings.
    /// </summary>
    Task<SettingsDto?> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active profile ID.
    /// </summary>
    Task<SettingsDto> SetActiveProfileAsync(int? profileId, CancellationToken cancellationToken = default);
}
