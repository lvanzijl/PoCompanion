namespace PoTool.Shared.Metrics;

/// <summary>
/// Top-level DTO for portfolio delivery snapshot across products for a selected sprint range.
/// Aggregates delivery data — no time-series information.
/// </summary>
public record PortfolioDeliveryDto
{
    /// <summary>
    /// Aggregated summary metrics across all products and sprints.
    /// </summary>
    public required PortfolioDeliverySummaryDto Summary { get; init; }

    /// <summary>
    /// Per-product delivery breakdown, ordered by descending completed effort.
    /// </summary>
    public required IReadOnlyList<ProductDeliveryDto> Products { get; init; }

    /// <summary>
    /// Top contributing features by delivered effort, ordered descending.
    /// Limited to top contributors for readability.
    /// </summary>
    public required IReadOnlyList<FeatureDeliveryDto> TopFeatures { get; init; }

    /// <summary>
    /// Number of sprints included in this snapshot.
    /// </summary>
    public int SprintCount { get; init; }

    /// <summary>
    /// Whether any data was found for the requested sprint range.
    /// </summary>
    public bool HasData { get; init; }
}

/// <summary>
/// Aggregated summary metrics for the portfolio delivery snapshot.
/// </summary>
public record PortfolioDeliverySummaryDto
{
    /// <summary>
    /// Total number of PBIs completed across all products and sprints.
    /// </summary>
    public int TotalCompletedPbis { get; init; }

    /// <summary>
    /// Total story points delivered across all products and sprints.
    /// </summary>
    public int TotalCompletedEffort { get; init; }

    /// <summary>
    /// Average progression delta percentage across all products and sprints.
    /// </summary>
    public double AverageProgressPercent { get; init; }

    /// <summary>
    /// Total bugs created across all products and sprints.
    /// </summary>
    public int TotalBugsCreated { get; init; }

    /// <summary>
    /// Total bugs worked (with activity) across all products and sprints.
    /// </summary>
    public int TotalBugsWorked { get; init; }

    /// <summary>
    /// Total bugs closed across all products and sprints.
    /// </summary>
    public int TotalBugsClosed { get; init; }
}

/// <summary>
/// Per-product delivery metrics aggregated across the selected sprint range.
/// </summary>
public record ProductDeliveryDto
{
    /// <summary>
    /// Product database ID.
    /// </summary>
    public required int ProductId { get; init; }

    /// <summary>
    /// Product display name.
    /// </summary>
    public required string ProductName { get; init; }

    /// <summary>
    /// Total PBIs completed for this product in the sprint range.
    /// </summary>
    public int CompletedPbis { get; init; }

    /// <summary>
    /// Total story points delivered for this product in the sprint range.
    /// </summary>
    public int CompletedEffort { get; init; }

    /// <summary>
    /// Share of total portfolio delivered effort (0–100 percentage).
    /// </summary>
    public double EffortShare { get; init; }

    /// <summary>
    /// Total bugs created for this product in the sprint range.
    /// </summary>
    public int BugsCreated { get; init; }

    /// <summary>
    /// Total bugs worked for this product in the sprint range.
    /// </summary>
    public int BugsWorked { get; init; }

    /// <summary>
    /// Total bugs closed for this product in the sprint range.
    /// </summary>
    public int BugsClosed { get; init; }

    /// <summary>
    /// Cumulative progression delta for this product across the sprint range (0–100 scale).
    /// </summary>
    public double ProgressionDelta { get; init; }
}

/// <summary>
/// Feature-level delivery contribution for the portfolio delivery snapshot.
/// </summary>
public record FeatureDeliveryDto
{
    /// <summary>
    /// Feature TFS ID.
    /// </summary>
    public required int FeatureId { get; init; }

    /// <summary>
    /// Feature display title.
    /// </summary>
    public required string FeatureTitle { get; init; }

    /// <summary>
    /// Parent epic title (null if not under an epic).
    /// </summary>
    public string? EpicTitle { get; init; }

    /// <summary>
    /// Product database ID.
    /// </summary>
    public required int ProductId { get; init; }

    /// <summary>
    /// Product display name.
    /// </summary>
    public required string ProductName { get; init; }

    /// <summary>
    /// Canonical story-point scope delivered for this feature in the sprint range.
    /// The legacy property name is retained for API compatibility and values may be fractional.
    /// Compatibility alias; deprecated in future contract revision.
    /// </summary>
    public double SprintCompletedEffort { get; init; }

    /// <summary>
    /// Canonical delivered story points for this feature in the sprint range.
    /// Maps to the same internal value as SprintCompletedEffort.
    /// </summary>
    public double DeliveredStoryPoints => SprintCompletedEffort;

    /// <summary>
    /// Total canonical story-point scope of all PBIs under this feature.
    /// The legacy property name is retained for API compatibility and values may be fractional.
    /// Compatibility alias; deprecated in future contract revision.
    /// </summary>
    public double TotalEffort { get; init; }

    /// <summary>
    /// Share of total portfolio delivered effort for this feature (0–100 percentage).
    /// </summary>
    public double EffortShare { get; init; }

    /// <summary>
    /// Overall feature progress percentage (0–100).
    /// </summary>
    public int ProgressPercent { get; init; }
}
