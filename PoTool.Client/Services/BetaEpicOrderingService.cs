using Microsoft.JSInterop;

namespace PoTool.Client.Services;

/// <summary>
/// Service for persisting epic ordering in local storage.
/// Decision #8: Epic ordering is stored as a view preference (local storage only).
/// </summary>
public interface IBetaEpicOrderingService
{
    /// <summary>
    /// Gets the saved epic order for a given profile.
    /// </summary>
    Task<List<int>> GetEpicOrderAsync(int profileId);
    
    /// <summary>
    /// Saves the epic order for a given profile.
    /// </summary>
    Task SaveEpicOrderAsync(int profileId, List<int> epicIds);
    
    /// <summary>
    /// Clears the saved epic order for a given profile.
    /// </summary>
    Task ClearEpicOrderAsync(int profileId);
}

/// <summary>
/// Implementation of IBetaEpicOrderingService using browser localStorage.
/// </summary>
public class BetaEpicOrderingService : IBetaEpicOrderingService
{
    private readonly IJSRuntime _jsRuntime;
    private const string StorageKeyPrefix = "beta-epic-ordering-";

    public BetaEpicOrderingService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<List<int>> GetEpicOrderAsync(int profileId)
    {
        try
        {
            var key = GetStorageKey(profileId);
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
            
            if (string.IsNullOrEmpty(json))
            {
                return new List<int>();
            }
            
            return System.Text.Json.JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
        }
        catch
        {
            // If localStorage fails, return empty list
            return new List<int>();
        }
    }

    public async Task SaveEpicOrderAsync(int profileId, List<int> epicIds)
    {
        try
        {
            var key = GetStorageKey(profileId);
            var json = System.Text.Json.JsonSerializer.Serialize(epicIds);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
        }
        catch
        {
            // Silently fail if localStorage is not available
        }
    }

    public async Task ClearEpicOrderAsync(int profileId)
    {
        try
        {
            var key = GetStorageKey(profileId);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch
        {
            // Silently fail if localStorage is not available
        }
    }

    private static string GetStorageKey(int profileId) => $"{StorageKeyPrefix}{profileId}";
}
