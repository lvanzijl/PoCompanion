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
