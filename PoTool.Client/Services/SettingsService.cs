using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing application settings via the API.
/// </summary>
public class SettingsService
{
    private readonly ISettingsClient _settingsClient;

    public SettingsService(ISettingsClient settingsClient)
    {
        _settingsClient = settingsClient;
    }

    /// <summary>
    /// Gets the current settings.
    /// </summary>
    public async Task<SettingsDto?> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _settingsClient.GetSettingsAsync(cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Updates the settings.
    /// </summary>
    public async Task<SettingsDto> UpdateSettingsAsync(DataMode dataMode, List<int> configuredGoalIds, CancellationToken cancellationToken = default)
    {
        var request = new UpdateSettingsRequest
        {
            DataMode = dataMode,
            ConfiguredGoalIds = configuredGoalIds
        };
        
        return await _settingsClient.UpdateSettingsAsync(request, cancellationToken);
    }

    /// <summary>
    /// Gets or creates default settings.
    /// </summary>
    public async Task<SettingsDto> GetOrCreateDefaultSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (settings != null)
        {
            return settings;
        }

        // Create default settings - empty goal list means show all goals
        return await UpdateSettingsAsync(DataMode.Mock, new List<int>(), cancellationToken);
    }
}
