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
    PortfolioReadGroupBy GroupBy = PortfolioReadGroupBy.None);

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
