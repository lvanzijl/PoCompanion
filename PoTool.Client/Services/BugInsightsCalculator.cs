using PoTool.Client.ApiClient;
using System.Text.Json;

namespace PoTool.Client.Services;

/// <summary>
/// Service for calculating Bug insights metrics.
/// Pure functions for metric calculations based on work item data.
/// </summary>
public class BugInsightsCalculator
{
    /// <summary>
    /// Calculates total count of open bugs (bugs not in terminal state).
    /// </summary>
    /// <param name="bugs">Bug work items to analyze.</param>
    /// <returns>Metric with total count of open bugs.</returns>
    public MetricResult CalculateTotalOpenBugs(IEnumerable<WorkItemDto> bugs)
    {
        var bugsList = bugs.ToList();
        
        if (bugsList.Count == 0)
        {
            return new MetricResult(0, null, 0, null);
        }

        // Count bugs that are NOT in terminal state (Done/Closed/Removed)
        var openBugs = bugsList.Count(b => !IsTerminalState(b.State));

        return new MetricResult(
            Median: openBugs,
            P75: null,
            Count: bugsList.Count,
            Coverage: null
        );
    }

    /// <summary>
    /// Calculates count of bugs created within the specified time window.
    /// </summary>
    /// <param name="bugs">Bug work items to analyze.</param>
    /// <param name="fromDate">Start of time window.</param>
    /// <param name="toDate">End of time window (default: now).</param>
    /// <returns>Metric with count of bugs created in period.</returns>
    public MetricResult CalculateBugsCreatedPerPeriod(
        IEnumerable<WorkItemDto> bugs,
        DateTimeOffset fromDate,
        DateTimeOffset? toDate = null)
    {
        var endDate = toDate ?? DateTimeOffset.UtcNow;
        var bugsList = bugs.ToList();

        if (bugsList.Count == 0)
        {
            return new MetricResult(0, null, 0, null);
        }

        // Count bugs created within time window
        var bugsCreated = bugsList.Count(b => 
            b.CreatedDate.HasValue && 
            b.CreatedDate.Value >= fromDate && 
            b.CreatedDate.Value <= endDate);

        return new MetricResult(
            Median: bugsCreated,
            P75: null,
            Count: bugsList.Count,
            Coverage: null
        );
    }

    /// <summary>
    /// Calculates count of bugs resolved (transitioned to terminal state) within the specified time window.
    /// </summary>
    /// <param name="bugs">Bug work items to analyze.</param>
    /// <param name="fromDate">Start of time window.</param>
    /// <param name="toDate">End of time window (default: now).</param>
    /// <returns>Metric with count of bugs resolved in period.</returns>
    public MetricResult CalculateBugsResolvedPerPeriod(
        IEnumerable<WorkItemDto> bugs,
        DateTimeOffset fromDate,
        DateTimeOffset? toDate = null)
    {
        var endDate = toDate ?? DateTimeOffset.UtcNow;
        var bugsList = bugs.ToList();

        if (bugsList.Count == 0)
        {
            return new MetricResult(0, null, 0, null);
        }

        // Count bugs that transitioned to terminal state within time window
        var bugsResolved = bugsList.Count(b => 
            b.ClosedDate.HasValue && 
            b.ClosedDate.Value >= fromDate && 
            b.ClosedDate.Value <= endDate);

        return new MetricResult(
            Median: bugsResolved,
            P75: null,
            Count: bugsList.Count,
            Coverage: null
        );
    }

    /// <summary>
    /// Calculates bug resolution time (lead time to close).
    /// </summary>
    /// <param name="bugs">Bug work items to analyze.</param>
    /// <returns>Metric with Median and P75 resolution time in hours.</returns>
    public MetricResult CalculateBugResolutionTime(IEnumerable<WorkItemDto> bugs)
    {
        var bugsList = bugs.ToList();

        if (bugsList.Count == 0)
        {
            return new MetricResult(null, null, 0, null);
        }

        // Calculate resolution time for bugs that have both created and closed dates
        var resolutionTimes = bugsList
            .Where(b => b.CreatedDate.HasValue && b.ClosedDate.HasValue)
            .Select(b => (b.ClosedDate!.Value - b.CreatedDate!.Value).TotalHours)
            .OrderBy(x => x)
            .ToList();

        if (resolutionTimes.Count == 0)
        {
            return new MetricResult(null, null, 0, 0.0);
        }

        var coverage = bugsList.Count > 0 ? (double)resolutionTimes.Count / bugsList.Count * 100 : 0.0;

        return new MetricResult(
            Median: CalculateMedian(resolutionTimes),
            P75: CalculatePercentile(resolutionTimes, 75),
            Count: resolutionTimes.Count,
            Coverage: coverage
        );
    }

    /// <summary>
    /// Calculates bugs by severity distribution.
    /// </summary>
    /// <param name="bugs">Bug work items to analyze.</param>
    /// <returns>Dictionary with severity as key and (count, percentage) as value.</returns>
    public Dictionary<string, (int Count, double Percentage)> CalculateBugsBySeverityDistribution(
        IEnumerable<WorkItemDto> bugs)
    {
        var bugsList = bugs.ToList();
        var result = new Dictionary<string, (int Count, double Percentage)>();

        if (bugsList.Count == 0)
        {
            return result;
        }

        // Extract severity from each bug
        var severityCounts = bugsList
            .GroupBy(b => ExtractSeverity(b))
            .OrderBy(g => GetSeverityOrder(g.Key))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var count = g.Count();
                    var percentage = (double)count / bugsList.Count * 100;
                    return (count, percentage);
                }
            );

        return severityCounts;
    }

    /// <summary>
    /// Extracts severity from bug work item JSON payload.
    /// </summary>
    private string ExtractSeverity(WorkItemDto bug)
    {
        if (!string.IsNullOrEmpty(bug.JsonPayload))
        {
            try
            {
                using var doc = JsonDocument.Parse(bug.JsonPayload);
                if (doc.RootElement.TryGetProperty("Microsoft.VSTS.Common.Severity", out var severity))
                {
                    var severityValue = severity.GetString();
                    if (!string.IsNullOrEmpty(severityValue))
                    {
                        return severityValue;
                    }
                }
            }
            catch
            {
                // Ignore JSON parsing errors
            }
        }
        return "Unknown";
    }

    /// <summary>
    /// Gets severity order for sorting (Critical first, Unknown last).
    /// </summary>
    private int GetSeverityOrder(string severity)
    {
        return severity switch
        {
            "1 - Critical" => 1,
            "2 - High" => 2,
            "3 - Medium" => 3,
            "4 - Low" => 4,
            "Critical" => 1,
            "High" => 2,
            "Medium" => 3,
            "Low" => 4,
            _ => 999 // Unknown goes last
        };
    }

    /// <summary>
    /// Checks if a bug state is terminal (Done/Closed/Removed).
    /// </summary>
    private bool IsTerminalState(string? state)
    {
        if (string.IsNullOrEmpty(state))
            return false;

        var terminalStates = new[] { "Done", "Closed", "Removed", "Resolved" };
        return terminalStates.Any(ts => state.Equals(ts, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Calculates median of a sorted list of doubles.
    /// </summary>
    private double? CalculateMedian(List<double> sortedValues)
    {
        if (sortedValues.Count == 0)
            return null;

        int mid = sortedValues.Count / 2;
        if (sortedValues.Count % 2 == 0)
        {
            return (sortedValues[mid - 1] + sortedValues[mid]) / 2.0;
        }
        return sortedValues[mid];
    }

    /// <summary>
    /// Calculates percentile of a sorted list of doubles.
    /// </summary>
    private double? CalculatePercentile(List<double> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0)
            return null;

        double rank = (percentile / 100.0) * (sortedValues.Count - 1);
        int lowerIndex = (int)Math.Floor(rank);
        int upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        double lowerValue = sortedValues[lowerIndex];
        double upperValue = sortedValues[upperIndex];
        double fraction = rank - lowerIndex;

        return lowerValue + (upperValue - lowerValue) * fraction;
    }
}
