namespace PoTool.Client.Models;

public enum GlobalFilterTimeMode
{
    Snapshot,
    Sprint,
    Trend,
    Rolling
}

public sealed record GlobalFilterState(
    IReadOnlyList<int> ProductIds,
    IReadOnlyList<string> ProjectAliases,
    int? TeamId,
    GlobalFilterTimeMode TimeMode,
    string? TimeValue)
{
    public static GlobalFilterState Neutral { get; } = new(Array.Empty<int>(), Array.Empty<string>(), null, GlobalFilterTimeMode.Snapshot, null);

    public bool AllProducts => ProductIds.Count == 0;

    public bool AllProjects => ProjectAliases.Count == 0;
}

public sealed record GlobalFilterPageDefinition(
    string PageName,
    bool UsesProduct,
    bool UsesProject,
    bool UsesTeam,
    bool UsesTime,
    GlobalFilterTimeMode TimeMode,
    bool RequiresTeam = false,
    bool RequiresSprint = false);

public sealed record GlobalFilterUsageReport(
    string PageName,
    string Route,
    bool UsesProduct,
    bool UsesProject,
    bool UsesTeam,
    bool UsesTime,
    IReadOnlyList<int> ProductIds,
    IReadOnlyList<string> ProjectAliases,
    int? TeamId,
    GlobalFilterTimeMode TimeMode,
    string? TimeValue,
    bool MissingTeam,
    bool MissingSprint,
    int? ActiveProfileId,
    DateTimeOffset RecordedAtUtc);
