using PoTool.Core.Metrics.Models;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

internal static class FirstDoneDeliveryLookup
{
    private const string StateFieldRefName = "System.State";

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

    public static DateTimeOffset GetEventTimestamp(FieldChangeEvent activityEvent)
    {
        return activityEvent.Timestamp;
    }

    private static DateTime GetOrderingTimestampUtc(FieldChangeEvent activityEvent)
    {
        return activityEvent.TimestampUtc;
    }
}
