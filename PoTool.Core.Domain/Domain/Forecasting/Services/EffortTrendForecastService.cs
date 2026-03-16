using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Statistics;

namespace PoTool.Core.Domain.Forecasting.Services;

public interface IEffortTrendForecastService
{
    EffortDistributionAnalysis Analyze(
        IReadOnlyList<EffortDistributionWorkItem> workItems,
        int maxIterations,
        int? defaultCapacityPerIteration);
}

public sealed class EffortTrendForecastService : IEffortTrendForecastService
{
    public EffortDistributionAnalysis Analyze(
        IReadOnlyList<EffortDistributionWorkItem> workItems,
        int maxIterations,
        int? defaultCapacityPerIteration)
    {
        ArgumentNullException.ThrowIfNull(workItems);

        var workItemsWithEffort = workItems
            .Where(static workItem => workItem.Effort > 0)
            .ToList();

        if (workItemsWithEffort.Count == 0)
        {
            return new EffortDistributionAnalysis(
                Array.Empty<EffortSprintTrend>(),
                Array.Empty<EffortAreaPathTrend>(),
                EffortForecastDirection.Stable,
                0,
                Array.Empty<EffortDistributionForecast>());
        }

        var iterationPaths = workItemsWithEffort
            .Where(static workItem => !string.IsNullOrWhiteSpace(workItem.IterationPath))
            .Select(static workItem => workItem.IterationPath)
            .Distinct()
            .OrderBy(static path => path)
            .Take(maxIterations)
            .ToList();

        var topAreaPaths = workItemsWithEffort
            .GroupBy(static workItem => workItem.AreaPath)
            .OrderByDescending(static group => group.Count())
            .Take(10)
            .Select(static group => group.Key)
            .ToList();

        var sprintTrends = AnalyzeSprintTrends(workItemsWithEffort, iterationPaths, defaultCapacityPerIteration);
        var areaPathTrends = AnalyzeAreaPathTrends(workItemsWithEffort, topAreaPaths, iterationPaths);
        var (overallTrend, trendSlope) = CalculateOverallTrend(sprintTrends);
        var forecasts = GenerateForecasts(sprintTrends);

        return new EffortDistributionAnalysis(
            sprintTrends,
            areaPathTrends,
            overallTrend,
            trendSlope,
            forecasts);
    }

    private static IReadOnlyList<EffortSprintTrend> AnalyzeSprintTrends(
        IReadOnlyList<EffortDistributionWorkItem> workItems,
        IReadOnlyList<string> iterationPaths,
        int? defaultCapacity)
    {
        var trends = new List<EffortSprintTrend>();
        int? previousEffort = null;

        foreach (var iterationPath in iterationPaths)
        {
            var itemsInSprint = workItems
                .Where(workItem => string.Equals(workItem.IterationPath, iterationPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var totalEffort = itemsInSprint.Sum(static workItem => workItem.Effort);
            var workItemCount = itemsInSprint.Count;
            var utilization = defaultCapacity.HasValue && defaultCapacity.Value > 0
                ? (double)totalEffort / defaultCapacity.Value * 100
                : 0;

            var changeFromPrevious = previousEffort.HasValue
                ? ((double)(totalEffort - previousEffort.Value) / (previousEffort.Value > 0 ? previousEffort.Value : 1)) * 100
                : 0;

            trends.Add(new EffortSprintTrend(
                iterationPath,
                ExtractSprintName(iterationPath),
                totalEffort,
                workItemCount,
                utilization,
                changeFromPrevious,
                DetermineEffortTrendDirection(changeFromPrevious)));

            previousEffort = totalEffort;
        }

        return trends;
    }

    private static IReadOnlyList<EffortAreaPathTrend> AnalyzeAreaPathTrends(
        IReadOnlyList<EffortDistributionWorkItem> workItems,
        IReadOnlyList<string> areaPaths,
        IReadOnlyList<string> iterationPaths)
    {
        return areaPaths
            .Select(areaPath =>
            {
                var effortBySprint = iterationPaths
                    .Select(iterationPath => workItems
                        .Where(workItem => string.Equals(workItem.AreaPath, areaPath, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(workItem.IterationPath, iterationPath, StringComparison.OrdinalIgnoreCase))
                        .Sum(static workItem => workItem.Effort))
                    .ToList();

                var averageEffort = effortBySprint.Count > 0 ? effortBySprint.Average() : 0;
                var standardDeviation = CalculateStandardDeviation(effortBySprint);
                var trendSlope = CalculateLinearRegressionSlope(effortBySprint);

                return new EffortAreaPathTrend(
                    areaPath,
                    effortBySprint,
                    averageEffort,
                    standardDeviation,
                    DetermineEffortTrendDirectionFromSlope(trendSlope, standardDeviation, averageEffort),
                    trendSlope);
            })
            .ToList();
    }

    private static (EffortForecastDirection Direction, double TrendSlope) CalculateOverallTrend(
        IReadOnlyList<EffortSprintTrend> sprintTrends)
    {
        if (sprintTrends.Count < 2)
        {
            return (EffortForecastDirection.Stable, 0);
        }

        var efforts = sprintTrends.Select(static trend => trend.TotalEffort).ToList();
        var trendSlope = CalculateLinearRegressionSlope(efforts);
        var averageEffort = efforts.Average();
        var standardDeviation = CalculateStandardDeviation(efforts);

        return (DetermineEffortTrendDirectionFromSlope(trendSlope, standardDeviation, averageEffort), trendSlope);
    }

    private static IReadOnlyList<EffortDistributionForecast> GenerateForecasts(IReadOnlyList<EffortSprintTrend> historicalTrends)
    {
        if (historicalTrends.Count < 2)
        {
            return Array.Empty<EffortDistributionForecast>();
        }

        var efforts = historicalTrends.Select(static trend => trend.TotalEffort).ToList();
        var averageEffort = efforts.Average();
        var standardDeviation = CalculateStandardDeviation(efforts);
        var trendSlope = CalculateLinearRegressionSlope(efforts);
        var forecasts = new List<EffortDistributionForecast>(3);

        for (var sprintOffset = 1; sprintOffset <= 3; sprintOffset++)
        {
            var forecastedEffort = (int)(averageEffort + trendSlope * sprintOffset);
            var confidenceMargin = (int)(2 * standardDeviation);
            var lowEstimate = Math.Max(0, forecastedEffort - confidenceMargin);
            var highEstimate = forecastedEffort + confidenceMargin;
            var coefficientOfVariation = averageEffort > 0 ? standardDeviation / averageEffort : 1;

            var confidenceLevel = coefficientOfVariation switch
            {
                < 0.2 => 0.9,
                < 0.4 => 0.7,
                < 0.6 => 0.5,
                _ => 0.3
            };

            forecasts.Add(new EffortDistributionForecast(
                $"Sprint +{sprintOffset}",
                forecastedEffort,
                lowEstimate,
                highEstimate,
                confidenceLevel));
        }

        return forecasts;
    }

    private static EffortForecastDirection DetermineEffortTrendDirection(double changePercentage)
    {
        return changePercentage switch
        {
            > 15 => EffortForecastDirection.Increasing,
            < -15 => EffortForecastDirection.Decreasing,
            _ => EffortForecastDirection.Stable
        };
    }

    private static EffortForecastDirection DetermineEffortTrendDirectionFromSlope(
        double slope,
        double standardDeviation,
        double average)
    {
        if (average == 0)
        {
            return EffortForecastDirection.Stable;
        }

        var coefficientOfVariation = standardDeviation / average;
        if (coefficientOfVariation > 0.5)
        {
            return EffortForecastDirection.Volatile;
        }

        var slopePercentage = (slope / average) * 100;
        return slopePercentage switch
        {
            > 10 => EffortForecastDirection.Increasing,
            < -10 => EffortForecastDirection.Decreasing,
            _ => EffortForecastDirection.Stable
        };
    }

    private static double CalculateStandardDeviation(IReadOnlyList<int> values)
    {
        return StatisticsMath.StandardDeviation(values.Select(static value => (double)value));
    }

    private static double CalculateLinearRegressionSlope(IReadOnlyList<int> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var n = values.Count;
        var sumX = 0d;
        var sumY = 0d;
        var sumXY = 0d;
        var sumX2 = 0d;

        for (var index = 0; index < n; index++)
        {
            var x = index;
            var y = values[index];
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        return (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
    }

    private static string ExtractSprintName(string iterationPath)
    {
        var parts = iterationPath.Split('\\', '/');
        return parts.Length > 0 ? parts[^1] : iterationPath;
    }
}
