using MudBlazor;

namespace PoTool.Client.Models;

/// <summary>
/// UI metadata helpers for validation categories.
/// Centralises icon, color, and severity mappings so they are consistent across
/// Validation Triage, Validation Queue, and Validation Fix Session pages.
/// </summary>
public static class ValidationCategoryMeta
{
    /// <summary>Returns the MudBlazor icon string for a category key.</summary>
    public static string GetIcon(string categoryKey) => categoryKey.ToUpperInvariant() switch
    {
        "SI"  => Icons.Material.Filled.Error,
        "RR"  => Icons.Material.Filled.Warning,
        "RC"  => Icons.Material.Filled.AssignmentLate,
        "EFF" => Icons.Material.Filled.HourglassEmpty,
        _     => Icons.Material.Filled.Info
    };

    /// <summary>Returns the MudBlazor color for a category key.</summary>
    public static Color GetColor(string categoryKey) => categoryKey.ToUpperInvariant() switch
    {
        "SI"  => Color.Error,
        "RR"  => Color.Warning,
        "RC"  => Color.Warning,
        "EFF" => Color.Info,
        _     => Color.Default
    };

    /// <summary>Returns the MudBlazor alert severity for a category key.</summary>
    public static Severity GetAlertSeverity(string categoryKey) => categoryKey.ToUpperInvariant() switch
    {
        "SI"  => Severity.Error,
        "RR"  => Severity.Warning,
        "RC"  => Severity.Warning,
        "EFF" => Severity.Info,
        _     => Severity.Normal
    };
}
