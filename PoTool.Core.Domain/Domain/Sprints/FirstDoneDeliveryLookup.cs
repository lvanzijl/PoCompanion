using PoTool.Core.Domain.Models;

namespace PoTool.Core.Domain.Sprints;

/// <summary>
/// Attributes delivery to the first canonical transition into Done for each work item.
/// </summary>
public static class FirstDoneDeliveryLookup
{
    private const string StateFieldRefName = "System.State";

    /// <summary>
    /// Builds the first-Done timestamp lookup for work items present in the provided snapshot set.
    /// </summary>
    public static IReadOnlyDictionary<int, DateTimeOffset> Build(
        IEnumerable<FieldChangeEvent> activityEvents,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        return activityEvents
            .Where(activityEvent =>
                string.Equals(activityEvent.FieldRefName, StateFieldRefName, StringComparison.OrdinalIgnoreCase)
                && workItemsById.ContainsKey(activityEvent.WorkItemId))
            .GroupBy(activityEvent => activityEvent.WorkItemId)
            .Select(group =>
            {
                var firstDoneTimestamp = GetFirstDoneTransitionTimestamp(
                    group,
                    workItemsById[group.Key].WorkItemType,
                    stateLookup);

                return new
                {
                    WorkItemId = group.Key,
                    FirstDoneTimestamp = firstDoneTimestamp
                };
            })
            .Where(entry => entry.FirstDoneTimestamp.HasValue)
            .ToDictionary(entry => entry.WorkItemId, entry => entry.FirstDoneTimestamp!.Value);
    }

    /// <summary>
    /// Returns the first timestamp where a work item transitioned from a non-Done state into canonical Done.
    /// </summary>
    public static DateTimeOffset? GetFirstDoneTransitionTimestamp(
        IEnumerable<FieldChangeEvent>? activityEvents,
        string workItemType,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        if (activityEvents == null)
        {
            return null;
        }

        foreach (var activityEvent in activityEvents
                     .Where(activityEvent => string.Equals(activityEvent.FieldRefName, StateFieldRefName, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(GetOrderingTimestampUtc)
                     .ThenBy(activityEvent => activityEvent.EventId)
                     .ThenBy(activityEvent => activityEvent.UpdateId))
        {
            if (!StateClassificationLookup.IsDone(stateLookup, workItemType, activityEvent.NewValue))
            {
                continue;
            }

            if (StateClassificationLookup.IsDone(stateLookup, workItemType, activityEvent.OldValue))
            {
                continue;
            }

            return GetEventTimestamp(activityEvent);
        }

        return null;
    }

    /// <summary>
    /// Returns the event timestamp used by the sprint-history helpers for point-in-time ordering.
    /// </summary>
    public static DateTimeOffset GetEventTimestamp(FieldChangeEvent activityEvent)
    {
        return activityEvent.Timestamp;
    }

    private static DateTime GetOrderingTimestampUtc(FieldChangeEvent activityEvent)
    {
        return activityEvent.TimestampUtc;
    }
}
