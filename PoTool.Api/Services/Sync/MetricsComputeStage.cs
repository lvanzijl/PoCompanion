using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that computes metrics from cached data.
/// </summary>
public class MetricsComputeStage : ISyncStage
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<MetricsComputeStage> _logger;

    public string StageName => "ComputeMetrics";
    public int StageNumber => 7;

    public MetricsComputeStage(
        PoToolDbContext context,
        ILogger<MetricsComputeStage> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback(0);

            _logger.LogInformation(
                "Starting metrics computation for ProductOwner {ProductOwnerId}",
                context.ProductOwnerId);

            var metrics = new List<(string Name, decimal Value, string? Unit)>();

            // Calculate work item metrics
            progressCallback(10);
            var workItemMetrics = await CalculateWorkItemMetricsAsync(context.ProductOwnerId, cancellationToken);
            metrics.AddRange(workItemMetrics);

            // Calculate pull request metrics
            progressCallback(40);
            var prMetrics = await CalculatePullRequestMetricsAsync(context.ProductOwnerId, cancellationToken);
            metrics.AddRange(prMetrics);

            // Calculate pipeline metrics
            progressCallback(70);
            var pipelineMetrics = await CalculatePipelineMetricsAsync(context.ProductOwnerId, cancellationToken);
            metrics.AddRange(pipelineMetrics);

            progressCallback(90);

            // Upsert metrics to database
            await UpsertMetricsAsync(context.ProductOwnerId, metrics, cancellationToken);

            progressCallback(100);

            _logger.LogInformation(
                "Successfully computed {Count} metrics for ProductOwner {ProductOwnerId}",
                metrics.Count,
                context.ProductOwnerId);

            return SyncStageResult.CreateSuccess(metrics.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Metrics compute cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metrics compute failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }

    private async Task<List<(string Name, decimal Value, string? Unit)>> CalculateWorkItemMetricsAsync(
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        var metrics = new List<(string, decimal, string?)>();

        // Get all work items
        var workItems = await _context.WorkItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Total work items
        metrics.Add(("TotalWorkItems", workItems.Count, "count"));

        // Count by type
        var byType = workItems.GroupBy(w => w.Type);
        foreach (var group in byType)
        {
            metrics.Add(($"WorkItems_{group.Key}", group.Count(), "count"));
        }

        // Count by state
        var byState = workItems.GroupBy(w => w.State);
        foreach (var group in byState)
        {
            metrics.Add(($"WorkItemsState_{group.Key.Replace(" ", "")}", group.Count(), "count"));
        }

        // Velocity (story points closed in last 7 days)
        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var closedItems = workItems
            .Where(w => w.State.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                       w.State.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            .Where(w => w.RetrievedAt >= sevenDaysAgo)
            .ToList();

        var velocity7d = closedItems.Sum(w => w.Effort ?? 0);
        metrics.Add(("Velocity7d", velocity7d, "points"));

        return metrics;
    }

    private async Task<List<(string Name, decimal Value, string? Unit)>> CalculatePullRequestMetricsAsync(
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        var metrics = new List<(string, decimal, string?)>();

        // Get products for this ProductOwner
        var productIds = await _context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        // Get PRs for these products
        var pullRequests = await _context.PullRequests
            .Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Total PRs
        metrics.Add(("TotalPullRequests", pullRequests.Count, "count"));

        // Active PRs
        var activePrs = pullRequests.Count(pr => pr.Status.Equals("active", StringComparison.OrdinalIgnoreCase));
        metrics.Add(("ActivePullRequests", activePrs, "count"));

        // Completed PRs
        var completedPrs = pullRequests.Count(pr => pr.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
        metrics.Add(("CompletedPullRequests", completedPrs, "count"));

        // PR throughput (merged in last 7 days)
        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var mergedLast7d = pullRequests
            .Count(pr => pr.CompletedDate.HasValue && pr.CompletedDate >= sevenDaysAgo &&
                        pr.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
        metrics.Add(("PrThroughput7d", mergedLast7d, "count"));

        // Average PR age (for completed PRs)
        var completedWithDates = pullRequests
            .Where(pr => pr.CompletedDate.HasValue && pr.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .Select(pr => (pr.CompletedDate!.Value - pr.CreatedDate).TotalDays)
            .ToList();

        if (completedWithDates.Count > 0)
        {
            var avgPrAge = (decimal)completedWithDates.Average();
            metrics.Add(("AvgPrAgeDays", Math.Round(avgPrAge, 2), "days"));
        }

        return metrics;
    }

    private async Task<List<(string Name, decimal Value, string? Unit)>> CalculatePipelineMetricsAsync(
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        var metrics = new List<(string, decimal, string?)>();

        // Get pipeline runs for this ProductOwner
        var pipelineRuns = await _context.CachedPipelineRuns
            .Where(r => r.ProductOwnerId == productOwnerId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Total runs
        metrics.Add(("TotalPipelineRuns", pipelineRuns.Count, "count"));

        // Runs by result
        var byResult = pipelineRuns
            .Where(r => !string.IsNullOrEmpty(r.Result))
            .GroupBy(r => r.Result);
        foreach (var group in byResult)
        {
            metrics.Add(($"PipelineRuns_{group.Key}", group.Count(), "count"));
        }

        // Success rate (last 7 days)
        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var runsLast7d = pipelineRuns
            .Where(r => r.FinishedDate.HasValue && r.FinishedDate >= sevenDaysAgo)
            .ToList();

        if (runsLast7d.Count > 0)
        {
            var succeeded = runsLast7d.Count(r => r.Result?.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) == true);
            var successRate = (decimal)succeeded / runsLast7d.Count * 100;
            metrics.Add(("PipelineSuccessRate7d", Math.Round(successRate, 2), "percent"));
        }

        return metrics;
    }

    private async Task UpsertMetricsAsync(
        int productOwnerId,
        List<(string Name, decimal Value, string? Unit)> metrics,
        CancellationToken cancellationToken)
    {
        var metricNames = metrics.Select(m => m.Name).ToList();
        var existingMetrics = await _context.CachedMetrics
            .Where(m => m.ProductOwnerId == productOwnerId && metricNames.Contains(m.MetricName))
            .ToListAsync(cancellationToken);

        var existingDict = existingMetrics.ToDictionary(m => m.MetricName);
        var now = DateTimeOffset.UtcNow;

        foreach (var (name, value, unit) in metrics)
        {
            if (existingDict.TryGetValue(name, out var existing))
            {
                existing.MetricValue = value;
                existing.Unit = unit;
                existing.ComputedAt = now;
            }
            else
            {
                var entity = new CachedMetricsEntity
                {
                    ProductOwnerId = productOwnerId,
                    MetricName = name,
                    MetricValue = value,
                    Unit = unit,
                    ComputedAt = now
                };
                await _context.CachedMetrics.AddAsync(entity, cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
