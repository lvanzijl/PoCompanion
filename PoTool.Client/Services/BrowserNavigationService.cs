using Microsoft.JSInterop;

namespace PoTool.Client.Services;

/// <summary>
/// Service for browser navigation operations using JavaScript interop.
/// </summary>
public class BrowserNavigationService
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserNavigationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Opens a URL in a new browser tab/window.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OpenInNewTabAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
            return;

        await _jsRuntime.InvokeVoidAsync("open", url, "_blank");
    }
}
