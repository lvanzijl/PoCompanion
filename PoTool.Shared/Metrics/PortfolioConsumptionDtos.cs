namespace PoTool.Shared.Metrics;

/// <summary>
/// Read-only lifecycle state exposed to portfolio consumers.
/// </summary>
public enum PortfolioLifecycleState
{
    /// <summary>Currently active portfolio row.</summary>
    Active = 0,

    /// <summary>Historically retained retired work-package row.</summary>
    Retired = 1
}

/// <summary>
/// Supported presentational sort fields for portfolio read models.
/// </summary>
public enum PortfolioReadSortBy
{
    /// <summary>Use the canonical deterministic business-key order.</summary>
    Default = 0,

    /// <summary>Sort by progress.</summary>
    Progress = 1,

    /// <summary>Sort by weight.</summary>
    Weight = 2,

    /// <summary>Sort by delta.</summary>
    Delta = 3
}

/// <summary>
/// Supported presentational sort directions for portfolio read models.
/// </summary>
public enum PortfolioReadSortDirection
{
    /// <summary>Ascending order.</summary>
    Asc = 0,

    /// <summary>Descending order.</summary>
    Desc = 1
}

/// <summary>
/// Supported presentational grouping dimensions for portfolio read models.
/// </summary>
public enum PortfolioReadGroupBy
{
    /// <summary>No grouping hint.</summary>
    None = 0,

    /// <summary>Group by product.</summary>
    Product = 1,

    /// <summary>Group by project number.</summary>
    Project = 2,

    /// <summary>Group by work package.</summary>
    WorkPackage = 3
}

/// <summary>
/// Deterministic trend direction for persisted portfolio history.
/// </summary>
public enum PortfolioTrendDirection
{
    /// <summary>Latest value is lower than the previous value.</summary>
    Decreasing = 0,

    /// <summary>Latest value matches the previous value.</summary>
    Stable = 1,

    /// <summary>Latest value is higher than the previous value.</summary>
    Increasing = 2
}

/// <summary>
/// Read-only decision-signal types derived from persisted portfolio history.
/// </summary>
public enum PortfolioDecisionSignalType
{
    ProgressImproving = 0,
    ProgressDeclining = 1,
    WeightIncreasing = 2,
    WeightDecreasing = 3,
    NewWorkPackage = 4,
    RetiredWorkPackage = 5,
    RepeatedNoChange = 6,
    ArchivedSnapshotExcludedNotice = 7
}

/// <summary>
/// Visual tone for read-only portfolio decision signals.
/// </summary>
public enum PortfolioDecisionSignalTone
{
    Info = 0,
    Positive = 1,
    Warning = 2
}

/// <summary>
/// Explicit filter value selection with deterministic ALL semantics.
/// </summary>
public sealed record FilterSelectionDto<T>
{
    /// <summary>True when the filter represents ALL values.</summary>
    public required bool IsAll { get; init; }

    /// <summary>Explicit selected values when <see cref="IsAll"/> is false.</summary>
    public required IReadOnlyList<T> Values { get; init; }
}

/// <summary>
/// Canonical time selection modes exposed in portfolio filter metadata.
/// </summary>
public enum FilterTimeSelectionModeDto
{
    None = 0,
    CurrentSprint = 1,
    Sprint = 2,
    MultiSprint = 3,
    DateRange = 4
}

/// <summary>
/// Explicit time selection metadata for backend filter resolution.
/// </summary>
public sealed record FilterTimeSelectionDto
{
    /// <summary>Selected time mode.</summary>
    public required FilterTimeSelectionModeDto Mode { get; init; }

    /// <summary>Single selected sprint identifier when applicable.</summary>
    public int? SprintId { get; init; }

    /// <summary>Selected sprint identifiers for multi-sprint mode.</summary>
    public required IReadOnlyList<int> SprintIds { get; init; }

    /// <summary>Optional lower time bound when using date-range mode.</summary>
    public DateTimeOffset? RangeStartUtc { get; init; }

    /// <summary>Optional upper time bound when using date-range mode.</summary>
    public DateTimeOffset? RangeEndUtc { get; init; }
}

/// <summary>
/// Canonical filter metadata echoed by portfolio responses.
/// </summary>
public sealed record FilterContextDto
{
    /// <summary>Explicit product filter selection.</summary>
    public required FilterSelectionDto<int> ProductIds { get; init; }

    /// <summary>Explicit project filter selection.</summary>
    public required FilterSelectionDto<string> ProjectNumbers { get; init; }

    /// <summary>Explicit work-package filter selection.</summary>
    public required FilterSelectionDto<string> WorkPackages { get; init; }

    /// <summary>Explicit lifecycle-state filter selection.</summary>
    public required FilterSelectionDto<PortfolioLifecycleState> LifecycleStates { get; init; }

    /// <summary>Explicit team filter selection.</summary>
    public required FilterSelectionDto<int> TeamIds { get; init; }

    /// <summary>Explicit time filter selection.</summary>
    public required FilterTimeSelectionDto Time { get; init; }
}

/// <summary>
/// Invalid field detail returned with portfolio filter metadata.
/// </summary>
public sealed record FilterValidationIssueDto
{
    /// <summary>Canonical field name.</summary>
    public required string Field { get; init; }

    /// <summary>Human-readable validation message.</summary>
    public required string Message { get; init; }
}

/// <summary>
/// Requested/effective filter metadata returned with portfolio responses.
/// </summary>
public sealed record FilterResponseMetadataDto
{
    /// <summary>The exact filter requested by the caller.</summary>
    public required FilterContextDto RequestedFilter { get; init; }

    /// <summary>The deterministic effective filter used by the backend.</summary>
    public required FilterContextDto EffectiveFilter { get; init; }

    /// <summary>Invalid filter fields that were replaced during resolution.</summary>
    public required IReadOnlyList<string> InvalidFields { get; init; }

    /// <summary>Optional validation details describing invalid fields.</summary>
    public required IReadOnlyList<FilterValidationIssueDto> Messages { get; init; }
}

/// <summary>
/// Read-only filter and presentational options for portfolio queries.
/// Filtering is applied after domain computation.
/// </summary>
public sealed record PortfolioReadQueryOptions(
    int? ProductId = null,
    string? ProjectNumber = null,
    string? WorkPackage = null,
    PortfolioLifecycleState? LifecycleState = null,
    PortfolioReadSortBy SortBy = PortfolioReadSortBy.Default,
    PortfolioReadSortDirection SortDirection = PortfolioReadSortDirection.Desc,
    PortfolioReadGroupBy GroupBy = PortfolioReadGroupBy.None,
    int SnapshotCount = 6,
    DateTimeOffset? RangeStartUtc = null,
    DateTimeOffset? RangeEndUtc = null,
    bool IncludeArchivedSnapshots = false,
    long? CompareToSnapshotId = null);

/// <summary>
/// Read-only portfolio progress response projected from the latest available portfolio snapshot.
/// </summary>
public sealed record PortfolioProgressDto
{
    /// <summary>Latest available snapshot label.</summary>
    public required string SnapshotLabel { get; init; }

    /// <summary>Latest available snapshot timestamp.</summary>
    public required DateTimeOffset SnapshotTimestamp { get; init; }

    /// <summary>Overall portfolio progress percentage for the unfiltered portfolio snapshot.</summary>
    public double? PortfolioProgress { get; init; }

    /// <summary>Total weight for the unfiltered portfolio snapshot.</summary>
    public required double TotalWeight { get; init; }

    /// <summary>Total item count before output filtering.</summary>
    public required int TotalItemCount { get; init; }

    /// <summary>Item count after output filtering.</summary>
    public required int FilteredItemCount { get; init; }

    /// <summary>Applied grouping hint.</summary>
    public required PortfolioReadGroupBy GroupBy { get; init; }

    /// <summary>Applied sort field.</summary>
    public required PortfolioReadSortBy SortBy { get; init; }

    /// <summary>Applied sort direction.</summary>
    public required PortfolioReadSortDirection SortDirection { get; init; }

    /// <summary>Filtered current snapshot rows for read-only display.</summary>
    public required IReadOnlyList<PortfolioSnapshotItemDto> Items { get; init; }

    /// <summary>Whether the response contains snapshot data.</summary>
    public required bool HasData { get; init; }

    /// <summary>Requested/effective filter metadata used to build the response.</summary>
    public required FilterResponseMetadataDto Filter { get; init; }
}

/// <summary>
/// Read-only portfolio snapshot projection.
/// </summary>
public sealed record PortfolioSnapshotDto
{
    /// <summary>Snapshot label.</summary>
    public required string SnapshotLabel { get; init; }

    /// <summary>Snapshot timestamp.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Total item count before output filtering.</summary>
    public required int TotalItemCount { get; init; }

    /// <summary>Item count after output filtering.</summary>
    public required int FilteredItemCount { get; init; }

    /// <summary>Applied grouping hint.</summary>
    public required PortfolioReadGroupBy GroupBy { get; init; }

    /// <summary>Applied sort field.</summary>
    public required PortfolioReadSortBy SortBy { get; init; }

    /// <summary>Applied sort direction.</summary>
    public required PortfolioReadSortDirection SortDirection { get; init; }

    /// <summary>Filtered snapshot rows.</summary>
    public required IReadOnlyList<PortfolioSnapshotItemDto> Items { get; init; }

    /// <summary>Whether the response contains snapshot data.</summary>
    public required bool HasData { get; init; }

    /// <summary>Requested/effective filter metadata used to build the response.</summary>
    public required FilterResponseMetadataDto Filter { get; init; }
}

/// <summary>
/// Read-only portfolio snapshot item projection.
/// </summary>
public sealed record PortfolioSnapshotItemDto
{
    /// <summary>Product identifier.</summary>
    public required int ProductId { get; init; }

    /// <summary>Product display name.</summary>
    public required string ProductName { get; init; }

    /// <summary>Project number business key.</summary>
    public required string ProjectNumber { get; init; }

    /// <summary>Optional work-package business key.</summary>
    public string? WorkPackage { get; init; }

    /// <summary>Lifecycle state for the snapshot row.</summary>
    public required PortfolioLifecycleState LifecycleState { get; init; }

    /// <summary>Snapshot progress ratio in the canonical unit interval [0, 1].</summary>
    public required double Progress { get; init; }

    /// <summary>Snapshot total weight.</summary>
    public required double Weight { get; init; }
}

/// <summary>
/// Read-only comparison of the latest two available portfolio snapshots.
/// </summary>
public sealed record PortfolioComparisonDto
{
    /// <summary>Previous snapshot label when available.</summary>
    public string? PreviousSnapshotLabel { get; init; }

    /// <summary>Current snapshot label.</summary>
    public required string CurrentSnapshotLabel { get; init; }

    /// <summary>Previous snapshot timestamp when available.</summary>
    public DateTimeOffset? PreviousTimestamp { get; init; }

    /// <summary>Current snapshot timestamp.</summary>
    public required DateTimeOffset CurrentTimestamp { get; init; }

    /// <summary>Total item count before output filtering.</summary>
    public required int TotalItemCount { get; init; }

    /// <summary>Item count after output filtering.</summary>
    public required int FilteredItemCount { get; init; }

    /// <summary>Applied grouping hint.</summary>
    public required PortfolioReadGroupBy GroupBy { get; init; }

    /// <summary>Applied sort field.</summary>
    public required PortfolioReadSortBy SortBy { get; init; }

    /// <summary>Applied sort direction.</summary>
    public required PortfolioReadSortDirection SortDirection { get; init; }

    /// <summary>Filtered comparison rows.</summary>
    public required IReadOnlyList<PortfolioComparisonItemDto> Items { get; init; }

    /// <summary>Whether the response contains comparison data.</summary>
    public required bool HasData { get; init; }

    /// <summary>Requested/effective filter metadata used to build the response.</summary>
    public required FilterResponseMetadataDto Filter { get; init; }
}

/// <summary>
/// Read-only comparison row projected from two portfolio snapshots.
/// </summary>
public sealed record PortfolioComparisonItemDto
{
    /// <summary>Product identifier.</summary>
    public required int ProductId { get; init; }

    /// <summary>Product display name.</summary>
    public required string ProductName { get; init; }

    /// <summary>Project number business key.</summary>
    public required string ProjectNumber { get; init; }

    /// <summary>Optional work-package business key.</summary>
    public string? WorkPackage { get; init; }

    /// <summary>Previous lifecycle state when available.</summary>
    public PortfolioLifecycleState? PreviousLifecycleState { get; init; }

    /// <summary>Current lifecycle state when available.</summary>
    public PortfolioLifecycleState? CurrentLifecycleState { get; init; }

    /// <summary>Previous progress ratio in the canonical unit interval [0, 1].</summary>
    public double? PreviousProgress { get; init; }

    /// <summary>Current progress ratio in the canonical unit interval [0, 1].</summary>
    public double? CurrentProgress { get; init; }

    /// <summary>Progress delta in the canonical unit interval [0, 1].</summary>
    public double? ProgressDelta { get; init; }

    /// <summary>Previous weight when available.</summary>
    public double? PreviousWeight { get; init; }

    /// <summary>Current weight when available.</summary>
    public double? CurrentWeight { get; init; }

    /// <summary>Weight delta when available.</summary>
    public double? WeightDelta { get; init; }
}

/// <summary>
/// Read-only historical snapshot option used for trend history and explicit comparison selection.
/// </summary>
public sealed record PortfolioHistoricalSnapshotDto
{
    /// <summary>Persisted snapshot identifier used for deterministic selection.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>Snapshot label/source.</summary>
    public required string SnapshotLabel { get; init; }

    /// <summary>Snapshot timestamp.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Whether the selected snapshot group contains archived persisted snapshots.</summary>
    public required bool IncludesArchivedSnapshot { get; init; }
}

/// <summary>
/// Point in a persisted portfolio trend series.
/// </summary>
public sealed record PortfolioTrendPointDto
{
    /// <summary>Persisted snapshot identifier.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>Snapshot label/source.</summary>
    public required string SnapshotLabel { get; init; }

    /// <summary>Snapshot timestamp.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Projected value at the selected snapshot, or null when no persisted active baseline exists.</summary>
    public double? Value { get; init; }

    /// <summary>Whether the selected point includes archived persisted snapshots.</summary>
    public required bool IncludesArchivedSnapshot { get; init; }
}

/// <summary>
/// Read-only trend metric projected from persisted snapshot history.
/// </summary>
public sealed record PortfolioMetricTrendDto
{
    /// <summary>Latest value in the selected series.</summary>
    public double? CurrentValue { get; init; }

    /// <summary>Previous value in the selected series.</summary>
    public double? PreviousValue { get; init; }

    /// <summary>Latest minus previous value when both exist.</summary>
    public double? Delta { get; init; }

    /// <summary>Deterministic direction derived from the latest two values when both exist.</summary>
    public PortfolioTrendDirection? Direction { get; init; }

    /// <summary>Persisted historical points that back this series.</summary>
    public required IReadOnlyList<PortfolioTrendPointDto> Points { get; init; }
}

/// <summary>
/// Read-only project/work-package trend summary projected from persisted snapshot history.
/// </summary>
public sealed record PortfolioScopedTrendDto
{
    /// <summary>Product identifier.</summary>
    public required int ProductId { get; init; }

    /// <summary>Product display name.</summary>
    public required string ProductName { get; init; }

    /// <summary>Project number business key.</summary>
    public required string ProjectNumber { get; init; }

    /// <summary>Optional work-package business key.</summary>
    public string? WorkPackage { get; init; }

    /// <summary>Latest lifecycle state in the selected history when available.</summary>
    public PortfolioLifecycleState? CurrentLifecycleState { get; init; }

    /// <summary>Previous lifecycle state in the selected history when available.</summary>
    public PortfolioLifecycleState? PreviousLifecycleState { get; init; }

    /// <summary>Progress trend summary.</summary>
    public required PortfolioMetricTrendDto ProgressTrend { get; init; }

    /// <summary>Weight trend summary.</summary>
    public required PortfolioMetricTrendDto WeightTrend { get; init; }
}

/// <summary>
/// Read-only portfolio trend response based only on persisted snapshot history.
/// </summary>
public sealed record PortfolioTrendDto
{
    /// <summary>Selected historical snapshots in deterministic latest-first order.</summary>
    public required IReadOnlyList<PortfolioHistoricalSnapshotDto> Snapshots { get; init; }

    /// <summary>Overall portfolio progress trend.</summary>
    public required PortfolioMetricTrendDto PortfolioProgressTrend { get; init; }

    /// <summary>Overall portfolio total-weight trend.</summary>
    public required PortfolioMetricTrendDto TotalWeightTrend { get; init; }

    /// <summary>Per-project trend summaries.</summary>
    public required IReadOnlyList<PortfolioScopedTrendDto> Projects { get; init; }

    /// <summary>Per-work-package trend summaries.</summary>
    public required IReadOnlyList<PortfolioScopedTrendDto> WorkPackages { get; init; }

    /// <summary>Applied snapshot-count bound.</summary>
    public required int SnapshotCount { get; init; }

    /// <summary>Optional lower time bound for history selection.</summary>
    public DateTimeOffset? RangeStartUtc { get; init; }

    /// <summary>Optional upper time bound for history selection.</summary>
    public DateTimeOffset? RangeEndUtc { get; init; }

    /// <summary>Whether archived persisted snapshots were explicitly included.</summary>
    public required bool IncludesArchivedSnapshots { get; init; }

    /// <summary>Whether archived snapshots remain excluded by default.</summary>
    public required bool ArchivedSnapshotsExcludedByDefault { get; init; }

    /// <summary>Whether excluded archived snapshots exist for the selected history window.</summary>
    public required bool ArchivedSnapshotsExcludedNotice { get; init; }

    /// <summary>Whether the response contains historical data.</summary>
    public required bool HasData { get; init; }

    /// <summary>Requested/effective filter metadata used to build the response.</summary>
    public required FilterResponseMetadataDto Filter { get; init; }
}

/// <summary>
/// Read-only decision-support signal projected from persisted portfolio history.
/// </summary>
public sealed record PortfolioDecisionSignalDto
{
    /// <summary>Signal type.</summary>
    public required PortfolioDecisionSignalType Type { get; init; }

    /// <summary>UI tone hint for this signal.</summary>
    public required PortfolioDecisionSignalTone Tone { get; init; }

    /// <summary>Short signal title.</summary>
    public required string Title { get; init; }

    /// <summary>Signal description.</summary>
    public required string Description { get; init; }

    /// <summary>Optional product identifier.</summary>
    public int? ProductId { get; init; }

    /// <summary>Optional product name.</summary>
    public string? ProductName { get; init; }

    /// <summary>Optional project number.</summary>
    public string? ProjectNumber { get; init; }

    /// <summary>Optional work-package key.</summary>
    public string? WorkPackage { get; init; }

    /// <summary>Optional lifecycle state for the signal scope.</summary>
    public PortfolioLifecycleState? LifecycleState { get; init; }

    /// <summary>Optional snapshot identifier associated with the signal.</summary>
    public long? SnapshotId { get; init; }

    /// <summary>Optional snapshot label associated with the signal.</summary>
    public string? SnapshotLabel { get; init; }

    /// <summary>Optional snapshot timestamp associated with the signal.</summary>
    public DateTimeOffset? SnapshotTimestamp { get; init; }
}

/// <summary>
/// Portfolio decision-signal response envelope with canonical filter metadata.
/// </summary>
public sealed record PortfolioSignalsDto
{
    /// <summary>Signals produced for the effective filter.</summary>
    public required IReadOnlyList<PortfolioDecisionSignalDto> Signals { get; init; }

    /// <summary>Requested/effective filter metadata used to build the response.</summary>
    public required FilterResponseMetadataDto Filter { get; init; }
}
