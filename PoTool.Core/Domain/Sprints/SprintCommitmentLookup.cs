using PoTool.Core.Domain.Models;

namespace PoTool.Core.Domain.Sprints;

/// <summary>
/// Reconstructs sprint commitment membership at the canonical sprint commitment timestamp.
/// </summary>
public static class SprintCommitmentLookup
{
    private const string IterationPathFieldRefName = "System.IterationPath";

    /// <summary>
    /// Returns the canonical sprint commitment timestamp.
    /// </summary>
    public static DateTimeOffset GetCommitmentTimestamp(DateTimeOffset sprintStart)
    {
        return sprintStart.AddDays(1);
    }

    /// <summary>
    /// Builds the set of work item IDs whose iteration path matched the sprint at the commitment timestamp.
    /// </summary>
    public static IReadOnlySet<int> BuildCommittedWorkItemIds(
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem,
        string sprintPath,
        DateTimeOffset commitmentTimestamp)
    {
        var committedWorkItemIds = new HashSet<int>();

        foreach (var workItem in workItemsById.Values)
        {
            var reconstructedIterationPath = GetIterationPathAtTimestamp(
                workItem.CurrentIterationPath,
                iterationEventsByWorkItem.GetValueOrDefault(workItem.WorkItemId),
                commitmentTimestamp);

            if (string.Equals(reconstructedIterationPath, sprintPath, StringComparison.OrdinalIgnoreCase))
            {
                committedWorkItemIds.Add(workItem.WorkItemId);
            }
        }

        return committedWorkItemIds;
    }

    /// <summary>
    /// Replays iteration-path changes backward from the current snapshot to recover sprint membership at the requested timestamp.
    /// </summary>
    public static string? GetIterationPathAtTimestamp(
        string? currentIterationPath,
        IReadOnlyList<FieldChangeEvent>? iterationEvents,
        DateTimeOffset targetTimestamp)
    {
        var reconstructedIterationPath = NormalizeIterationPath(currentIterationPath);

        if (iterationEvents == null || iterationEvents.Count == 0)
        {
            return reconstructedIterationPath;
        }

        foreach (var iterationEvent in iterationEvents
                     .Where(IsIterationPathEvent)
                     .Where(iterationEvent => FirstDoneDeliveryLookup.GetEventTimestamp(iterationEvent) > targetTimestamp)
                     .OrderByDescending(GetOrderingTimestampUtc)
                     .ThenByDescending(iterationEvent => iterationEvent.EventId)
                     .ThenByDescending(iterationEvent => iterationEvent.UpdateId))
        {
            reconstructedIterationPath = NormalizeIterationPath(iterationEvent.OldValue);
        }

        return reconstructedIterationPath;
    }

    private static bool IsIterationPathEvent(FieldChangeEvent activityEvent)
    {
        return string.Equals(activityEvent.FieldRefName, IterationPathFieldRefName, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime GetOrderingTimestampUtc(FieldChangeEvent activityEvent)
    {
        return activityEvent.TimestampUtc;
    }

    private static string? NormalizeIterationPath(string? iterationPath)
    {
        return string.IsNullOrWhiteSpace(iterationPath)
            ? null
            : iterationPath.Trim();
    }
}
