using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for calculating pull request metrics and generating chart data.
/// </summary>
public class PullRequestMetricsService
{
    /// <summary>
    /// Calculates the average time PRs have been open.
    /// </summary>
    /// <param name="metrics">Pull request metrics.</param>
    /// <returns>Formatted string like "3.5d".</returns>
    public string CalculateAverageTimeOpen(IEnumerable<PullRequestMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        if (!metricsList.Any()) return "0d";
        var avgDays = metricsList.Average(m => m.TotalTimeOpen.TotalDays);
        return $"{avgDays:F1}d";
    }

    /// <summary>
    /// Calculates the average number of iterations per PR.
    /// </summary>
    /// <param name="metrics">Pull request metrics.</param>
    /// <returns>Formatted string like "2.1".</returns>
    public string CalculateAverageIterations(IEnumerable<PullRequestMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        if (!metricsList.Any()) return "0";
        var avg = metricsList.Average(m => m.IterationCount);
        return avg.ToString("F1");
    }

    /// <summary>
    /// Calculates the average number of files changed per PR.
    /// </summary>
    /// <param name="metrics">Pull request metrics.</param>
    /// <returns>Formatted string like "15".</returns>
    public string CalculateAverageFiles(IEnumerable<PullRequestMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        if (!metricsList.Any()) return "0";
        var avg = metricsList.Average(m => m.TotalFileCount);
        return avg.ToString("F0");
    }

    /// <summary>
    /// Filters metrics by date range.
    /// </summary>
    /// <param name="metrics">Pull request metrics to filter.</param>
    /// <param name="startDate">Optional start date.</param>
    /// <param name="endDate">Optional end date.</param>
    /// <returns>Filtered metrics.</returns>
    public IEnumerable<PullRequestMetricsDto> FilterByDateRange(
        IEnumerable<PullRequestMetricsDto> metrics,
        DateTime? startDate,
        DateTime? endDate)
    {
        return metrics.Where(m =>
        {
            var createdDate = m.CreatedDate;

            if (startDate.HasValue && createdDate < startDate.Value)
                return false;

            if (endDate.HasValue && createdDate > endDate.Value)
                return false;

            return true;
        });
    }

    /// <summary>
    /// Generates chart data for PR status distribution.
    /// </summary>
    /// <param name="metrics">Pull request metrics.</param>
    /// <returns>Data values and labels for chart.</returns>
    public (double[] data, string[] labels) GetStatusChartData(IEnumerable<PullRequestMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        if (!metricsList.Any())
            return (new[] { 0.0 }, new[] { "No data" });

        var grouped = metricsList.GroupBy(m => m.Status).ToList();
        var data = grouped.Select(g => (double)g.Count()).ToArray();
        var labels = grouped.Select(g => g.Key).ToArray();

        return (data, labels);
    }

    /// <summary>
    /// Generates chart data for time open distribution (top 10).
    /// </summary>
    /// <param name="metrics">Pull request metrics.</param>
    /// <returns>Data values and labels for chart.</returns>
    public (double[] data, string[] labels) GetTimeOpenChartData(IEnumerable<PullRequestMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        var topItems = metricsList
            .OrderByDescending(m => m.TotalTimeOpen)
            .Take(10)
            .ToList();

        var data = topItems.Select(m => m.TotalTimeOpen.TotalDays).ToArray();
        var labels = topItems.Select(m => m.Title.Length > 30 ? m.Title[0..27] + "..." : m.Title).ToArray();

        return (data, labels);
    }

    /// <summary>
    /// Generates chart data for PRs by user.
    /// </summary>
    /// <param name="metrics">Pull request metrics.</param>
    /// <returns>Data values and labels for chart.</returns>
    public (double[] data, string[] labels) GetPRsByUserChartData(IEnumerable<PullRequestMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        var grouped = metricsList.GroupBy(m => m.CreatedBy).ToList();
        var data = grouped.Select(g => (double)g.Count()).ToArray();
        var labels = grouped.Select(g => g.Key).ToArray();

        return (data, labels);
    }

    /// <summary>
    /// Calculates a hash code for metrics collection to support cache invalidation.
    /// </summary>
    /// <param name="metrics">Pull request metrics.</param>
    /// <returns>Hash code representing the collection state.</returns>
    public int CalculateMetricsHashCode(IEnumerable<PullRequestMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();
        if (metricsList.Count == 0)
            return 0;

        var hash = new HashCode();
        hash.Add(metricsList.Count);
        // Sample multiple items across the collection for better change detection
        hash.Add(metricsList[0].Title);
        if (metricsList.Count > 1)
            hash.Add(metricsList[metricsList.Count - 1].Title);
        if (metricsList.Count > 2)
            hash.Add(metricsList[metricsList.Count / 2].Title);
        // Add aggregate values for additional validation
        hash.Add(metricsList.Sum(m => m.TotalTimeOpen.TotalDays));
        hash.Add(metricsList.Sum(m => m.IterationCount));
        return hash.ToHashCode();
    }
}
