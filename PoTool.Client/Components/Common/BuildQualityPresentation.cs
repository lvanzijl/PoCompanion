using System.Globalization;
using MudBlazor;
using PoTool.Client.ApiClient;
using SharedBuildQuality = PoTool.Shared.BuildQuality;

namespace PoTool.Client.Components.Common;

public enum BuildQualityVisualState
{
    Unknown,
    Good,
    Warning,
    Bad
}

public enum BuildQualityDimension
{
    Builds,
    Tests,
    Coverage
}

public sealed record BuildQualityStateDescriptor(
    BuildQualityVisualState State,
    Color Color,
    string Icon,
    string Label,
    string StrokeColor);

public static class BuildQualityPresentation
{
    public const int MinimumBuilds = 3;
    public const int MinimumTests = 20;

    private const double GoodThreshold = 0.90d;
    private const double WarningThreshold = 0.70d;

    public static string FormatPercent(double? value, bool isUnknown)
    {
        return isUnknown || !value.HasValue
            ? "Unknown"
            : value.Value.ToString("P0", CultureInfo.InvariantCulture);
    }

    public static BuildQualityStateDescriptor GetDimensionState(
        PoTool.Client.ApiClient.BuildQualityResultDto result,
        BuildQualityDimension dimension)
    {
        ArgumentNullException.ThrowIfNull(result);

        return dimension switch
        {
            BuildQualityDimension.Builds => GetRateState(
                result.Metrics.SuccessRate,
                result.Evidence.SuccessRateUnknown),
            BuildQualityDimension.Tests => GetTestState(result),
            BuildQualityDimension.Coverage => GetRateState(
                result.Metrics.Coverage,
                result.Evidence.CoverageUnknown),
            _ => UnknownState()
        };
    }

    public static BuildQualityStateDescriptor GetOverallState(PoTool.Client.ApiClient.BuildQualityResultDto result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var buildState = GetDimensionState(result, BuildQualityDimension.Builds);
        var testState = GetDimensionState(result, BuildQualityDimension.Tests);
        var coverageState = GetDimensionState(result, BuildQualityDimension.Coverage);

        if (buildState.State == BuildQualityVisualState.Unknown
            || testState.State == BuildQualityVisualState.Unknown
            || coverageState.State == BuildQualityVisualState.Unknown)
        {
            return UnknownState();
        }

        if (buildState.State == BuildQualityVisualState.Bad
            || testState.State == BuildQualityVisualState.Bad
            || coverageState.State == BuildQualityVisualState.Bad)
        {
            return BadState();
        }

        if (result.Metrics.Confidence < 2
            || buildState.State == BuildQualityVisualState.Warning
            || testState.State == BuildQualityVisualState.Warning
            || coverageState.State == BuildQualityVisualState.Warning)
        {
            return WarningState();
        }

        return GoodState();
    }

    public static BuildQualityStateDescriptor GetConfidenceState(PoTool.Client.ApiClient.BuildQualityResultDto result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Metrics.Confidence switch
        {
            >= 2 => new BuildQualityStateDescriptor(
                BuildQualityVisualState.Good,
                Color.Success,
                Icons.Material.Filled.Verified,
                "High confidence",
                "var(--mud-palette-success)"),
            1 => new BuildQualityStateDescriptor(
                BuildQualityVisualState.Warning,
                Color.Warning,
                Icons.Material.Filled.ReportProblem,
                "Low confidence",
                "var(--mud-palette-warning)"),
            _ => new BuildQualityStateDescriptor(
                BuildQualityVisualState.Unknown,
                Color.Default,
                Icons.Material.Filled.HelpOutline,
                "Insufficient confidence",
                "var(--mud-palette-text-disabled)")
        };
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

    public static string GetConfidenceSummary(PoTool.Client.ApiClient.BuildQualityResultDto result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var buildStatus = result.Evidence.BuildThresholdMet ? "met" : "not met";
        var testStatus = result.Evidence.TestThresholdMet ? "met" : "not met";

        return $"Minimum builds ({MinimumBuilds}): {buildStatus}. Minimum tests ({MinimumTests}): {testStatus}.";
    }

    private static BuildQualityStateDescriptor GetTestState(PoTool.Client.ApiClient.BuildQualityResultDto result)
    {
        if (result.Evidence.TestPassRateUnknown)
        {
            return UnknownState();
        }

        if (!result.Evidence.TestThresholdMet)
        {
            return WarningState();
        }

        return GetRateState(result.Metrics.TestPassRate, false);
    }

    private static BuildQualityStateDescriptor GetRateState(double? value, bool isUnknown)
    {
        if (isUnknown || !value.HasValue)
        {
            return UnknownState();
        }

        if (value.Value >= GoodThreshold)
        {
            return GoodState();
        }

        if (value.Value >= WarningThreshold)
        {
            return WarningState();
        }

        return BadState();
    }

    private static BuildQualityStateDescriptor GoodState() =>
        new(
            BuildQualityVisualState.Good,
            Color.Success,
            Icons.Material.Filled.CheckCircle,
            "Healthy",
            "var(--mud-palette-success)");

    private static BuildQualityStateDescriptor WarningState() =>
        new(
            BuildQualityVisualState.Warning,
            Color.Warning,
            Icons.Material.Filled.Warning,
            "Needs attention",
            "var(--mud-palette-warning)");

    private static BuildQualityStateDescriptor BadState() =>
        new(
            BuildQualityVisualState.Bad,
            Color.Error,
            Icons.Material.Filled.Error,
            "Unhealthy",
            "var(--mud-palette-error)");

    private static BuildQualityStateDescriptor UnknownState() =>
        new(
            BuildQualityVisualState.Unknown,
            Color.Default,
            Icons.Material.Filled.HelpOutline,
            "Unknown",
            "var(--mud-palette-text-disabled)");
}
