using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public interface IPortfolioDecisionSignalService
{
    IReadOnlyList<PortfolioDecisionSignalDto> BuildSignals(
        PortfolioTrendDto trend,
        PortfolioComparisonDto comparison);
}

public sealed class PortfolioDecisionSignalService : IPortfolioDecisionSignalService
{
    public IReadOnlyList<PortfolioDecisionSignalDto> BuildSignals(
        PortfolioTrendDto trend,
        PortfolioComparisonDto comparison)
    {
        ArgumentNullException.ThrowIfNull(trend);
        ArgumentNullException.ThrowIfNull(comparison);

        var signals = new List<PortfolioDecisionSignalDto>();

        AddMetricSignal(
            signals,
            trend.PortfolioProgressTrend.Direction,
            PortfolioDecisionSignalType.ProgressImproving,
            PortfolioDecisionSignalType.ProgressDeclining,
            comparison.CurrentSnapshotLabel,
            comparison.CurrentTimestamp,
            "Portfolio progress is improving",
            "Portfolio progress increased versus the previous persisted snapshot.",
            "Portfolio progress is declining",
            "Portfolio progress decreased versus the previous persisted snapshot.");

        AddMetricSignal(
            signals,
            trend.TotalWeightTrend.Direction,
            PortfolioDecisionSignalType.WeightIncreasing,
            PortfolioDecisionSignalType.WeightDecreasing,
            comparison.CurrentSnapshotLabel,
            comparison.CurrentTimestamp,
            "Portfolio weight is increasing",
            "Total active portfolio weight increased versus the previous persisted snapshot.",
            "Portfolio weight is decreasing",
            "Total active portfolio weight decreased versus the previous persisted snapshot.");

        foreach (var item in comparison.Items
                     .Where(item => item.WorkPackage is not null
                                    && item.PreviousLifecycleState is null
                                    && item.CurrentLifecycleState == PortfolioLifecycleState.Active)
                     .OrderBy(item => item.ProductId)
                     .ThenBy(item => item.ProjectNumber, StringComparer.Ordinal)
                     .ThenBy(item => item.WorkPackage, StringComparer.Ordinal))
        {
            signals.Add(new PortfolioDecisionSignalDto
            {
                Type = PortfolioDecisionSignalType.NewWorkPackage,
                Tone = PortfolioDecisionSignalTone.Info,
                Title = "New work package introduced",
                Description = $"Work package {item.WorkPackage} appeared in the latest persisted snapshot.",
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ProjectNumber = item.ProjectNumber,
                WorkPackage = item.WorkPackage,
                LifecycleState = item.CurrentLifecycleState,
                SnapshotLabel = comparison.CurrentSnapshotLabel,
                SnapshotTimestamp = comparison.CurrentTimestamp
            });
        }

        foreach (var item in comparison.Items
                     .Where(item => item.WorkPackage is not null
                                    && item.PreviousLifecycleState == PortfolioLifecycleState.Active
                                    && item.CurrentLifecycleState == PortfolioLifecycleState.Retired)
                     .OrderBy(item => item.ProductId)
                     .ThenBy(item => item.ProjectNumber, StringComparer.Ordinal)
                     .ThenBy(item => item.WorkPackage, StringComparer.Ordinal))
        {
            signals.Add(new PortfolioDecisionSignalDto
            {
                Type = PortfolioDecisionSignalType.RetiredWorkPackage,
                Tone = PortfolioDecisionSignalTone.Info,
                Title = "Work package retired",
                Description = $"Work package {item.WorkPackage} is retained historically but excluded from active aggregation in the latest snapshot.",
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ProjectNumber = item.ProjectNumber,
                WorkPackage = item.WorkPackage,
                LifecycleState = item.CurrentLifecycleState,
                SnapshotLabel = comparison.CurrentSnapshotLabel,
                SnapshotTimestamp = comparison.CurrentTimestamp
            });
        }

        foreach (var trendItem in trend.WorkPackages
                     .Concat(trend.Projects)
                     .Where(HasRepeatedNoChange)
                     .OrderBy(item => item.ProductId)
                     .ThenBy(item => item.ProjectNumber, StringComparer.Ordinal)
                     .ThenBy(item => item.WorkPackage ?? string.Empty, StringComparer.Ordinal))
        {
            var latestPoint = trendItem.ProgressTrend.Points.FirstOrDefault();
            signals.Add(new PortfolioDecisionSignalDto
            {
                Type = PortfolioDecisionSignalType.RepeatedNoChange,
                Tone = PortfolioDecisionSignalTone.Info,
                Title = "Repeated no-change across snapshots",
                Description = trendItem.WorkPackage is null
                    ? $"Project {trendItem.ProjectNumber} kept the same active progress and weight across the recent persisted history."
                    : $"Work package {trendItem.WorkPackage} kept the same active progress and weight across the recent persisted history.",
                ProductId = trendItem.ProductId,
                ProductName = trendItem.ProductName,
                ProjectNumber = trendItem.ProjectNumber,
                WorkPackage = trendItem.WorkPackage,
                LifecycleState = trendItem.CurrentLifecycleState,
                SnapshotId = latestPoint?.SnapshotId,
                SnapshotLabel = latestPoint?.SnapshotLabel,
                SnapshotTimestamp = latestPoint?.Timestamp
            });
        }

        if (trend.ArchivedSnapshotsExcludedNotice)
        {
            signals.Add(new PortfolioDecisionSignalDto
            {
                Type = PortfolioDecisionSignalType.ArchivedSnapshotExcludedNotice,
                Tone = PortfolioDecisionSignalTone.Info,
                Title = "Archived snapshots excluded",
                Description = "Archived persisted snapshots exist in the selected history window but remain excluded until explicitly included.",
                SnapshotLabel = trend.Snapshots.FirstOrDefault()?.SnapshotLabel,
                SnapshotTimestamp = trend.Snapshots.FirstOrDefault()?.Timestamp
            });
        }

        return signals;
    }

    private static void AddMetricSignal(
        ICollection<PortfolioDecisionSignalDto> signals,
        PortfolioTrendDirection? direction,
        PortfolioDecisionSignalType increasingType,
        PortfolioDecisionSignalType decreasingType,
        string snapshotLabel,
        DateTimeOffset snapshotTimestamp,
        string increasingTitle,
        string increasingDescription,
        string decreasingTitle,
        string decreasingDescription)
    {
        if (direction == PortfolioTrendDirection.Increasing)
        {
            signals.Add(new PortfolioDecisionSignalDto
            {
                Type = increasingType,
                Tone = increasingType == PortfolioDecisionSignalType.WeightIncreasing
                    ? PortfolioDecisionSignalTone.Warning
                    : PortfolioDecisionSignalTone.Positive,
                Title = increasingTitle,
                Description = increasingDescription,
                SnapshotLabel = snapshotLabel,
                SnapshotTimestamp = snapshotTimestamp
            });
        }
        else if (direction == PortfolioTrendDirection.Decreasing)
        {
            signals.Add(new PortfolioDecisionSignalDto
            {
                Type = decreasingType,
                Tone = decreasingType == PortfolioDecisionSignalType.WeightDecreasing
                    ? PortfolioDecisionSignalTone.Positive
                    : PortfolioDecisionSignalTone.Warning,
                Title = decreasingTitle,
                Description = decreasingDescription,
                SnapshotLabel = snapshotLabel,
                SnapshotTimestamp = snapshotTimestamp
            });
        }
    }

    private static bool HasRepeatedNoChange(PortfolioScopedTrendDto trend)
    {
        const int minimumPoints = 3;

        var progressPoints = trend.ProgressTrend.Points
            .Take(minimumPoints)
            .ToArray();
        var weightPoints = trend.WeightTrend.Points
            .Take(minimumPoints)
            .ToArray();

        return progressPoints.Length == minimumPoints
               && weightPoints.Length == minimumPoints
               && progressPoints.All(point => point.Value.HasValue)
               && weightPoints.All(point => point.Value.HasValue)
               && progressPoints.All(point => point.Value == progressPoints[0].Value)
               && weightPoints.All(point => point.Value == weightPoints[0].Value);
    }
}
