using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Services;

internal static class StateReconstructionLookup
{
    private const string StateFieldRefName = "System.State";

    public static string? GetStateAtTimestamp(
        string? currentState,
        IReadOnlyList<ActivityEventLedgerEntryEntity>? stateEvents,
        DateTimeOffset targetTimestamp)
    {
        var reconstructedState = NormalizeState(currentState);

        if (stateEvents == null || stateEvents.Count == 0)
        {
            return reconstructedState;
        }

        foreach (var stateEvent in stateEvents
                     .Where(IsStateEvent)
                     .Where(stateEvent => FirstDoneDeliveryLookup.GetEventTimestamp(stateEvent) > targetTimestamp)
                     .OrderByDescending(GetOrderingTimestampUtc)
                     .ThenByDescending(stateEvent => stateEvent.Id)
                     .ThenByDescending(stateEvent => stateEvent.UpdateId))
        {
            reconstructedState = NormalizeState(stateEvent.OldValue);
        }

        return reconstructedState;
    }

    private static bool IsStateEvent(ActivityEventLedgerEntryEntity activityEvent)
    {
        return string.Equals(activityEvent.FieldRefName, StateFieldRefName, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime GetOrderingTimestampUtc(ActivityEventLedgerEntryEntity activityEvent)
    {
        return activityEvent.EventTimestampUtc != default
            ? DateTime.SpecifyKind(activityEvent.EventTimestampUtc, DateTimeKind.Utc)
            : FirstDoneDeliveryLookup.GetEventTimestamp(activityEvent).UtcDateTime;
    }

    private static string? NormalizeState(string? state)
    {
        return string.IsNullOrWhiteSpace(state)
            ? null
            : state.Trim();
    }
}
