using System.Globalization;
using SharedBuildQuality = PoTool.Shared.BuildQuality;

namespace PoTool.Client.Components.Common;

public enum BuildQualityDimension
{
    Builds,
    Tests,
    Coverage
}

public static class BuildQualityPresentation
{
    /// <summary>
    /// Formats a backend-provided percentage metric for display.
    /// Returns <c>Unknown</c> only when the explicit DTO unknown flag is set, returns an em dash when no value is present without an unknown flag, and otherwise formats the percentage value.
    /// </summary>
    /// <param name="value">The backend-provided percentage value.</param>
    /// <param name="isUnknown">The explicit backend unknown flag for the metric.</param>
    public static string FormatPercent(double? value, bool isUnknown)
    {
        if (isUnknown)
        {
            return "Unknown";
        }

        return value.HasValue
            ? value.Value.ToString("P0", CultureInfo.InvariantCulture)
            : "—";
    }

    /// <summary>
    /// Maps a backend-provided BuildQuality unknown reason code to user-facing text.
    /// Returns a generic fallback message when the reason is null, empty, or unrecognized.
    /// </summary>
    /// <param name="reason">The backend-provided unknown reason code.</param>
    public static string GetUnknownReasonText(string? reason)
    {
        return reason switch
        {
            SharedBuildQuality.BuildQualityUnknownReasons.NoEligibleBuilds => "No eligible builds are available in scope.",
            SharedBuildQuality.BuildQualityUnknownReasons.NoTestRuns => "No test runs are available in scope.",
            SharedBuildQuality.BuildQualityUnknownReasons.NoCoverage => "No coverage data is available in scope.",
            SharedBuildQuality.BuildQualityUnknownReasons.ZeroTotalLines => "Coverage total lines are zero, so coverage remains Unknown.",
            _ => "No evidence is available for this metric."
        };
    }
}
