using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Compares two canonical product snapshots without reinterpreting the underlying data.
/// </summary>
public interface ISnapshotComparisonService
{
    /// <summary>
    /// Computes product-level deltas between a previous snapshot and the current snapshot.
    /// </summary>
    SnapshotComparisonResult Compare(SnapshotComparisonRequest request);
}

/// <summary>
/// Pure delta engine for canonical product snapshots.
/// </summary>
public sealed class SnapshotComparisonService : ISnapshotComparisonService
{
    /// <inheritdoc />
    public SnapshotComparisonResult Compare(SnapshotComparisonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Current);

        return new SnapshotComparisonResult(
            ComputeDelta(request.Previous?.ProductProgress, request.Current.ProductProgress),
            ComputeDelta(request.Previous?.ProductForecastConsumed, request.Current.ProductForecastConsumed),
            ComputeDelta(request.Previous?.ProductForecastRemaining, request.Current.ProductForecastRemaining));
    }

    private static double? ComputeDelta(double? previous, double? current)
    {
        if (!previous.HasValue || !current.HasValue)
        {
            return null;
        }

        return current.Value - previous.Value;
    }
}
