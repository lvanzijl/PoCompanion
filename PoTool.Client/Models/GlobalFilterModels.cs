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

public enum FilterResolutionStatus
{
    Resolved,
    ResolvedWithNormalization,
    Unresolved,
    Invalid
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

    public bool IsResolved
        => Mode switch
        {
            FilterTimeMode.Snapshot => true,
            FilterTimeMode.Sprint => SprintId.HasValue,
            FilterTimeMode.Range => StartSprintId.HasValue && EndSprintId.HasValue,
            FilterTimeMode.Rolling => RollingWindow.HasValue && RollingWindow.Value > 0 && RollingUnit.HasValue,
            _ => false
        };

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

    public int? PrimaryProductId => ProductIds.Count > 0 ? ProductIds[0] : null;

    public string? PrimaryProjectId => ProjectIds.Count > 0 ? ProjectIds[0] : null;
}

public sealed record GlobalFilterPageDefinition(
    string PageName,
    bool UsesProduct,
    bool UsesProject,
    bool UsesTeam,
    bool UsesTime,
    FilterTimeMode TimeMode,
    bool RequiresTeam = false,
    bool RequiresSprint = false,
    IReadOnlyList<FilterTimeMode>? SupportedTimeModes = null,
    FilterTimeMode? DefaultTimeMode = null)
{
    public IReadOnlyList<FilterTimeMode> AllowedTimeModes => SupportedTimeModes ?? [TimeMode];

    public FilterTimeMode EffectiveDefaultTimeMode => DefaultTimeMode ?? TimeMode;
}

public sealed record FilterLocalBridgeState(
    int? ProductId = null,
    string? ProjectAlias = null,
    string? ProjectId = null,
    int? TeamId = null,
    int? SprintId = null,
    int? FromSprintId = null,
    int? ToSprintId = null,
    int? RollingWindow = null,
    FilterTimeUnit? RollingUnit = null)
{
    public static FilterLocalBridgeState FromState(FilterState state, string? projectAlias = null)
        => new(
            ProductId: state.PrimaryProductId,
            ProjectAlias: projectAlias,
            ProjectId: state.PrimaryProjectId,
            TeamId: state.TeamId,
            SprintId: state.Time.SprintId,
            FromSprintId: state.Time.StartSprintId,
            ToSprintId: state.Time.EndSprintId,
            RollingWindow: state.Time.RollingWindow,
            RollingUnit: state.Time.RollingUnit);
}

public sealed record FilterStateResolution(
    string PageName,
    string Route,
    string RouteSignature,
    bool UsesProduct,
    bool UsesProject,
    bool UsesTeam,
    bool UsesTime,
    FilterState State,
    FilterResolutionStatus Status,
    bool MissingTeam,
    bool MissingSprint,
    int? ActiveProfileId,
    FilterUpdateSource LastUpdateSource,
    IReadOnlyList<string> NormalizationDecisions,
    IReadOnlyList<string> StateIssues,
    DateTimeOffset RecordedAtUtc);
