using PoTool.Api.Persistence.Entities;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

internal static class FirstDoneDeliveryLookup
{
    private const string StateFieldRefName = "System.State";

    public static IReadOnlyDictionary<int, DateTimeOffset> Build(
        IEnumerable<ActivityEventLedgerEntryEntity> activityEvents,
        IReadOnlyDictionary<int, string> workItemTypesById,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null)
    {
        return activityEvents
            .Where(activityEvent =>
                string.Equals(activityEvent.FieldRefName, StateFieldRefName, StringComparison.OrdinalIgnoreCase)
                && workItemTypesById.ContainsKey(activityEvent.WorkItemId))
            .GroupBy(activityEvent => activityEvent.WorkItemId)
            .Select(group =>
            {
                var firstDoneTimestamp = GetFirstDoneTransitionTimestamp(
                    group,
                    workItemTypesById[group.Key],
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
        IEnumerable<ActivityEventLedgerEntryEntity>? activityEvents,
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
                     .ThenBy(activityEvent => activityEvent.Id)
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

    public static DateTimeOffset GetEventTimestamp(ActivityEventLedgerEntryEntity activityEvent)
    {
        return activityEvent.EventTimestamp != default
            ? activityEvent.EventTimestamp
            : new DateTimeOffset(DateTime.SpecifyKind(activityEvent.EventTimestampUtc, DateTimeKind.Utc));
    }

    private static DateTime GetOrderingTimestampUtc(ActivityEventLedgerEntryEntity activityEvent)
    {
        return activityEvent.EventTimestampUtc != default
            ? DateTime.SpecifyKind(activityEvent.EventTimestampUtc, DateTimeKind.Utc)
            : GetEventTimestamp(activityEvent).UtcDateTime;
    }
}
