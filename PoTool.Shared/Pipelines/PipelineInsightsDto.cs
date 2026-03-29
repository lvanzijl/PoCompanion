namespace PoTool.Shared.Pipelines;

/// <summary>
/// Response for the Pipeline Insights endpoint.
/// Contains aggregated pipeline health metrics for the active PO, per sprint.
/// Phase 1: aggregation. Phase 2: scatter data added per product.
/// </summary>
public sealed class PipelineInsightsDto
{
    /// <summary>Selected sprint ID.</summary>
    public int SprintId { get; set; }

    /// <summary>Selected sprint display name.</summary>
    public string SprintName { get; set; } = string.Empty;

    /// <summary>Sprint start time (UTC). Used by the scatter component for X-axis boundaries.</summary>
    public DateTimeOffset? SprintStart { get; set; }

    /// <summary>Sprint end time (UTC). Used by the scatter component for X-axis boundaries.</summary>
    public DateTimeOffset? SprintEnd { get; set; }

    /// <summary>Previous sprint ID (for delta computation). Null when no predecessor exists in cache.</summary>
    public int? PreviousSprintId { get; set; }

    /// <summary>Previous sprint display name. Null when no predecessor exists.</summary>
    public string? PreviousSprintName { get; set; }

    // ── Global summary chips ──────────────────────────────────────────────

    /// <summary>Total builds (all statuses) in the sprint across all products.</summary>
    public int TotalBuilds { get; set; }

    /// <summary>Completed builds (excluded: Unknown, None, Canceled when not included).</summary>
    public int CompletedBuilds { get; set; }

    /// <summary>Failed builds count across all products.</summary>
    public int FailedBuilds { get; set; }

    /// <summary>Failure rate percentage (0–100) across all products.</summary>
    public double FailureRate { get; set; }

    /// <summary>Warning builds (partiallySucceeded) count across all products.</summary>
    public int WarningBuilds { get; set; }

    /// <summary>Warning rate percentage (0–100) across all products.</summary>
    public double WarningRate { get; set; }

    /// <summary>P90 duration in minutes across all products. Null when insufficient data.</summary>
    public double? P90DurationMinutes { get; set; }

    // ── Global top-3 pipelines in trouble ────────────────────────────────

    /// <summary>
    /// Top 3 most troubled pipelines globally (across all PO products), ranked by failure rate.
    /// May contain fewer than 3 entries when not enough distinct pipelines have data.
    /// </summary>
    public IReadOnlyList<PipelineTroubleEntryDto> GlobalTop3InTrouble { get; set; }
        = Array.Empty<PipelineTroubleEntryDto>();

    // ── Per-product sections ──────────────────────────────────────────────

    /// <summary>One section per product owned by the active Product Owner, ordered by product name.</summary>
    public IReadOnlyList<ProductPipelineInsightsDto> Products { get; set; }
        = Array.Empty<ProductPipelineInsightsDto>();
}

/// <summary>
/// Pipeline health summary for a single product.
/// </summary>
public sealed class ProductPipelineInsightsDto
{
    /// <summary>Product ID.</summary>
    public int ProductId { get; set; }

    /// <summary>Product name.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>True when the product has any cached pipeline runs in the sprint.</summary>
    public bool HasData { get; set; }

    // ── Product summary chips ─────────────────────────────────────────────

    /// <summary>Total builds in the sprint for this product.</summary>
    public int TotalBuilds { get; set; }

    /// <summary>Completed builds for this product.</summary>
    public int CompletedBuilds { get; set; }

    /// <summary>Failed builds count.</summary>
    public int FailedBuilds { get; set; }

    /// <summary>Failure rate percentage (0–100). Zero when no completed builds.</summary>
    public double FailureRate { get; set; }

    /// <summary>Warning builds (partiallySucceeded) count.</summary>
    public int WarningBuilds { get; set; }

    /// <summary>Warning rate percentage (0–100). Zero when no completed builds.</summary>
    public double WarningRate { get; set; }

    /// <summary>Succeeded builds count.</summary>
    public int SucceededBuilds { get; set; }

    /// <summary>Success rate percentage (0–100). Zero when no completed builds.</summary>
    public double SuccessRate { get; set; }

    /// <summary>Median build duration in minutes. Null when no duration data.</summary>
    public double? MedianDurationMinutes { get; set; }

    /// <summary>P90 build duration in minutes. Null when fewer than 3 runs with duration data.</summary>
    public double? P90DurationMinutes { get; set; }

    // ── Top 3 in trouble for this product ────────────────────────────────

    /// <summary>Top 3 most troubled pipelines for this product, ranked by failure rate.</summary>
    public IReadOnlyList<PipelineTroubleEntryDto> Top3InTrouble { get; set; }
        = Array.Empty<PipelineTroubleEntryDto>();

    // ── Scatter data ──────────────────────────────────────────────────────

    /// <summary>
    /// All cached pipeline run points for this product in the selected sprint.
    /// Used to render the TimeScatterSvg.
    /// Only populated when the product has data (HasData = true).
    /// </summary>
    public IReadOnlyList<PipelineScatterPointDto> ScatterPoints { get; set; }
        = Array.Empty<PipelineScatterPointDto>();

    // ── Per-pipeline breakdown ────────────────────────────────────────────

    /// <summary>
    /// Full per-pipeline breakdown for this product, ordered by failure rate descending.
    /// Each entry covers one distinct pipeline definition active in the selected sprint.
    /// Only populated when HasData = true.
    /// </summary>
    public IReadOnlyList<PipelineBreakdownEntryDto> PipelineBreakdown { get; set; }
        = Array.Empty<PipelineBreakdownEntryDto>();
}

/// <summary>
/// A single entry in the "pipelines in trouble" list.
/// Ranked by failure rate (descending), tie-broken by completed builds (descending) then pipeline name.
/// </summary>
public sealed class PipelineTroubleEntryDto
{
    /// <summary>External TFS pipeline definition ID.</summary>
    public int PipelineDefinitionId { get; set; }

    /// <summary>Pipeline display name.</summary>
    public string PipelineName { get; set; } = string.Empty;

    /// <summary>Product ID owning this pipeline.</summary>
    public int ProductId { get; set; }

    /// <summary>Product display name.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Rank in this list (1 = worst).</summary>
    public int Rank { get; set; }

    // ── Current sprint ────────────────────────────────────────────────────

    /// <summary>Completed builds count in the current sprint.</summary>
    public int CompletedBuilds { get; set; }

    /// <summary>Failed builds count in the current sprint.</summary>
    public int FailedBuilds { get; set; }

    /// <summary>Failure rate percentage (0–100) in the current sprint.</summary>
    public double FailureRate { get; set; }

    /// <summary>Warning builds (partiallySucceeded) count in the current sprint.</summary>
    public int WarningBuilds { get; set; }

    /// <summary>Warning rate percentage (0–100) in the current sprint.</summary>
    public double WarningRate { get; set; }

    // ── Delta versus previous sprint ──────────────────────────────────────

    /// <summary>
    /// Change in failure rate versus the previous sprint (percentage points).
    /// Null when no previous sprint data is available (n/a).
    /// Positive = worse, negative = improved.
    /// </summary>
    public double? DeltaFailureRate { get; set; }

    /// <summary>
    /// Change in warning rate versus the previous sprint (percentage points).
    /// Null when no previous sprint data is available (n/a).
    /// </summary>
    public double? DeltaWarningRate { get; set; }
}

/// <summary>
/// A single pipeline run data point for the scatter visualization.
/// X = start time, Y = duration in minutes.
/// </summary>
public sealed class PipelineScatterPointDto
{
    /// <summary>Database PK of CachedPipelineRunEntity.</summary>
    public int Id { get; set; }

    /// <summary>TFS run ID (used in Azure DevOps URL).</summary>
    public int TfsRunId { get; set; }

    /// <summary>External TFS pipeline definition ID (for highlight filtering and drilldown).</summary>
    public int PipelineDefinitionId { get; set; }

    /// <summary>Pipeline display name.</summary>
    public string PipelineName { get; set; } = string.Empty;

    /// <summary>Build number / run name (e.g., "20260101.5").</summary>
    public string? BuildNumber { get; set; }

    /// <summary>Run result (Succeeded, Failed, PartiallySucceeded, Canceled).</summary>
    public string? Result { get; set; }

    /// <summary>Build start time (when the run was created).</summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>Build finish time.</summary>
    public DateTimeOffset? FinishTime { get; set; }

    /// <summary>Build duration in minutes. Null when start or finish time is missing.</summary>
    public double? DurationMinutes { get; set; }

    /// <summary>Source branch (optional).</summary>
    public string? Branch { get; set; }

    /// <summary>Azure DevOps URL to the pipeline run (optional, for the Build Summary Drawer link).</summary>
    public string? Url { get; set; }
}

/// <summary>
/// Direction of pipeline health within the sprint, computed by comparing
/// failure rates in the first half vs. the second half.
///
/// Threshold: difference must be ≥ 10 percentage points to be classified
/// as Improving or Degrading; otherwise Stable.
/// Insufficient: fewer than 2 completed runs in either half.
/// </summary>
public enum PipelineHalfSprintTrend
{
    /// <summary>Not enough runs in one or both halves to determine a trend.</summary>
    Insufficient = 0,

    /// <summary>Failure rate in the second half is lower by ≥ 10 pp vs. the first half.</summary>
    Improving = 1,

    /// <summary>Failure rate in the second half is higher by ≥ 10 pp vs. the first half.</summary>
    Degrading = 2,

    /// <summary>Failure rate difference between halves is < 10 pp.</summary>
    Stable = 3
}

/// <summary>
/// Per-pipeline health breakdown for a single product in a single sprint.
/// Ordered by failure rate descending (worst first).
/// </summary>
public sealed class PipelineBreakdownEntryDto
{
    /// <summary>External TFS pipeline definition ID.</summary>
    public int PipelineDefinitionId { get; set; }

    /// <summary>Pipeline display name.</summary>
    public string PipelineName { get; set; } = string.Empty;

    // ── Volume ────────────────────────────────────────────────────────────

    /// <summary>Total runs in the sprint (all statuses).</summary>
    public int TotalRuns { get; set; }

    /// <summary>Completed runs (after applying include-partial / include-canceled toggles).</summary>
    public int CompletedRuns { get; set; }

    /// <summary>Succeeded runs.</summary>
    public int SucceededRuns { get; set; }

    /// <summary>Failed runs.</summary>
    public int FailedRuns { get; set; }

    /// <summary>Warning (partiallySucceeded) runs.</summary>
    public int WarningRuns { get; set; }

    // ── Rates ─────────────────────────────────────────────────────────────

    /// <summary>Success rate percentage (0–100).</summary>
    public double SuccessRate { get; set; }

    /// <summary>Failure rate percentage (0–100).</summary>
    public double FailureRate { get; set; }

    /// <summary>Warning rate percentage (0–100).</summary>
    public double WarningRate { get; set; }

    // ── Duration ──────────────────────────────────────────────────────────

    /// <summary>Median build duration in minutes. Null when no duration data.</summary>
    public double? MedianDurationMinutes { get; set; }

    /// <summary>P90 build duration in minutes. Null when fewer than 3 runs with duration data.</summary>
    public double? P90DurationMinutes { get; set; }

    // ── Delta vs. previous sprint ─────────────────────────────────────────

    /// <summary>
    /// Change in failure rate vs. the previous sprint (percentage points).
    /// Positive = worse; negative = improved. Null when no previous sprint data.
    /// </summary>
    public double? DeltaFailureRate { get; set; }

    // ── Half-sprint trend ─────────────────────────────────────────────────

    /// <summary>
    /// Direction of pipeline health within the sprint.
    /// Computed by comparing failure rates in the first half vs. the second half.
    /// </summary>
    public PipelineHalfSprintTrend HalfSprintTrend { get; set; } = PipelineHalfSprintTrend.Insufficient;

    /// <summary>
    /// Failure rate in the first half of the sprint (0–100).
    /// Null when insufficient runs.
    /// </summary>
    public double? FirstHalfFailureRate { get; set; }

    /// <summary>
    /// Failure rate in the second half of the sprint (0–100).
    /// Null when insufficient runs.
    /// </summary>
    public double? SecondHalfFailureRate { get; set; }
}
