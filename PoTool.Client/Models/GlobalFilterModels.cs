namespace PoTool.Client.Models;

public enum FilterTimeMode
{
    Snapshot,
    Sprint,
    Range,
    Rolling
}

public enum FilterTimeUnit
{
    Sprint,
    Days
}

public enum FilterUpdateSource
{
    Default,
    Route,
    Query,
    LocalBridge,
    Ui
}

public sealed record FilterTimeSelection(
    FilterTimeMode Mode,
    int? SprintId = null,
    int? StartSprintId = null,
    int? EndSprintId = null,
    int? RollingWindow = null,
    FilterTimeUnit? RollingUnit = null)
{
    public static FilterTimeSelection Snapshot { get; } = new(FilterTimeMode.Snapshot);

    public string ToDisplayString()
        => Mode switch
        {
            FilterTimeMode.Snapshot => "Snapshot",
            FilterTimeMode.Sprint => SprintId.HasValue ? $"Sprint {SprintId.Value}" : "Sprint",
            FilterTimeMode.Range => $"{FormatBoundary("from", StartSprintId)} → {FormatBoundary("to", EndSprintId)}",
            FilterTimeMode.Rolling when RollingWindow.HasValue && RollingUnit.HasValue => $"Rolling {RollingWindow.Value} {RollingUnit.Value}",
            FilterTimeMode.Rolling => "Rolling",
            _ => Mode.ToString()
        };

    private static string FormatBoundary(string label, int? sprintId)
        => sprintId.HasValue ? $"{label} {sprintId.Value}" : $"{label} ?";
}

public sealed record FilterState(
    IReadOnlyList<int> ProductIds,
    IReadOnlyList<string> ProjectIds,
    int? TeamId,
    FilterTimeSelection Time)
{
    public static FilterState Neutral { get; } = new(Array.Empty<int>(), Array.Empty<string>(), null, FilterTimeSelection.Snapshot);

    public bool AllProducts => ProductIds.Count == 0;

    public bool AllProjects => ProjectIds.Count == 0;
}

public sealed record GlobalFilterPageDefinition(
    string PageName,
    bool UsesProduct,
    bool UsesProject,
    bool UsesTeam,
    bool UsesTime,
    FilterTimeMode TimeMode,
    bool RequiresTeam = false,
    bool RequiresSprint = false);

public sealed record FilterLocalBridgeState(
    int? ProductId = null,
    string? ProjectAlias = null,
    string? ProjectId = null,
    int? TeamId = null,
    int? SprintId = null,
    int? FromSprintId = null,
    int? ToSprintId = null,
    int? RollingWindow = null,
    FilterTimeUnit? RollingUnit = null);

public sealed record FilterStateResolution(
    string PageName,
    string Route,
    bool UsesProduct,
    bool UsesProject,
    bool UsesTeam,
    bool UsesTime,
    FilterState State,
    bool MissingTeam,
    bool MissingSprint,
    int? ActiveProfileId,
    FilterUpdateSource LastUpdateSource,
    IReadOnlyList<string> NormalizationDecisions,
    DateTimeOffset RecordedAtUtc);
