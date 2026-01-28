using PoTool.Shared.PullRequests;

namespace PoTool.Client.Services;

/// <summary>
/// Service for calculating Pull Request insights metrics.
/// Pure functions for metric calculations based on PR data.
/// </summary>
public class PullRequestInsightsCalculator
{
    /// <summary>
    /// Calculates lead time to merge metric (creation to merge).
    /// </summary>
    /// <param name="prs">Pull requests to analyze.</param>
    /// <param name="metrics">PR metrics with file and iteration data.</param>
    /// <returns>Metric with Median, P75, and count.</returns>
    public MetricResult CalculateLeadTimeToMerge(
        IEnumerable<PullRequestDto> prs,
        IEnumerable<PullRequestMetricsDto> metrics)
    {
        // Filter to completed/merged PRs only
        var completedPrs = prs.Where(pr => 
            pr.CompletedDate.HasValue && 
            pr.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (completedPrs.Count == 0)
        {
            return new MetricResult(null, null, 0, null);
        }

        var leadTimes = completedPrs
            .Select(pr => (pr.CompletedDate!.Value - pr.CreatedDate).TotalHours)
            .OrderBy(x => x)
            .ToList();

        return new MetricResult(
            Median: CalculateMedian(leadTimes),
            P75: CalculatePercentile(leadTimes, 75),
            Count: leadTimes.Count,
            Coverage: null
        );
    }

    /// <summary>
    /// Calculates cycle time metric (first commit to merge).
    /// Falls back to creation time if first commit time unavailable.
    /// </summary>
    public MetricResult CalculateCycleTime(
        IEnumerable<PullRequestDto> prs,
        IEnumerable<PullRequestMetricsDto> metrics)
    {
        var completedPrs = prs.Where(pr => 
            pr.CompletedDate.HasValue && 
            pr.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (completedPrs.Count == 0)
        {
            return new MetricResult(null, null, 0, null);
        }

        // Note: First commit time not available in current data model
        // Falling back to creation time as documented
        var cycleTimes = completedPrs
            .Select(pr => (pr.CompletedDate!.Value - pr.CreatedDate).TotalHours)
            .OrderBy(x => x)
            .ToList();

        return new MetricResult(
            Median: CalculateMedian(cycleTimes),
            P75: CalculatePercentile(cycleTimes, 75),
            Count: cycleTimes.Count,
            Coverage: null
        );
    }

    /// <summary>
    /// Calculates time to first review metric.
    /// </summary>
    public MetricResult CalculateTimeToFirstReview(
        IEnumerable<PullRequestDto> prs,
        IEnumerable<PullRequestMetricsDto> metrics,
        Dictionary<int, List<PullRequestCommentDto>> prComments)
    {
        var prsList = prs.ToList();
        var prsWithReviews = new List<double>();

        foreach (var pr in prsList)
        {
            if (prComments.TryGetValue(pr.Id, out var comments) && comments.Count > 0)
            {
                var firstReviewTime = comments.Min(c => c.CreatedDate);
                var timeToFirstReview = (firstReviewTime - pr.CreatedDate).TotalHours;
                prsWithReviews.Add(timeToFirstReview);
            }
        }

        if (prsWithReviews.Count == 0)
        {
            return new MetricResult(null, null, 0, 0.0);
        }

        var coverage = prsList.Count > 0 ? (double)prsWithReviews.Count / prsList.Count * 100 : 0.0;

        var orderedTimes = prsWithReviews.OrderBy(x => x).ToList();

        return new MetricResult(
            Median: CalculateMedian(orderedTimes),
            P75: CalculatePercentile(orderedTimes, 75),
            Count: orderedTimes.Count,
            Coverage: coverage
        );
    }

    /// <summary>
    /// Calculates review duration metric (first review event to last review event).
    /// </summary>
    public MetricResult CalculateReviewDuration(
        IEnumerable<PullRequestDto> prs,
        IEnumerable<PullRequestMetricsDto> metrics,
        Dictionary<int, List<PullRequestCommentDto>> prComments)
    {
        var prsList = prs.ToList();
        var reviewDurations = new List<double>();

        foreach (var pr in prsList)
        {
            if (prComments.TryGetValue(pr.Id, out var comments) && comments.Count >= 2)
            {
                var firstReview = comments.Min(c => c.CreatedDate);
                var lastReview = comments.Max(c => c.CreatedDate);
                var duration = (lastReview - firstReview).TotalHours;
                reviewDurations.Add(duration);
            }
        }

        if (reviewDurations.Count == 0)
        {
            return new MetricResult(null, null, 0, 0.0);
        }

        var coverage = prsList.Count > 0 ? (double)reviewDurations.Count / prsList.Count * 100 : 0.0;

        var orderedDurations = reviewDurations.OrderBy(x => x).ToList();

        return new MetricResult(
            Median: CalculateMedian(orderedDurations),
            P75: CalculatePercentile(orderedDurations, 75),
            Count: orderedDurations.Count,
            Coverage: coverage
        );
    }

    /// <summary>
    /// Calculates PR size metrics (lines changed and files changed).
    /// </summary>
    public (MetricResult LinesChanged, MetricResult FilesChanged) CalculatePRSize(
        IEnumerable<PullRequestDto> prs,
        IEnumerable<PullRequestMetricsDto> metrics)
    {
        var metricsList = metrics.ToList();

        if (metricsList.Count == 0)
        {
            var emptyResult = new MetricResult(null, null, 0, null);
            return (emptyResult, emptyResult);
        }

        // Lines changed
        var linesChanged = metricsList
            .Select(m => (double)(m.TotalLinesAdded + m.TotalLinesDeleted))
            .OrderBy(x => x)
            .ToList();

        var linesResult = new MetricResult(
            Median: CalculateMedian(linesChanged),
            P75: CalculatePercentile(linesChanged, 75),
            Count: linesChanged.Count,
            Coverage: null
        );

        // Files changed
        var filesChanged = metricsList
            .Select(m => (double)m.TotalFileCount)
            .OrderBy(x => x)
            .ToList();

        var filesResult = new MetricResult(
            Median: CalculateMedian(filesChanged),
            P75: CalculatePercentile(filesChanged, 75),
            Count: filesChanged.Count,
            Coverage: null
        );

        return (linesResult, filesResult);
    }

    /// <summary>
    /// Calculates rework rate (commits after first review).
    /// </summary>
    public MetricResult CalculateReworkRate(
        IEnumerable<PullRequestDto> prs,
        IEnumerable<PullRequestMetricsDto> metrics,
        Dictionary<int, List<PullRequestCommentDto>> prComments,
        Dictionary<int, List<PullRequestIterationDto>> prIterations)
    {
        var prsList = prs.ToList();
        var postReviewCommits = new List<double>();

        foreach (var pr in prsList)
        {
            // Need both comments (for review timing) and iterations (for commits)
            if (!prComments.TryGetValue(pr.Id, out var comments) || comments.Count == 0)
                continue;

            if (!prIterations.TryGetValue(pr.Id, out var iterations) || iterations.Count == 0)
                continue;

            var firstReviewTime = comments.Min(c => c.CreatedDate);
            
            // Count iterations created after first review
            var postReviewIterations = iterations
                .Where(it => it.CreatedDate > firstReviewTime)
                .ToList();

            var totalPostReviewCommits = postReviewIterations.Sum(it => it.CommitCount);
            postReviewCommits.Add(totalPostReviewCommits);
        }

        if (postReviewCommits.Count == 0)
        {
            return new MetricResult(null, null, 0, 0.0);
        }

        var coverage = prsList.Count > 0 ? (double)postReviewCommits.Count / prsList.Count * 100 : 0.0;

        var orderedCommits = postReviewCommits.OrderBy(x => x).ToList();

        return new MetricResult(
            Median: CalculateMedian(orderedCommits),
            P75: CalculatePercentile(orderedCommits, 75),
            Count: orderedCommits.Count,
            Coverage: coverage
        );
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

/// <summary>
/// Result of a metric calculation.
/// </summary>
/// <param name="Median">Median value (in hours for time metrics, count for size metrics).</param>
/// <param name="P75">75th percentile value.</param>
/// <param name="Count">Number of PRs included in calculation.</param>
/// <param name="Coverage">Coverage percentage (% of PRs with this data), null if not applicable.</param>
public record MetricResult(
    double? Median,
    double? P75,
    int Count,
    double? Coverage
);
