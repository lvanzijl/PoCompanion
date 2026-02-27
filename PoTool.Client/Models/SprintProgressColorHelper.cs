using MudBlazor;

namespace PoTool.Client.Models;

/// <summary>
/// Provides consistent MudBlazor color mapping for sprint progress percentages.
/// </summary>
public static class SprintProgressColorHelper
{
    /// <summary>
    /// Returns a MudBlazor Color based on a progress percentage:
    /// ≥ 90 → Success, ≥ 50 → Warning, otherwise Error.
    /// </summary>
    public static Color GetProgressColor(int progress) => progress switch
    {
        >= 90 => Color.Success,
        >= 50 => Color.Warning,
        _ => Color.Error
    };
}
