namespace PoTool.Shared.Metrics;

/// <summary>
/// DTO for Sprint Trend metrics showing planned vs worked items.
/// </summary>
public record SprintTrendMetricsDto
{
    /// <summary>
    /// Sprint ID.
    /// </summary>
    public required int SprintId { get; init; }

    /// <summary>
    /// Sprint name.
    /// </summary>
    public required string SprintName { get; init; }

    /// <summary>
    /// Sprint start date.
    /// </summary>
    public DateTimeOffset? StartUtc { get; init; }

    /// <summary>
    /// Sprint end date.
    /// </summary>
    public DateTimeOffset? EndUtc { get; init; }

    /// <summary>
    /// Metrics broken down by product.
    /// </summary>
    public required IReadOnlyList<ProductSprintMetricsDto> ProductMetrics { get; init; }

    /// <summary>
    /// Total planned items (excluding bugs).
    /// </summary>
    public int TotalPlannedCount { get; init; }

    /// <summary>
    /// Total planned effort (excluding bugs).
    /// </summary>
    public int TotalPlannedEffort { get; init; }

    /// <summary>
    /// Total worked items (excluding bugs).
    /// </summary>
    public int TotalWorkedCount { get; init; }

    /// <summary>
    /// Total worked effort (excluding bugs).
    /// </summary>
    public int TotalWorkedEffort { get; init; }

    /// <summary>
    /// Total bugs planned.
    /// </summary>
    public int TotalBugsPlannedCount { get; init; }

    /// <summary>
    /// Total bugs worked.
    /// </summary>
    public int TotalBugsWorkedCount { get; init; }
}

/// <summary>
/// DTO for product-specific sprint metrics.
/// </summary>
public record ProductSprintMetricsDto
{
    /// <summary>
    /// Product ID.
    /// </summary>
    public required int ProductId { get; init; }

    /// <summary>
    /// Product name.
    /// </summary>
    public required string ProductName { get; init; }

    /// <summary>
    /// Number of items planned for this sprint-product.
    /// </summary>
    public int PlannedCount { get; init; }

    /// <summary>
    /// Total effort of planned items.
    /// </summary>
    public int PlannedEffort { get; init; }

    /// <summary>
    /// Number of items with activity in this sprint-product.
    /// </summary>
    public int WorkedCount { get; init; }

    /// <summary>
    /// Total effort of worked items.
    /// </summary>
    public int WorkedEffort { get; init; }

    /// <summary>
    /// Number of bugs planned.
    /// </summary>
    public int BugsPlannedCount { get; init; }

    /// <summary>
    /// Number of bugs with activity.
    /// </summary>
    public int BugsWorkedCount { get; init; }
}

/// <summary>
/// Request to get sprint trend metrics.
/// </summary>
public record GetSprintTrendMetricsRequest
{
    /// <summary>
    /// Product Owner ID.
    /// </summary>
    public required int ProductOwnerId { get; init; }

    /// <summary>
    /// Sprint IDs to get metrics for.
    /// </summary>
    public required IReadOnlyList<int> SprintIds { get; init; }

    /// <summary>
    /// Whether to recompute metrics (true) or use cached projections (false).
    /// </summary>
    public bool Recompute { get; init; }
}

/// <summary>
/// Response containing sprint trend metrics.
/// </summary>
public record GetSprintTrendMetricsResponse
{
    /// <summary>
    /// Whether the request was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if not successful.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Sprint metrics by sprint.
    /// </summary>
    public IReadOnlyList<SprintTrendMetricsDto>? Metrics { get; init; }
}

/// <summary>
/// DTO for feature progress information.
/// </summary>
public record FeatureProgressDto
{
    /// <summary>
    /// Feature TFS ID.
    /// </summary>
    public required int FeatureId { get; init; }

    /// <summary>
    /// Feature title.
    /// </summary>
    public required string FeatureTitle { get; init; }

    /// <summary>
    /// Epic TFS ID (parent of feature).
    /// </summary>
    public int? EpicId { get; init; }

    /// <summary>
    /// Epic title.
    /// </summary>
    public string? EpicTitle { get; init; }

    /// <summary>
    /// Product ID.
    /// </summary>
    public required int ProductId { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// Feature.State == Done => 100%, otherwise min(raw, 90%).
    /// </summary>
    public int ProgressPercent { get; init; }

    /// <summary>
    /// Total effort of all PBIs under this feature.
    /// </summary>
    public int TotalEffort { get; init; }

    /// <summary>
    /// Effort of done PBIs under this feature.
    /// </summary>
    public int DoneEffort { get; init; }

    /// <summary>
    /// Whether the feature is in Done state.
    /// </summary>
    public bool IsDone { get; init; }
}
