namespace PoTool.Shared.Metrics;

/// <summary>
/// Product-scoped read model that exposes the canonical delivery analytics outputs without recalculation.
/// </summary>
public sealed record ProductDeliveryAnalyticsDto
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
    /// Canonical product-level progress and forecast summary.
    /// </summary>
    public required ProductProgressSummaryDto Progress { get; init; }

    /// <summary>
    /// Canonical comparison against the previous available snapshot for this product.
    /// </summary>
    public required SnapshotComparisonDto Comparison { get; init; }

    /// <summary>
    /// Canonical planning-quality score and signals for this product.
    /// </summary>
    public required PlanningQualityDto PlanningQuality { get; init; }

    /// <summary>
    /// Canonical insights derived for this product.
    /// </summary>
    public IReadOnlyList<InsightDto> Insights { get; init; } = Array.Empty<InsightDto>();
}

/// <summary>
/// Product-level canonical summary values.
/// </summary>
public sealed record ProductProgressSummaryDto
{
    public double? ProductProgress { get; init; }

    public double? ProductForecastConsumed { get; init; }

    public double? ProductForecastRemaining { get; init; }

    public int ExcludedEpicsCount { get; init; }

    public int IncludedEpicsCount { get; init; }

    public double TotalWeight { get; init; }
}

/// <summary>
/// Canonical product comparison deltas.
/// </summary>
public sealed record SnapshotComparisonDto
{
    public double? ProgressDelta { get; init; }

    public double? ForecastConsumedDelta { get; init; }

    public double? ForecastRemainingDelta { get; init; }
}

/// <summary>
/// Canonical Planning Quality read model.
/// </summary>
public sealed record PlanningQualityDto
{
    public int PlanningQualityScore { get; init; }

    public IReadOnlyList<PlanningQualitySignalDto> PlanningQualitySignals { get; init; } = Array.Empty<PlanningQualitySignalDto>();
}

/// <summary>
/// Canonical Planning Quality signal read model.
/// </summary>
public sealed record PlanningQualitySignalDto
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Scope { get; init; }

    public required string Message { get; init; }

    public required int EntityId { get; init; }
}

/// <summary>
/// Canonical insight read model.
/// </summary>
public sealed record InsightDto
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public required InsightContextDto Context { get; init; }
}

/// <summary>
/// Explainability payload for an insight.
/// </summary>
public sealed record InsightContextDto
{
    public double? ProgressDelta { get; init; }

    public double? ForecastRemainingDelta { get; init; }

    public int PlanningQualityScore { get; init; }
}
