using System.Net.Http.Json;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing application settings via the API.
/// </summary>
public class SettingsService
{
    private readonly HttpClient _httpClient;

    public SettingsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets the current settings.
    /// </summary>
    public async Task<SettingsDto?> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<SettingsDto>("/api/settings", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Updates the settings.
    /// </summary>
    public async Task<SettingsDto> UpdateSettingsAsync(DataMode dataMode, List<int> configuredGoalIds, CancellationToken cancellationToken = default)
    {
        var request = new { DataMode = dataMode, ConfiguredGoalIds = configuredGoalIds };
        var response = await _httpClient.PutAsJsonAsync("/api/settings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SettingsDto>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize settings response");
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

        // Create default settings
        return await UpdateSettingsAsync(DataMode.Mock, new List<int> { 1000, 2000 }, cancellationToken);
    }
}
