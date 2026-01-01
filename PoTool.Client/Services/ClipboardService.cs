using Microsoft.JSInterop;
using PoTool.Shared.Contracts;

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
    /// <exception cref="InvalidOperationException">Thrown when the clipboard operation fails.</exception>
    public async Task CopyToClipboardAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException("Failed to copy to clipboard. The browser may not support the Clipboard API or permission was denied.", ex);
        }
    }
}
