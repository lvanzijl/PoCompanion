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

    /// <summary>
    /// Total PBIs completed (transitioned to Done) in this sprint.
    /// </summary>
    public int TotalCompletedPbiCount { get; init; }

    /// <summary>
    /// Total effort of completed PBIs in this sprint.
    /// </summary>
    public int TotalCompletedPbiEffort { get; init; }

    /// <summary>
    /// Aggregate progression delta across products.
    /// </summary>
    public double TotalProgressionDelta { get; init; }

    /// <summary>
    /// Total new bugs created during this sprint.
    /// </summary>
    public int TotalBugsCreatedCount { get; init; }

    /// <summary>
    /// Total bugs closed during this sprint.
    /// </summary>
    public int TotalBugsClosedCount { get; init; }

    /// <summary>
    /// Total PBIs with missing effort.
    /// </summary>
    public int TotalMissingEffortCount { get; init; }

    /// <summary>
    /// Whether any metric in this sprint used approximation.
    /// </summary>
    public bool IsApproximate { get; init; }
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

    /// <summary>
    /// PBIs completed (transitioned to Done) in this sprint for this product.
    /// </summary>
    public int CompletedPbiCount { get; init; }

    /// <summary>
    /// Effort of completed PBIs in this sprint for this product.
    /// </summary>
    public int CompletedPbiEffort { get; init; }

    /// <summary>
    /// Progression delta for this product in this sprint.
    /// </summary>
    public double ProgressionDelta { get; init; }

    /// <summary>
    /// New bugs created during this sprint for this product.
    /// </summary>
    public int BugsCreatedCount { get; init; }

    /// <summary>
    /// Bugs closed during this sprint for this product.
    /// </summary>
    public int BugsClosedCount { get; init; }

    /// <summary>
    /// PBIs with missing effort for this product.
    /// </summary>
    public int MissingEffortCount { get; init; }

    /// <summary>
    /// Whether approximation was used for this product.
    /// </summary>
    public bool IsApproximate { get; init; }
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

    /// <summary>
    /// Feature-level progress derived from the resolved work item hierarchy.
    /// Populated for the current (or most recent) sprint in the request.
    /// </summary>
    public IReadOnlyList<FeatureProgressDto>? FeatureProgress { get; init; }

    /// <summary>
    /// Epic-level progress derived from child Feature progress.
    /// </summary>
    public IReadOnlyList<EpicProgressDto>? EpicProgress { get; init; }

    /// <summary>
    /// Whether the cached projections may be stale.
    /// True when activity events have been ingested after the last projection computation.
    /// </summary>
    public bool IsStale { get; init; }

    /// <summary>
    /// When projections were last computed (null if never).
    /// </summary>
    public DateTimeOffset? ProjectionsAsOfUtc { get; init; }
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

    /// <summary>
    /// Effort of PBIs that closed (transitioned to Done) during the selected sprint.
    /// Zero when not in single-sprint view.
    /// </summary>
    public int SprintCompletedEffort { get; init; }

    /// <summary>
    /// Progression delta gained during the selected sprint for this feature (0-100).
    /// Computed as SprintCompletedEffort / TotalEffort * 100. Zero when not in single-sprint view.
    /// </summary>
    public double SprintProgressionDelta { get; init; }
}

/// <summary>
/// DTO for epic-level progress information.
/// Epic progress is derived from effort-weighted child Feature completion.
/// </summary>
public record EpicProgressDto
{
    /// <summary>
    /// Epic TFS ID.
    /// </summary>
    public required int EpicId { get; init; }

    /// <summary>
    /// Epic title.
    /// </summary>
    public required string EpicTitle { get; init; }

    /// <summary>
    /// Product ID.
    /// </summary>
    public required int ProductId { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// Derived from effort-weighted child Feature completion.
    /// Epic.State == Done => 100%, otherwise min(raw, 90%).
    /// </summary>
    public int ProgressPercent { get; init; }

    /// <summary>
    /// Total effort of all PBIs under this epic's features.
    /// </summary>
    public int TotalEffort { get; init; }

    /// <summary>
    /// Effort of done PBIs under this epic's features.
    /// </summary>
    public int DoneEffort { get; init; }

    /// <summary>
    /// Number of child features under this epic.
    /// </summary>
    public int FeatureCount { get; init; }

    /// <summary>
    /// Number of child features that are done.
    /// </summary>
    public int DoneFeatureCount { get; init; }

    /// <summary>
    /// Whether the epic is in Done state.
    /// </summary>
    public bool IsDone { get; init; }

    /// <summary>
    /// Effort of PBIs that closed (transitioned to Done) during the selected sprint across this epic's features.
    /// Zero when not in single-sprint view.
    /// </summary>
    public int SprintCompletedEffort { get; init; }

    /// <summary>
    /// Progression delta gained during the selected sprint for this epic (0-100).
    /// Computed as SprintCompletedEffort / TotalEffort * 100. Zero when not in single-sprint view.
    /// </summary>
    public double SprintProgressionDelta { get; init; }
}
