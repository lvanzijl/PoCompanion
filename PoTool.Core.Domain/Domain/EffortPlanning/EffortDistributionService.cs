using PoTool.Shared.Metrics;

namespace PoTool.Core.Domain.EffortPlanning;

/// <summary>
/// Produces canonical effort-distribution rollups from preloaded work-item snapshots.
/// </summary>
public interface IEffortDistributionService
{
    /// <summary>
    /// Builds area, iteration, and heat-map effort totals for the selected snapshot scope.
    /// </summary>
    EffortDistributionResult Analyze(
        IReadOnlyList<EffortPlanningWorkItem> workItems,
        int maxIterations,
        int? defaultCapacityPerIteration);
}

/// <summary>
/// Implements canonical effort-distribution formulas inside the EffortPlanning CDC slice.
/// </summary>
public sealed class EffortDistributionService : IEffortDistributionService
{
    /// <inheritdoc />
    public EffortDistributionResult Analyze(
        IReadOnlyList<EffortPlanningWorkItem> workItems,
        int maxIterations,
        int? defaultCapacityPerIteration)
    {
        ArgumentNullException.ThrowIfNull(workItems);

        var workItemsWithEffort = workItems
            .Where(static workItem => workItem.Effort.HasValue && workItem.Effort.Value > 0)
            .ToList();

        if (workItemsWithEffort.Count == 0)
        {
            return new EffortDistributionResult(
                Array.Empty<EffortAreaDistributionResult>(),
                Array.Empty<EffortIterationDistributionResult>(),
                Array.Empty<EffortHeatMapCellResult>(),
                TotalEffort: 0);
        }

        var areaPathsByVolume = workItemsWithEffort
            .GroupBy(static workItem => workItem.AreaPath)
            .OrderByDescending(static group => group.Count())
            .Take(10)
            .Select(static group => group.Key)
            .ToList();

        var iterationPaths = workItemsWithEffort
            .Where(static workItem => !string.IsNullOrWhiteSpace(workItem.IterationPath))
            .Select(static workItem => workItem.IterationPath)
            .Distinct()
            .OrderByDescending(static path => path)
            .Take(Math.Max(0, maxIterations))
            .ToList();

        return new EffortDistributionResult(
            CalculateEffortByAreaPath(workItemsWithEffort, areaPathsByVolume),
            CalculateEffortByIteration(workItemsWithEffort, iterationPaths, defaultCapacityPerIteration),
            CalculateHeatMapCells(workItemsWithEffort, areaPathsByVolume, iterationPaths, defaultCapacityPerIteration),
            workItemsWithEffort.Sum(static workItem => workItem.Effort ?? 0));
    }

    private static IReadOnlyList<EffortAreaDistributionResult> CalculateEffortByAreaPath(
        IReadOnlyList<EffortPlanningWorkItem> workItems,
        IReadOnlyList<string> areaPathsToInclude)
    {
        return workItems
            .Where(workItem => areaPathsToInclude.Contains(workItem.AreaPath, StringComparer.Ordinal))
            .GroupBy(static workItem => workItem.AreaPath)
            .Select(group => new EffortAreaDistributionResult(
                group.Key,
                TotalEffort: group.Sum(static workItem => workItem.Effort ?? 0),
                WorkItemCount: group.Count(),
                AverageEffortPerItem: group.Average(static workItem => workItem.Effort ?? 0)))
            .OrderByDescending(static result => result.TotalEffort)
            .ToList();
    }

    private static IReadOnlyList<EffortIterationDistributionResult> CalculateEffortByIteration(
        IReadOnlyList<EffortPlanningWorkItem> workItems,
        IReadOnlyList<string> iterationPaths,
        int? defaultCapacity)
    {
        return iterationPaths
            .Select(iterationPath =>
            {
                var itemsInIteration = workItems
                    .Where(workItem => string.Equals(workItem.IterationPath, iterationPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var totalEffort = itemsInIteration.Sum(static workItem => workItem.Effort ?? 0);
                var utilization = defaultCapacity.HasValue && defaultCapacity.Value > 0
                    ? (double)totalEffort / defaultCapacity.Value * 100d
                    : 0d;

                return new EffortIterationDistributionResult(
                    iterationPath,
                    ExtractSprintName(iterationPath),
                    totalEffort,
                    itemsInIteration.Count,
                    defaultCapacity,
                    utilization);
            })
            .ToList();
    }

    private static IReadOnlyList<EffortHeatMapCellResult> CalculateHeatMapCells(
        IReadOnlyList<EffortPlanningWorkItem> workItems,
        IReadOnlyList<string> areaPaths,
        IReadOnlyList<string> iterationPaths,
        int? defaultCapacity)
    {
        var cells = new List<EffortHeatMapCellResult>(areaPaths.Count * iterationPaths.Count);

        foreach (var areaPath in areaPaths)
        {
            foreach (var iterationPath in iterationPaths)
            {
                var itemsInCell = workItems
                    .Where(workItem =>
                        string.Equals(workItem.AreaPath, areaPath, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(workItem.IterationPath, iterationPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var effort = itemsInCell.Sum(static workItem => workItem.Effort ?? 0);
                cells.Add(new EffortHeatMapCellResult(
                    areaPath,
                    iterationPath,
                    effort,
                    itemsInCell.Count,
                    DetermineCapacityStatus(effort, defaultCapacity)));
            }
        }

        return cells;
    }

    private static CapacityStatus DetermineCapacityStatus(int effort, int? capacity)
    {
        if (!capacity.HasValue || capacity.Value == 0)
        {
            return CapacityStatus.Unknown;
        }

        var utilizationPercentage = (double)effort / capacity.Value * 100d;
        return utilizationPercentage switch
        {
            < 50d => CapacityStatus.Underutilized,
            >= 50d and < 85d => CapacityStatus.Normal,
            >= 85d and < 100d => CapacityStatus.NearCapacity,
            _ => CapacityStatus.OverCapacity
        };
    }

    private static string ExtractSprintName(string iterationPath)
    {
        var parts = iterationPath.Split('\\', '/');
        return parts.Length > 0 ? parts[^1] : iterationPath;
    }
}
