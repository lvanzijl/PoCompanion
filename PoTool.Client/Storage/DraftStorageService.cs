using Microsoft.JSInterop;
using System.Text.Json;

namespace PoTool.Client.Storage;

/// <summary>
/// Browser-based draft storage service using localStorage.
/// Persists form state across navigation and page refreshes.
/// </summary>
public class DraftStorageService
{
    private readonly IJSRuntime _jsRuntime;
    private const string DraftKeyPrefix = "draft_";

    public DraftStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <summary>
    /// Saves a draft to localStorage.
    /// </summary>
    /// <typeparam name="T">Type of draft data</typeparam>
    /// <param name="key">Unique key for the draft (e.g., "product_edit_123")</param>
    /// <param name="data">Data to save</param>
    public async Task SaveDraftAsync<T>(string key, T data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", $"{DraftKeyPrefix}{key}", json);
        }
        catch (Exception)
        {
            // Silently fail - draft persistence is not critical
        }
    }

    /// <summary>
    /// Loads a draft from localStorage.
    /// </summary>
    /// <typeparam name="T">Type of draft data</typeparam>
    /// <param name="key">Unique key for the draft</param>
    /// <returns>Draft data or null if not found</returns>
    public async Task<T?> LoadDraftAsync<T>(string key)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>($"localStorage.getItem", $"{DraftKeyPrefix}{key}");
            if (string.IsNullOrEmpty(json)) return default;
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception)
        {
            // Silently fail - draft persistence is not critical
            return default;
        }
    }

    /// <summary>
    /// Clears a draft from localStorage.
    /// </summary>
    /// <param name="key">Unique key for the draft</param>
    public async Task ClearDraftAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", $"{DraftKeyPrefix}{key}");
        }
        catch (Exception)
        {
            // Silently fail - draft persistence is not critical
        }
    }

    /// <summary>
    /// Clears all drafts from localStorage.
    /// </summary>
    public async Task ClearAllDraftsAsync()
    {
        try
        {
            // Get all keys from localStorage
            var keys = await _jsRuntime.InvokeAsync<string[]>("Object.keys", await _jsRuntime.InvokeAsync<object>("localStorage"));
            
            // Remove all draft keys
            foreach (var key in keys)
            {
                if (key.StartsWith(DraftKeyPrefix))
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
                }
            }
        }
        catch (Exception)
        {
            // Silently fail - draft persistence is not critical
        }
    }
}
