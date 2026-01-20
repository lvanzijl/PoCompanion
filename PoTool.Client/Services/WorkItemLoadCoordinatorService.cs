using System.Collections.Concurrent;

namespace PoTool.Client.Services;

/// <summary>
/// Coordinates work item loading operations to prevent duplicate in-flight requests.
/// When multiple views request the same root set, they share the same loading task.
/// </summary>
public class WorkItemLoadCoordinatorService
{
    private readonly ConcurrentDictionary<string, Task> _inflightLoads = new();

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
            Console.WriteLine($"[WorkItemLoadCoordinator] Starting new load for roots: {string.Join(", ", rootIds)}");
            return ExecuteLoadAsync(key, loadFunc);
        });

        // If we got an existing task, log that we're reusing it
        if (loadTask.Status == TaskStatus.Running || !loadTask.IsCompleted)
        {
            Console.WriteLine($"[WorkItemLoadCoordinator] Reusing in-flight load for roots: {string.Join(", ", rootIds)}");
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
            Console.WriteLine($"[WorkItemLoadCoordinator] Load completed and removed from tracking: {key}");
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
        Console.WriteLine("[WorkItemLoadCoordinator] Cleared all in-flight load tracking");
    }
}
