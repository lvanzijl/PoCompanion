using Microsoft.JSInterop;
using PoTool.Client.Services;

namespace PoTool.Client.Storage;

/// <summary>
/// Browser-based preferences service using localStorage.
/// </summary>
public class BrowserPreferencesService : IPreferencesService
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserPreferencesService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue)
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
            if (value == null) return defaultValue;
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task SetBoolAsync(string key, bool value)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value.ToString());
    }

    public async Task<int?> GetIntAsync(string key)
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
            if (value == null) return null;
            return int.TryParse(value, out var result) ? result : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetIntAsync(string key, int value)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value.ToString());
    }

    public async Task RemoveAsync(string key)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
    }
}
