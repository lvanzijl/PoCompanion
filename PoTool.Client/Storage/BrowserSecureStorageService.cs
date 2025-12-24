using Microsoft.JSInterop;
using PoTool.Client.Services;

namespace PoTool.Client.Storage;

/// <summary>
/// Browser-based secure storage service using sessionStorage.
/// Note: sessionStorage is cleared when the browser tab/window is closed.
/// For sensitive data like PAT tokens, users must re-enter on each session.
/// </summary>
public class BrowserSecureStorageService : ISecureStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserSecureStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", key);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", key, value);
    }

    public bool Remove(string key)
    {
        try
        {
            _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", key).AsTask().Wait();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void RemoveAll()
    {
        _jsRuntime.InvokeVoidAsync("sessionStorage.clear").AsTask().Wait();
    }
}
