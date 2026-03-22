namespace PoTool.Shared.BuildQuality;

/// <summary>
/// Canonical BuildQuality unknown reasons transported to clients.
/// </summary>
public static class BuildQualityUnknownReasons
{
    public const string NoEligibleBuilds = "NoEligibleBuilds";
    public const string NoTestRuns = "NoTestRuns";
    public const string NoCoverage = "NoCoverage";
    public const string ZeroTotalLines = "ZeroTotalLines";
}
