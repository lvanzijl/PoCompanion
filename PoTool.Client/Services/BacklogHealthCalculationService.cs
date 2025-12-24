using PoTool.Client.ApiClient;
using PoTool.Core.Health;
using MudBlazor;

namespace PoTool.Client.Services;

/// <summary>
/// UI service for backlog health visualization.
/// Business logic is delegated to Core layer.
/// </summary>
public class BacklogHealthCalculationService
{
    private readonly BacklogHealthCalculator _calculator;

    public BacklogHealthCalculationService(BacklogHealthCalculator calculator)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    /// <summary>
    /// Calculates the health score for an iteration based on validation issues.
    /// </summary>
    /// <param name="iteration">Iteration health data.</param>
    /// <returns>Health score from 0 to 100.</returns>
    public int CalculateHealthScore(BacklogHealthDto iteration)
    {
        return _calculator.CalculateHealthScore(
            iteration.TotalWorkItems,
            iteration.WorkItemsWithoutEffort,
            iteration.WorkItemsInProgressWithoutEffort,
            iteration.ParentProgressIssues,
            iteration.BlockedItems
        );
    }

    /// <summary>
    /// Determines the color associated with a trend direction.
    /// </summary>
    /// <param name="trend">Trend direction.</param>
    /// <returns>MudBlazor color.</returns>
    public Color GetTrendColor(TrendDirection trend)
    {
        return trend switch
        {
            TrendDirection.Improving => Color.Success,
            TrendDirection.Stable => Color.Info,
            TrendDirection.Degrading => Color.Error,
            _ => Color.Default
        };
    }

    /// <summary>
    /// Determines the icon associated with a trend direction.
    /// </summary>
    /// <param name="trend">Trend direction.</param>
    /// <returns>Material icon string.</returns>
    public string GetTrendIcon(TrendDirection trend)
    {
        return trend switch
        {
            TrendDirection.Improving => Icons.Material.Filled.TrendingUp,
            TrendDirection.Stable => Icons.Material.Filled.TrendingFlat,
            TrendDirection.Degrading => Icons.Material.Filled.TrendingDown,
            _ => Icons.Material.Filled.HelpOutline
        };
    }

    /// <summary>
    /// Determines the color associated with a validation severity.
    /// </summary>
    /// <param name="severity">Severity string.</param>
    /// <returns>MudBlazor color.</returns>
    public Color GetSeverityColor(string severity)
    {
        return severity.ToLower() switch
        {
            "error" => Color.Error,
            "warning" => Color.Warning,
            _ => Color.Info
        };
    }

    /// <summary>
    /// Generates chart series data for comparing issues across iterations.
    /// </summary>
    /// <param name="healthData">Multi-iteration health data.</param>
    /// <returns>List of chart series.</returns>
    public List<ChartSeries> GenerateComparisonChartData(MultiIterationBacklogHealthDto healthData)
    {
        return new List<ChartSeries>
        {
            new ChartSeries
            {
                Name = "Without Effort",
                Data = healthData.IterationHealth.Select(i => (double)i.WorkItemsWithoutEffort).ToArray()
            },
            new ChartSeries
            {
                Name = "Parent Issues",
                Data = healthData.IterationHealth.Select(i => (double)i.ParentProgressIssues).ToArray()
            },
            new ChartSeries
            {
                Name = "Blocked",
                Data = healthData.IterationHealth.Select(i => (double)i.BlockedItems).ToArray()
            }
        };
    }

    /// <summary>
    /// Extracts iteration labels for chart X-axis.
    /// </summary>
    /// <param name="healthData">Multi-iteration health data.</param>
    /// <returns>Array of iteration names.</returns>
    public string[] GetIterationLabels(MultiIterationBacklogHealthDto healthData)
    {
        return healthData.IterationHealth
            .Select(i => i.SprintName)
            .ToArray();
    }
}
