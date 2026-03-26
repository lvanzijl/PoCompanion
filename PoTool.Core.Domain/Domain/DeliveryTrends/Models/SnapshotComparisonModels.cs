namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Prepared product-level snapshot values produced by the canonical aggregation layer.
/// </summary>
public sealed record ProductSnapshot(
    double? ProductProgress,
    double? ProductForecastConsumed,
    double? ProductForecastRemaining);

/// <summary>
/// Comparison request for two snapshots of the same product.
/// </summary>
public sealed record SnapshotComparisonRequest(
    ProductSnapshot? Previous,
    ProductSnapshot Current);

/// <summary>
/// Delta output produced strictly by comparing canonical snapshot values.
/// </summary>
public sealed record SnapshotComparisonResult(
    double? ProgressDelta,
    double? ForecastConsumedDelta,
    double? ForecastRemainingDelta);
