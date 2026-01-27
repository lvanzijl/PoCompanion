using Microsoft.Extensions.Logging;
using PoTool.Shared.WorkItems;

namespace PoTool.Integrations.Tfs.Clients;

public partial class RealTfsClient
{
    /// <summary>
    /// Gets revision history for multiple work items in a batch.
    /// Uses parallel requests to TFS for improved performance.
    /// </summary>
    public async Task<IDictionary<int, IEnumerable<WorkItemRevisionDto>>> GetWorkItemRevisionsBatchAsync(
        IEnumerable<int> workItemIds,
        CancellationToken cancellationToken = default)
    {
        var idsList = workItemIds.ToList();
        var tfsCallCount = 0;

        _logger.LogInformation("Starting bulk revision fetch for {Count} work items", idsList.Count);

        var results = new Dictionary<int, IEnumerable<WorkItemRevisionDto>>();
        var lockObj = new object();

        // Fetch revisions with read throttling to prevent overwhelming the server
        var fetchTasks = idsList.Select(async workItemId =>
        {
            try
            {
                // Apply read throttling to revision fetching (GET operations)
                var revisions = await _throttler.ExecuteReadAsync(
                    () => GetWorkItemRevisionsAsync(workItemId, cancellationToken),
                    cancellationToken);
                Interlocked.Increment(ref tfsCallCount);

                lock (lockObj)
                {
                    results[workItemId] = revisions;
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref tfsCallCount);
                _logger.LogWarning(ex, "Failed to fetch revisions for work item {WorkItemId}", workItemId);
                lock (lockObj)
                {
                    results[workItemId] = Enumerable.Empty<WorkItemRevisionDto>();
                }
            }
        });

        await Task.WhenAll(fetchTasks);

        _logger.LogInformation("Bulk revision fetch completed: {Count} work items ({CallCount} TFS calls)",
            results.Count, tfsCallCount);

        return results;
    }
}
