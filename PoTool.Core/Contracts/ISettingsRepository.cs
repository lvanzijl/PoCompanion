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
    /// Saves or updates the settings.
    /// </summary>
    Task<SettingsDto> SaveSettingsAsync(DataMode dataMode, List<int> configuredGoalIds, CancellationToken cancellationToken = default);
}
