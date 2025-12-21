namespace PoTool.Core.Contracts;

/// <summary>
/// Service for clipboard operations.
/// Provides cross-platform clipboard functionality for copying text to the system clipboard.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies the specified text to the system clipboard.
    /// </summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CopyToClipboardAsync(string text);
}
