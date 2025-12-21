using Microsoft.JSInterop;
using PoTool.Core.Contracts;

namespace PoTool.Client.Services;

/// <summary>
/// Service for clipboard operations using JavaScript interop.
/// </summary>
public class ClipboardService : IClipboardService
{
    private readonly IJSRuntime _jsRuntime;

    public ClipboardService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Copies the specified text to the system clipboard using the Clipboard API.
    /// </summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CopyToClipboardAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
    }
}
