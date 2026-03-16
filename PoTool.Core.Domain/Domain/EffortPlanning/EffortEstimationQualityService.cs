using PoTool.Core.Domain.Statistics;

namespace PoTool.Core.Domain.EffortPlanning;

/// <summary>
/// Produces canonical effort-estimation quality rollups from completed work items with effort.
/// </summary>
public interface IEffortEstimationQualityService
{
    /// <summary>
    /// Builds per-type, per-period, and overall effort-estimation quality results.
    /// </summary>
    EffortEstimationQualityResult Analyze(
        IReadOnlyList<EffortPlanningWorkItem> completedWorkItems,
        int maxIterations);
}

/// <summary>
/// Implements canonical effort-estimation quality formulas inside the EffortPlanning CDC slice.
/// </summary>
public sealed class EffortEstimationQualityService : IEffortEstimationQualityService
{
    /// <inheritdoc />
    public EffortEstimationQualityResult Analyze(
        IReadOnlyList<EffortPlanningWorkItem> completedWorkItems,
        int maxIterations)
    {
        ArgumentNullException.ThrowIfNull(completedWorkItems);

        var estimatedWorkItems = completedWorkItems
            .Where(static workItem => workItem.Effort.HasValue && workItem.Effort.Value > 0)
            .OrderByDescending(static workItem => workItem.RetrievedAt)
            .ToList();

        if (estimatedWorkItems.Count == 0)
        {
            return new EffortEstimationQualityResult(
                AverageEstimationAccuracy: 0d,
                TotalCompletedWorkItems: 0,
                WorkItemsWithEstimates: 0,
                QualityByType: Array.Empty<EffortTypeQualityResult>(),
                TrendOverTime: Array.Empty<EffortQualityTrendResult>());
        }

        var iterationGroups = estimatedWorkItems
            .GroupBy(static workItem => workItem.IterationPath)
            .OrderByDescending(static group => group.Max(workItem => workItem.RetrievedAt))
            .Take(Math.Max(0, maxIterations))
            .ToList();

        return new EffortEstimationQualityResult(
            AverageEstimationAccuracy: CalculateOverallAccuracy(estimatedWorkItems),
            TotalCompletedWorkItems: estimatedWorkItems.Count,
            WorkItemsWithEstimates: estimatedWorkItems.Count,
            QualityByType: CalculateQualityByType(estimatedWorkItems),
            TrendOverTime: CalculateTrendOverTime(iterationGroups));
    }

    private static IReadOnlyList<EffortTypeQualityResult> CalculateQualityByType(
        IReadOnlyList<EffortPlanningWorkItem> workItems)
    {
        return workItems
            .GroupBy(static workItem => workItem.WorkItemType)
            .Select(group =>
            {
                var efforts = group
                    .Select(static workItem => workItem.Effort!.Value)
                    .ToList();

                var averageEffort = (int)Math.Round(efforts.Average());
                var accuracy = CalculateAccuracy(efforts, averageEffort);

                return new EffortTypeQualityResult(
                    group.Key,
                    group.Count(),
                    accuracy,
                    efforts.Min(),
                    efforts.Max(),
                    averageEffort);
            })
            .OrderByDescending(static result => result.Count)
            .ToList();
    }

    private static IReadOnlyList<EffortQualityTrendResult> CalculateTrendOverTime(
        IReadOnlyList<IGrouping<string, EffortPlanningWorkItem>> iterationGroups)
    {
        return iterationGroups
            .Select(group =>
            {
                var items = group.ToList();
                var efforts = items
                    .Select(static workItem => workItem.Effort!.Value)
                    .ToList();

                var averageEffort = efforts.Average();
                return new EffortQualityTrendResult(
                    group.Key,
                    StartDate: items.Min(static workItem => workItem.RetrievedAt),
                    EndDate: items.Max(static workItem => workItem.RetrievedAt),
                    AverageAccuracy: CalculateAccuracy(efforts, averageEffort),
                    EstimatedCount: efforts.Count);
            })
            .OrderBy(static result => result.StartDate)
            .ToList();
    }

    private static double CalculateOverallAccuracy(IReadOnlyList<EffortPlanningWorkItem> workItems)
    {
        if (workItems.Count == 0)
        {
            return 0d;
        }

        var totalItems = workItems.Count;
        return workItems
            .GroupBy(static workItem => workItem.WorkItemType)
            .Sum(group =>
            {
                var efforts = group
                    .Select(static workItem => workItem.Effort!.Value)
                    .ToList();
                var averageEffort = efforts.Average();
                var weight = (double)group.Count() / totalItems;
                return CalculateAccuracy(efforts, averageEffort) * weight;
            });
    }

    private static double CalculateAccuracy(IReadOnlyList<int> efforts, double averageEffort)
    {
        var variance = StatisticsMath.Variance(efforts.Select(static value => (double)value));
        var coefficientOfVariation = averageEffort > 0d
            ? Math.Sqrt(variance) / averageEffort
            : 0d;

        return Math.Max(0d, 1d - Math.Min(1d, coefficientOfVariation));
    }
}
