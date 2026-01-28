using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for calculating Pipeline insights metrics.
/// Pure functions for metric calculations based on pipeline run data.
/// </summary>
public class PipelineInsightsCalculator
{
    /// <summary>
    /// Calculates build success rate metric.
    /// </summary>
    /// <param name="runs">Pipeline runs to analyze.</param>
    /// <returns>Metric with success rate as percentage.</returns>
    public MetricResult CalculateBuildSuccessRate(IEnumerable<PipelineRunDto> runs)
    {
        var completedRuns = runs.Where(r => 
            r.Result != PipelineRunResult.Unknown && 
            r.Result != PipelineRunResult.None)
            .ToList();

        if (completedRuns.Count == 0)
        {
            return new MetricResult(null, null, 0, null);
        }

        var successfulRuns = completedRuns.Count(r => r.Result == PipelineRunResult.Succeeded);
        var successRate = (double)successfulRuns / completedRuns.Count * 100;

        return new MetricResult(
            Median: successRate,
            P75: null,
            Count: completedRuns.Count,
            Coverage: null
        );
    }

    /// <summary>
    /// Calculates build failure rate metric.
    /// </summary>
    /// <param name="runs">Pipeline runs to analyze.</param>
    /// <returns>Metric with failure rate as percentage.</returns>
    public MetricResult CalculateBuildFailureRate(IEnumerable<PipelineRunDto> runs)
    {
        var completedRuns = runs.Where(r => 
            r.Result != PipelineRunResult.Unknown && 
            r.Result != PipelineRunResult.None)
            .ToList();

        if (completedRuns.Count == 0)
        {
            return new MetricResult(null, null, 0, null);
        }

        var failedRuns = completedRuns.Count(r => 
            r.Result == PipelineRunResult.Failed || 
            r.Result == PipelineRunResult.PartiallySucceeded);
        var failureRate = (double)failedRuns / completedRuns.Count * 100;

        return new MetricResult(
            Median: failureRate,
            P75: null,
            Count: completedRuns.Count,
            Coverage: null
        );
    }

    /// <summary>
    /// Calculates Mean Time To Repair (MTTR) metric.
    /// Time between a failed pipeline run and the next successful run on the same pipeline.
    /// </summary>
    /// <param name="runs">Pipeline runs to analyze (must be ordered by time).</param>
    /// <returns>Metric with Median and P75 MTTR in hours.</returns>
    public MetricResult CalculateMTTR(IEnumerable<PipelineRunDto> runs)
    {
        var runsList = runs
            .Where(r => r.StartTime.HasValue)
            .OrderBy(r => r.StartTime!.Value)
            .ToList();

        var mttrValues = new List<double>();

        // Group by pipeline to calculate MTTR per pipeline
        var groupedByPipeline = runsList.GroupBy(r => r.PipelineId);

        foreach (var pipelineRuns in groupedByPipeline)
        {
            var orderedRuns = pipelineRuns.OrderBy(r => r.StartTime!.Value).ToList();

            for (int i = 0; i < orderedRuns.Count - 1; i++)
            {
                var currentRun = orderedRuns[i];
                
                // Check if current run failed
                if (currentRun.Result == PipelineRunResult.Failed || 
                    currentRun.Result == PipelineRunResult.PartiallySucceeded)
                {
                    // Find next successful run
                    for (int j = i + 1; j < orderedRuns.Count; j++)
                    {
                        var nextRun = orderedRuns[j];
                        if (nextRun.Result == PipelineRunResult.Succeeded && 
                            nextRun.StartTime.HasValue && 
                            currentRun.StartTime.HasValue)
                        {
                            var mttr = (nextRun.StartTime.Value - currentRun.StartTime.Value).TotalHours;
                            mttrValues.Add(mttr);
                            break;
                        }
                    }
                }
            }
        }

        if (mttrValues.Count == 0)
        {
            return new MetricResult(null, null, 0, null);
        }

        var sortedMttrs = mttrValues.OrderBy(x => x).ToList();

        return new MetricResult(
            Median: CalculateMedian(sortedMttrs),
            P75: CalculatePercentile(sortedMttrs, 75),
            Count: sortedMttrs.Count,
            Coverage: null
        );
    }

    /// <summary>
    /// Calculates pipeline duration metric.
    /// </summary>
    /// <param name="runs">Pipeline runs to analyze.</param>
    /// <returns>Metric with Median and P75 duration in hours.</returns>
    public MetricResult CalculatePipelineDuration(IEnumerable<PipelineRunDto> runs)
    {
        var durations = runs
            .Where(r => r.Duration.HasValue && r.Duration.Value.TotalSeconds > 0)
            .Select(r => r.Duration!.Value.TotalHours)
            .OrderBy(x => x)
            .ToList();

        if (durations.Count == 0)
        {
            return new MetricResult(null, null, 0, null);
        }

        return new MetricResult(
            Median: CalculateMedian(durations),
            P75: CalculatePercentile(durations, 75),
            Count: durations.Count,
            Coverage: null
        );
    }

    /// <summary>
    /// Calculates time to first failure detection metric.
    /// Time from run start to failure time for failed runs.
    /// </summary>
    /// <param name="runs">Pipeline runs to analyze.</param>
    /// <returns>Metric with Median and P75 time to failure in hours.</returns>
    public MetricResult CalculateTimeToFirstFailureDetection(IEnumerable<PipelineRunDto> runs)
    {
        var failureTimes = runs
            .Where(r => 
                (r.Result == PipelineRunResult.Failed || r.Result == PipelineRunResult.PartiallySucceeded) &&
                r.StartTime.HasValue && 
                r.FinishTime.HasValue)
            .Select(r => (r.FinishTime!.Value - r.StartTime!.Value).TotalHours)
            .OrderBy(x => x)
            .ToList();

        if (failureTimes.Count == 0)
        {
            return new MetricResult(null, null, 0, null);
        }

        return new MetricResult(
            Median: CalculateMedian(failureTimes),
            P75: CalculatePercentile(failureTimes, 75),
            Count: failureTimes.Count,
            Coverage: null
        );
    }

    /// <summary>
    /// Calculates flakiness rate metric.
    /// Pipelines that have both failures and successes in the time window.
    /// </summary>
    /// <param name="runs">Pipeline runs to analyze.</param>
    /// <returns>Metric with flakiness rate as percentage.</returns>
    public MetricResult CalculateFlakinessRate(IEnumerable<PipelineRunDto> runs)
    {
        var completedRuns = runs
            .Where(r => r.Result != PipelineRunResult.Unknown && r.Result != PipelineRunResult.None)
            .ToList();

        if (completedRuns.Count == 0)
        {
            return new MetricResult(null, null, 0, null);
        }

        // Group by pipeline
        var groupedByPipeline = completedRuns.GroupBy(r => r.PipelineId).ToList();
        var totalPipelines = groupedByPipeline.Count;

        // Count pipelines with both successes and failures
        var flakyPipelines = groupedByPipeline.Count(pipelineRuns =>
        {
            var hasSuccess = pipelineRuns.Any(r => r.Result == PipelineRunResult.Succeeded);
            var hasFailure = pipelineRuns.Any(r => 
                r.Result == PipelineRunResult.Failed || 
                r.Result == PipelineRunResult.PartiallySucceeded);
            return hasSuccess && hasFailure;
        });

        var flakinessRate = totalPipelines > 0 ? (double)flakyPipelines / totalPipelines * 100 : 0;

        return new MetricResult(
            Median: flakinessRate,
            P75: null,
            Count: flakyPipelines,
            Coverage: totalPipelines > 0 ? flakinessRate : null
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
