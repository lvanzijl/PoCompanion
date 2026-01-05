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
}
