namespace PoTool.Client.Helpers;

/// <summary>
/// Shared formatting helpers used by PR Delivery Insights pages and components.
/// Centralises display logic so that the page and the SVG tooltip stay consistent.
/// </summary>
public static class PrFormatHelpers
{
    /// <summary>
    /// Formats a PR lifetime in hours as a human-readable string.
    /// Examples: "n/a", "2h", "3d 4h", "1w 2d", "2w".
    /// </summary>
    public static string FormatLifetime(double hours)
    {
        if (hours <= 0) return "n/a";
        if (hours >= 168)
        {
            int w = (int)(hours / 168);
            int d = (int)((hours % 168) / 24);
            return d > 0 ? $"{w}w {d}d" : $"{w}w";
        }
        if (hours >= 24)
        {
            int d  = (int)(hours / 24);
            int hr = (int)(hours % 24);
            return hr > 0 ? $"{d}d {hr}h" : $"{d}d";
        }
        return $"{hours:F0}h";
    }

    /// <summary>
    /// Returns a human-readable label for a raw PR status string.
    /// Handles "completed" → "Merged", "abandoned" → "Abandoned", "active" → "Active".
    /// </summary>
    public static string FormatStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "completed" => "Merged",
            "abandoned" => "Abandoned",
            "active"    => "Active",
            _           => status
        };

    /// <summary>
    /// Returns a human-readable label for a PR delivery category.
    /// "DeliveryMapped" is shortened to "Delivery" because the full identifier
    /// is an internal classification term; the PO-facing label is more concise.
    /// Bug, Disturbance, and Unmapped keep their original names.
    /// </summary>
    public static string FormatCategory(string category) =>
        category switch
        {
            "DeliveryMapped" => "Delivery",
            "Bug"            => "Bug",
            "Disturbance"    => "Disturbance",
            "Unmapped"       => "Unmapped",
            _                => category
        };
}
