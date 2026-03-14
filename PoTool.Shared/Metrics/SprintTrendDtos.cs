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
    /// Total resolved story points for planned PBIs.
    /// Includes real, fallback, and derived estimates.
    /// </summary>
    public double TotalPlannedStoryPoints { get; init; }

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
    /// Total delivered story points for completed PBIs in this sprint.
    /// Derived and missing estimates are excluded.
    /// </summary>
    public double TotalCompletedPbiStoryPoints { get; init; }

    /// <summary>
    /// Total committed PBIs that spilled into the next sprint unfinished.
    /// </summary>
    public int TotalSpilloverCount { get; init; }

    /// <summary>
    /// Total effort of committed PBIs that spilled into the next sprint unfinished.
    /// </summary>
    public int TotalSpilloverEffort { get; init; }

    /// <summary>
    /// Total resolved story points of committed PBIs that spilled unfinished into the next sprint.
    /// </summary>
    public double TotalSpilloverStoryPoints { get; init; }

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
    /// Total planned PBIs with no resolved story-point estimate.
    /// </summary>
    public int TotalMissingStoryPointCount { get; init; }

    /// <summary>
    /// Total planned PBIs whose story points were derived from sibling estimates.
    /// </summary>
    public int TotalDerivedStoryPointCount { get; init; }

    /// <summary>
    /// Total derived story points used for planned scope sizing.
    /// </summary>
    public double TotalDerivedStoryPoints { get; init; }

    /// <summary>
    /// Total delivered PBIs without an authoritative story-point estimate.
    /// </summary>
    public int TotalUnestimatedDeliveryCount { get; init; }

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
    /// Resolved story points for planned PBIs.
    /// Includes real, fallback, and derived estimates.
    /// </summary>
    public double PlannedStoryPoints { get; init; }

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
    /// Delivered story points for completed PBIs in this sprint for this product.
    /// Derived and missing estimates are excluded.
    /// </summary>
    public double CompletedPbiStoryPoints { get; init; }

    /// <summary>
    /// Committed PBIs that spilled into the next sprint unfinished for this product.
    /// </summary>
    public int SpilloverCount { get; init; }

    /// <summary>
    /// Effort of committed PBIs that spilled into the next sprint unfinished for this product.
    /// </summary>
    public int SpilloverEffort { get; init; }

    /// <summary>
    /// Resolved story points of committed PBIs that spilled into the next sprint unfinished for this product.
    /// </summary>
    public double SpilloverStoryPoints { get; init; }

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
    /// Planned PBIs with no resolved story-point estimate for this product.
    /// </summary>
    public int MissingStoryPointCount { get; init; }

    /// <summary>
    /// Planned PBIs whose story points were derived from sibling estimates for this product.
    /// </summary>
    public int DerivedStoryPointCount { get; init; }

    /// <summary>
    /// Aggregate derived story points used for planned scope sizing for this product.
    /// </summary>
    public double DerivedStoryPoints { get; init; }

    /// <summary>
    /// Delivered PBIs without an authoritative story-point estimate for this product.
    /// </summary>
    public int UnestimatedDeliveryCount { get; init; }

    /// <summary>
    /// Whether approximation was used for this product.
    /// </summary>
    public bool IsApproximate { get; init; }

    /// <summary>
    /// Net scope change in effort for this product during the sprint.
    /// Populated for the current or most recent sprint in the request.
    /// </summary>
    public int ScopeChangeEffort { get; init; }

    /// <summary>
    /// Number of features completed for this product during the sprint.
    /// Populated for the current or most recent sprint in the request.
    /// </summary>
    public int CompletedFeatureCount { get; init; }
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

    /// <summary>
    /// Whether feature/epic drilldown detail should be included.
    /// </summary>
    public bool IncludeDetails { get; init; } = true;
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
    /// Total canonical story-point scope of all PBIs under this feature.
    /// The legacy property name is retained for API compatibility and values may be fractional.
    /// </summary>
    public double TotalEffort { get; init; }

    /// <summary>
    /// Canonical story-point scope of done PBIs under this feature.
    /// The legacy property name is retained for API compatibility and values may be fractional.
    /// </summary>
    public double DoneEffort { get; init; }

    /// <summary>
    /// Number of PBIs in Done state under this feature.
    /// </summary>
    public int DonePbiCount { get; init; }

    /// <summary>
    /// Whether the feature is in Done state.
    /// </summary>
    public bool IsDone { get; init; }

    /// <summary>
    /// Canonical story-point scope of PBIs that closed (transitioned to Done) during the selected sprint.
    /// The legacy property name is retained for API compatibility and values may be fractional.
    /// Zero when not in single-sprint view.
    /// </summary>
    public double SprintCompletedEffort { get; init; }

    /// <summary>
    /// Progression delta gained during the selected sprint for this feature (0-100).
    /// Computed as SprintCompletedEffort / TotalEffort * 100. Zero when not in single-sprint view.
    /// </summary>
    public double SprintProgressionDelta { get; init; }

    /// <summary>
    /// Absolute effort scope change during the sprint for this feature.
    /// Computed as effort_end_of_sprint − effort_start_of_sprint for child PBIs.
    /// Positive = scope added, negative = scope reduced. Zero when not in single-sprint view.
    /// </summary>
    public int SprintEffortDelta { get; init; }

    /// <summary>
    /// Number of PBIs that transitioned to Done during the selected sprint under this feature.
    /// Zero when not in single-sprint view.
    /// </summary>
    public int SprintCompletedPbiCount { get; init; }

    /// <summary>
    /// Whether this feature itself transitioned to Done during the selected sprint.
    /// False when not in single-sprint view.
    /// </summary>
    public bool SprintCompletedInSprint { get; init; }

    /// <summary>
    /// Individual PBIs that were completed during the selected sprint under this feature.
    /// Empty when not in single-sprint view.
    /// </summary>
    public IReadOnlyList<CompletedPbiDto> CompletedPbis { get; init; } = Array.Empty<CompletedPbiDto>();
}

/// <summary>
/// DTO for epic-level progress information.
/// Epic progress is derived from child feature story-point rollups.
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
    /// Derived from child feature story-point rollups.
    /// Epic.State == Done => 100%, otherwise min(raw, 90%).
    /// </summary>
    public int ProgressPercent { get; init; }

    /// <summary>
    /// Total canonical story-point scope of all PBIs under this epic's features.
    /// The legacy property name is retained for API compatibility and values may be fractional.
    /// </summary>
    public double TotalEffort { get; init; }

    /// <summary>
    /// Canonical story-point scope of done PBIs under this epic's features.
    /// The legacy property name is retained for API compatibility and values may be fractional.
    /// </summary>
    public double DoneEffort { get; init; }

    /// <summary>
    /// Number of child features under this epic.
    /// </summary>
    public int FeatureCount { get; init; }

    /// <summary>
    /// Number of child features that are done.
    /// </summary>
    public int DoneFeatureCount { get; init; }

    /// <summary>
    /// Number of PBIs in Done state across all child features of this epic.
    /// </summary>
    public int DonePbiCount { get; init; }

    /// <summary>
    /// Whether the epic is in Done state.
    /// </summary>
    public bool IsDone { get; init; }

    /// <summary>
    /// Canonical story-point scope of PBIs that closed (transitioned to Done) during the selected sprint across this epic's features.
    /// The legacy property name is retained for API compatibility and values may be fractional.
    /// Zero when not in single-sprint view.
    /// </summary>
    public double SprintCompletedEffort { get; init; }

    /// <summary>
    /// Progression delta gained during the selected sprint for this epic (0-100).
    /// Computed as SprintCompletedEffort / TotalEffort * 100. Zero when not in single-sprint view.
    /// </summary>
    public double SprintProgressionDelta { get; init; }

    /// <summary>
    /// Absolute effort scope change during the sprint for this epic.
    /// Aggregated from child Feature SprintEffortDelta values.
    /// Positive = scope added, negative = scope reduced. Zero when not in single-sprint view.
    /// </summary>
    public int SprintEffortDelta { get; init; }

    /// <summary>
    /// Number of PBIs that transitioned to Done during the sprint across this epic's features.
    /// Zero when not in single-sprint view.
    /// </summary>
    public int SprintCompletedPbiCount { get; init; }

    /// <summary>
    /// Number of features that transitioned to Done during the sprint under this epic.
    /// Zero when not in single-sprint view.
    /// </summary>
    public int SprintCompletedFeatureCount { get; init; }
}

/// <summary>
/// DTO for an individual PBI completed during a sprint.
/// Used at the lowest level of the Sprint Delivery hierarchy.
/// </summary>
public record CompletedPbiDto
{
    /// <summary>
    /// TFS work item ID.
    /// </summary>
    public required int TfsId { get; init; }

    /// <summary>
    /// PBI title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Canonical story-point scope of this PBI.
    /// The legacy property name is retained for API compatibility and values may be fractional.
    /// </summary>
    public double Effort { get; init; }

    /// <summary>
    /// Date the PBI was closed (transitioned to Done). Null if not available.
    /// </summary>
    public DateTimeOffset? ClosedDate { get; init; }
}
