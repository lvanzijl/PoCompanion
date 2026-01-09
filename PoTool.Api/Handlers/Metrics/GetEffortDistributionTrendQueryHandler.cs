using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetEffortDistributionTrendQuery.
/// Analyzes how effort distribution changes over time and forecasts future trends.
/// </summary>
public sealed class GetEffortDistributionTrendQueryHandler 
    : IQueryHandler<GetEffortDistributionTrendQuery, EffortDistributionTrendDto>
{
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<GetEffortDistributionTrendQueryHandler> _logger;

    public GetEffortDistributionTrendQueryHandler(
        IWorkItemRepository repository,
        ILogger<GetEffortDistributionTrendQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<EffortDistributionTrendDto> Handle(
        GetEffortDistributionTrendQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetEffortDistributionTrendQuery with AreaPathFilter: {AreaPathFilter}, MaxIterations: {MaxIterations}", 
            query.AreaPathFilter ?? "All", 
            query.MaxIterations);

        var allWorkItems = await _repository.GetAllAsync(cancellationToken);
        
        // Filter by area path if specified
        if (!string.IsNullOrWhiteSpace(query.AreaPathFilter))
        {
            allWorkItems = allWorkItems
                .Where(wi => wi.AreaPath.StartsWith(query.AreaPathFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Filter to items with effort only
        var workItemsWithEffort = allWorkItems
            .Where(wi => wi.Effort.HasValue && wi.Effort.Value > 0)
            .ToList();

        if (!workItemsWithEffort.Any())
        {
            return new EffortDistributionTrendDto(
                TrendBySprint: Array.Empty<SprintTrendData>(),
                TrendByAreaPath: Array.Empty<AreaPathTrendData>(),
                OverallTrend: EffortTrendDirection.Stable,
                TrendSlope: 0,
                Forecasts: Array.Empty<DistributionForecast>(),
                AnalysisTimestamp: DateTimeOffset.UtcNow
            );
        }

        // Get recent iterations ordered chronologically
        // Note: Simple string sorting works if iteration paths follow consistent naming (e.g., "Sprint 01", "Sprint 02")
        // For more complex scenarios, consider extracting dates or numeric values for proper chronological ordering
        var iterationPaths = workItemsWithEffort
            .Where(wi => !string.IsNullOrWhiteSpace(wi.IterationPath))
            .Select(wi => wi.IterationPath)
            .Distinct()
            .OrderBy(path => path) // Chronological order - assumes consistent naming
            .Take(query.MaxIterations)
            .ToList();

        // Get top area paths
        var topAreaPaths = workItemsWithEffort
            .GroupBy(wi => wi.AreaPath)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        // Analyze sprint trends
        var sprintTrends = AnalyzeSprintTrends(
            workItemsWithEffort, 
            iterationPaths, 
            query.DefaultCapacityPerIteration);

        // Analyze area path trends
        var areaPathTrends = AnalyzeAreaPathTrends(workItemsWithEffort, topAreaPaths, iterationPaths);

        // Calculate overall trend
        var (overallTrend, trendSlope) = CalculateOverallTrend(sprintTrends);

        // Generate forecasts
        var forecasts = GenerateForecasts(sprintTrends, iterationPaths.Count);

        return new EffortDistributionTrendDto(
            TrendBySprint: sprintTrends,
            TrendByAreaPath: areaPathTrends,
            OverallTrend: overallTrend,
            TrendSlope: trendSlope,
            Forecasts: forecasts,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private static List<SprintTrendData> AnalyzeSprintTrends(
        List<WorkItemDto> workItems,
        List<string> iterationPaths,
        int? defaultCapacity)
    {
        var trends = new List<SprintTrendData>();
        int? previousEffort = null;

        foreach (var iterationPath in iterationPaths)
        {
            var itemsInSprint = workItems
                .Where(wi => wi.IterationPath.Equals(iterationPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var totalEffort = itemsInSprint.Sum(wi => wi.Effort ?? 0);
            var workItemCount = itemsInSprint.Count;
            var utilization = defaultCapacity.HasValue && defaultCapacity.Value > 0
                ? (double)totalEffort / defaultCapacity.Value * 100
                : 0;

            var changeFromPrevious = previousEffort.HasValue
                ? ((double)(totalEffort - previousEffort.Value) / (previousEffort.Value > 0 ? previousEffort.Value : 1)) * 100
                : 0;

            var direction = DetermineEffortTrendDirection(changeFromPrevious);

            trends.Add(new SprintTrendData(
                IterationPath: iterationPath,
                SprintName: ExtractSprintName(iterationPath),
                TotalEffort: totalEffort,
                WorkItemCount: workItemCount,
                UtilizationPercentage: utilization,
                ChangeFromPrevious: changeFromPrevious,
                Direction: direction
            ));

            previousEffort = totalEffort;
        }

        return trends;
    }

    private static List<AreaPathTrendData> AnalyzeAreaPathTrends(
        List<WorkItemDto> workItems,
        List<string> areaPaths,
        List<string> iterationPaths)
    {
        return areaPaths
            .Select(areaPath =>
            {
                var effortBySprint = iterationPaths
                    .Select(iterationPath => workItems
                        .Where(wi => wi.AreaPath.Equals(areaPath, StringComparison.OrdinalIgnoreCase) &&
                                    wi.IterationPath.Equals(iterationPath, StringComparison.OrdinalIgnoreCase))
                        .Sum(wi => wi.Effort ?? 0))
                    .ToList();

                var avgEffort = effortBySprint.Any() ? effortBySprint.Average() : 0;
                var stdDev = CalculateStandardDeviation(effortBySprint);
                var slope = CalculateLinearRegressionSlope(effortBySprint);
                var direction = DetermineEffortTrendDirectionFromSlope(slope, stdDev, avgEffort);

                return new AreaPathTrendData(
                    AreaPath: areaPath,
                    EffortBySprint: effortBySprint,
                    AverageEffort: avgEffort,
                    StandardDeviation: stdDev,
                    Direction: direction,
                    TrendSlope: slope
                );
            })
            .ToList();
    }

    private static (EffortTrendDirection, double) CalculateOverallTrend(List<SprintTrendData> sprintTrends)
    {
        if (sprintTrends.Count < 2)
        {
            return (EffortTrendDirection.Stable, 0);
        }

        var efforts = sprintTrends.Select(s => s.TotalEffort).ToList();
        var slope = CalculateLinearRegressionSlope(efforts);
        var avgEffort = efforts.Average();
        var stdDev = CalculateStandardDeviation(efforts);

        var direction = DetermineEffortTrendDirectionFromSlope(slope, stdDev, avgEffort);

        return (direction, slope);
    }

    private static List<DistributionForecast> GenerateForecasts(
        List<SprintTrendData> historicalTrends,
        int historicalSprintCount)
    {
        if (historicalTrends.Count < 2)
        {
            return new List<DistributionForecast>();
        }

        var forecasts = new List<DistributionForecast>();
        var efforts = historicalTrends.Select(s => s.TotalEffort).ToList();
        var avgEffort = efforts.Average();
        var stdDev = CalculateStandardDeviation(efforts);
        var slope = CalculateLinearRegressionSlope(efforts);

        // Forecast next 3 sprints
        for (int i = 1; i <= 3; i++)
        {
            // Linear projection with trend - use forecast position (i), not cumulative sprint count
            var forecastedEffort = (int)(avgEffort + slope * i);
            
            // Confidence interval (assuming 95% confidence ~ 2 standard deviations)
            var confidenceMargin = (int)(2 * stdDev);
            var lowEstimate = Math.Max(0, forecastedEffort - confidenceMargin);
            var highEstimate = forecastedEffort + confidenceMargin;

            // Confidence level based on historical variance
            var coefficientOfVariation = avgEffort > 0 ? stdDev / avgEffort : 1;
            var confidenceLevel = coefficientOfVariation switch
            {
                < 0.2 => 0.9,
                < 0.4 => 0.7,
                < 0.6 => 0.5,
                _ => 0.3
            };

            forecasts.Add(new DistributionForecast(
                SprintName: $"Sprint +{i}",
                ForecastedEffort: forecastedEffort,
                LowEstimate: lowEstimate,
                HighEstimate: highEstimate,
                ConfidenceLevel: confidenceLevel
            ));
        }

        return forecasts;
    }

    private static EffortTrendDirection DetermineEffortTrendDirection(double changePercentage)
    {
        return changePercentage switch
        {
            > 15 => EffortTrendDirection.Increasing,
            < -15 => EffortTrendDirection.Decreasing,
            _ => EffortTrendDirection.Stable
        };
    }

    private static EffortTrendDirection DetermineEffortTrendDirectionFromSlope(
        double slope,
        double standardDeviation,
        double average)
    {
        if (average == 0)
        {
            return EffortTrendDirection.Stable;
        }

        // High variance relative to average indicates volatility
        var coefficientOfVariation = standardDeviation / average;
        if (coefficientOfVariation > 0.5)
        {
            return EffortTrendDirection.Volatile;
        }

        // Slope as percentage of average
        var slopePercentage = (slope / average) * 100;

        return slopePercentage switch
        {
            > 10 => EffortTrendDirection.Increasing,
            < -10 => EffortTrendDirection.Decreasing,
            _ => EffortTrendDirection.Stable
        };
    }

    private static double CalculateStandardDeviation(List<int> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }

    private static double CalculateLinearRegressionSlope(List<int> values)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var n = values.Count;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;

        for (int i = 0; i < n; i++)
        {
            var x = i;
            var y = values[i];
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        return slope;
    }

    private static string ExtractSprintName(string iterationPath)
    {
        var parts = iterationPath.Split('\\', '/');
        return parts.Length > 0 ? parts[^1] : iterationPath;
    }
}
