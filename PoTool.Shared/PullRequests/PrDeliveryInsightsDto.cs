namespace PoTool.Shared.PullRequests;

/// <summary>
/// Response for the PR Delivery Insights endpoint.
/// Contains PR classification, Epic/Feature aggregations, scatter chart data, and outliers.
/// All data is computed from the local cache — no live TFS/Azure DevOps calls.
/// </summary>
public sealed class PrDeliveryInsightsDto
{
    /// <summary>Team ID used for this result. Null when no team filter was applied.</summary>
    public int? TeamId { get; set; }

    /// <summary>Team display name.</summary>
    public string? TeamName { get; set; }

    /// <summary>Sprint ID used for this result. Null when using a custom date range.</summary>
    public int? SprintId { get; set; }

    /// <summary>Sprint display name.</summary>
    public string? SprintName { get; set; }

    /// <summary>Start of the date range.</summary>
    public DateTimeOffset FromDate { get; set; }

    /// <summary>End of the date range.</summary>
    public DateTimeOffset ToDate { get; set; }

    /// <summary>Global category summary (DeliveryMapped / Bug / Disturbance / Unmapped).</summary>
    public PRCategorySummaryDto CategorySummary { get; set; } = new();

    /// <summary>Per-Epic aggregated friction metrics. Sorted by PR count descending.</summary>
    public IReadOnlyList<EpicFrictionSummaryDto> EpicBreakdown { get; set; }
        = Array.Empty<EpicFrictionSummaryDto>();

    /// <summary>Per-Feature complexity metrics. Sorted by PR count descending.</summary>
    public IReadOnlyList<FeatureComplexitySummaryDto> FeatureBreakdown { get; set; }
        = Array.Empty<FeatureComplexitySummaryDto>();

    /// <summary>All scatter chart data points.</summary>
    public IReadOnlyList<PrDeliveryScatterPointDto> ScatterPoints { get; set; }
        = Array.Empty<PrDeliveryScatterPointDto>();

    /// <summary>Outlier PRs (top 20 by lifetime, filtered to completed/abandoned).</summary>
    public IReadOnlyList<PrOutlierDto> Outliers { get; set; }
        = Array.Empty<PrOutlierDto>();

    /// <summary>
    /// Rule-based team improvement tips derived from signal detection.
    /// At most three tips are included, ordered by signal severity.
    /// </summary>
    public IReadOnlyList<TeamImprovementTipDto> ImprovementTips { get; set; }
        = Array.Empty<TeamImprovementTipDto>();
}

/// <summary>
/// Global PR category summary showing count and percentage for each category.
/// </summary>
public sealed class PRCategorySummaryDto
{
    /// <summary>Total PRs in scope.</summary>
    public int TotalPrs { get; set; }

    /// <summary>Number of PRs classified as DeliveryMapped (linked to a Feature or Epic).</summary>
    public int DeliveryMappedCount { get; set; }

    /// <summary>Percentage of PRs classified as DeliveryMapped.</summary>
    public double DeliveryMappedPct { get; set; }

    /// <summary>Number of PRs classified as Bug (linked to a Bug with no Feature/Epic).</summary>
    public int BugCount { get; set; }

    /// <summary>Percentage of PRs classified as Bug.</summary>
    public double BugPct { get; set; }

    /// <summary>Number of PRs classified as Disturbance (PBI without a Feature parent).</summary>
    public int DisturbanceCount { get; set; }

    /// <summary>Percentage of PRs classified as Disturbance.</summary>
    public double DisturbancePct { get; set; }

    /// <summary>Number of PRs classified as Unmapped (no usable work item link).</summary>
    public int UnmappedCount { get; set; }

    /// <summary>Percentage of PRs classified as Unmapped.</summary>
    public double UnmappedPct { get; set; }
}

/// <summary>
/// Per-Epic aggregated friction metrics for the Epic breakdown table.
/// </summary>
public sealed class EpicFrictionSummaryDto
{
    /// <summary>Epic TFS ID.</summary>
    public int EpicId { get; set; }

    /// <summary>Epic title.</summary>
    public string EpicName { get; set; } = string.Empty;

    /// <summary>Total PR count linked to this Epic.</summary>
    public int PrCount { get; set; }

    /// <summary>Median PR lifetime in hours across all PRs linked to this Epic. Null when no data.</summary>
    public double? MedianLifetimeHours { get; set; }

    /// <summary>90th-percentile PR lifetime in hours. Null when sample &lt; 3 PRs.</summary>
    public double? P90LifetimeHours { get; set; }

    /// <summary>Percentage of PRs that were abandoned. Null when no data.</summary>
    public double? AbandonedPct { get; set; }

    /// <summary>Average review cycles across PRs linked to this Epic. Null when no data.</summary>
    public double? AvgReviewCycles { get; set; }
}

/// <summary>
/// Per-Feature complexity metrics for the Feature breakdown table.
/// </summary>
public sealed class FeatureComplexitySummaryDto
{
    /// <summary>Feature TFS ID.</summary>
    public int FeatureId { get; set; }

    /// <summary>Feature title.</summary>
    public string FeatureName { get; set; } = string.Empty;

    /// <summary>Epic TFS ID that this Feature belongs to. Null when not under an Epic.</summary>
    public int? EpicId { get; set; }

    /// <summary>Epic title. Null when not under an Epic.</summary>
    public string? EpicName { get; set; }

    /// <summary>Total PR count linked to this Feature.</summary>
    public int PrCount { get; set; }

    /// <summary>
    /// PR-to-PBI ratio: number of PRs divided by the number of PBIs under this Feature.
    /// Null when there are no PBIs.
    /// </summary>
    public double? PrPerPbiRatio { get; set; }

    /// <summary>Median PR lifetime in hours. Null when no data.</summary>
    public double? MedianLifetimeHours { get; set; }
}

/// <summary>
/// A single data point for the PR Delivery scatter chart.
/// X = PR creation timestamp; Y = PR lifetime in hours.
/// Color / shape encode the primary category and Epic.
/// </summary>
public sealed class PrDeliveryScatterPointDto
{
    /// <summary>PR identifier.</summary>
    public int Id { get; set; }

    /// <summary>PR title (shown in tooltip).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>PR creation time — X axis.</summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>PR lifetime in hours — Y axis.</summary>
    public double LifetimeHours { get; set; }

    /// <summary>PR result state: "completed", "abandoned", "active".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Primary category: "DeliveryMapped", "Bug", "Disturbance", "Unmapped".</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Epic TFS ID. Null when not mapped to an Epic.</summary>
    public int? EpicId { get; set; }

    /// <summary>Epic name. Null when not mapped to an Epic.</summary>
    public string? EpicName { get; set; }

    /// <summary>Feature TFS ID. Null when not mapped to a Feature.</summary>
    public int? FeatureId { get; set; }

    /// <summary>Feature name. Null when not mapped to a Feature.</summary>
    public string? FeatureName { get; set; }
}

/// <summary>
/// An outlier PR entry shown in the PR Delivery Insights outlier table.
/// Outliers are the longest-lived completed/abandoned PRs in scope.
/// </summary>
public sealed class PrOutlierDto
{
    /// <summary>PR identifier.</summary>
    public int Id { get; set; }

    /// <summary>PR title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Repository name.</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>PR status: "completed", "abandoned", or "active".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>PR lifetime in hours.</summary>
    public double LifetimeHours { get; set; }

    /// <summary>Number of distinct files changed.</summary>
    public int FilesChanged { get; set; }

    /// <summary>Number of review cycles (iteration count).</summary>
    public int ReviewCycles { get; set; }

    /// <summary>Epic TFS ID. Null when not mapped to an Epic.</summary>
    public int? EpicId { get; set; }

    /// <summary>Epic name. Null when not mapped to an Epic.</summary>
    public string? EpicName { get; set; }

    /// <summary>Feature TFS ID. Null when not mapped to a Feature.</summary>
    public int? FeatureId { get; set; }

    /// <summary>Feature name. Null when not mapped to a Feature.</summary>
    public string? FeatureName { get; set; }

    /// <summary>Primary classification category.</summary>
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// A single actionable team improvement tip derived from signal detection on PR analytics.
/// Each tip contains a detected signal, its interpretation, and a concise PO message.
/// </summary>
public sealed class TeamImprovementTipDto
{
    /// <summary>Short label for the detected signal pattern (e.g. "Long PR Lifetimes").</summary>
    public string Signal { get; set; } = string.Empty;

    /// <summary>Explanation of what the observed metric pattern likely indicates.</summary>
    public string Interpretation { get; set; } = string.Empty;

    /// <summary>Concise message the Product Owner can communicate to the team.</summary>
    public string PoMessage { get; set; } = string.Empty;
}
