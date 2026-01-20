using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PoTool.Client.Services;

/// <summary>
/// Coordinates work item loading operations to prevent duplicate in-flight requests.
/// When multiple views request the same root set, they share the same loading task.
/// </summary>
public class WorkItemLoadCoordinatorService
{
    private readonly ConcurrentDictionary<string, Task> _inflightLoads = new();
    private readonly ILogger<WorkItemLoadCoordinatorService> _logger;

    public WorkItemLoadCoordinatorService(ILogger<WorkItemLoadCoordinatorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Ensures work items are loaded for the specified root IDs.
    /// If a load is already in progress for the same root set, returns the existing task.
    /// If the root set differs, starts a new load operation.
    /// </summary>
    /// <param name="rootIds">The root work item IDs to load.</param>
    /// <param name="loadFunc">Function to execute the actual load operation.</param>
    /// <returns>Task that completes when the load is finished.</returns>
    public async Task EnsureLoadedAsync(int[] rootIds, Func<Task> loadFunc)
    {
        if (rootIds == null || rootIds.Length == 0)
        {
            return;
        }

        // Create a unique key for this root set
        var key = CreateKey(rootIds);

        // Try to get or add the load task
        var loadTask = _inflightLoads.GetOrAdd(key, _ =>
        {
            _logger.LogInformation("Starting new load for roots: {RootIds}", string.Join(", ", rootIds));
            return ExecuteLoadAsync(key, loadFunc);
        });

        // If we got an existing task, log that we're reusing it
        if (!loadTask.IsCompleted)
        {
            _logger.LogInformation("Reusing in-flight load for roots: {RootIds}", string.Join(", ", rootIds));
        }

        await loadTask;
    }

    private async Task ExecuteLoadAsync(string key, Func<Task> loadFunc)
    {
        try
        {
            await loadFunc();
        }
        finally
        {
            // Remove from tracking once complete (success or failure)
            _inflightLoads.TryRemove(key, out _);
            _logger.LogDebug("Load completed and removed from tracking: {Key}", key);
        }
    }

    private static string CreateKey(int[] rootIds)
    {
        // Sort the IDs to ensure consistent keys regardless of order
        var sortedIds = rootIds.OrderBy(id => id).ToArray();
        return string.Join(",", sortedIds);
    }

    /// <summary>
    /// Clears all in-flight load tracking.
    /// Useful for testing or when forcing a full reload.
    /// </summary>
    public void Clear()
    {
        _inflightLoads.Clear();
    }
}
