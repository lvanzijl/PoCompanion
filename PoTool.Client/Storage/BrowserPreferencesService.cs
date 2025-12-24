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

    public bool GetBool(string key, bool defaultValue)
    {
        try
        {
            var value = _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key).AsTask().Result;
            if (value == null) return defaultValue;
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public void SetBool(string key, bool value)
    {
        _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value.ToString()).AsTask().Wait();
    }

    public void Remove(string key)
    {
        _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key).AsTask().Wait();
    }
}
