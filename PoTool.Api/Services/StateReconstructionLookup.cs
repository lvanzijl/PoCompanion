using PoTool.Core.Metrics.Models;

namespace PoTool.Api.Services;

internal static class StateReconstructionLookup
{
    private const string StateFieldRefName = "System.State";

    public static string? GetStateAtTimestamp(
        string? currentState,
        IReadOnlyList<FieldChangeEvent>? stateEvents,
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
                     .ThenByDescending(stateEvent => stateEvent.EventId)
                     .ThenByDescending(stateEvent => stateEvent.UpdateId))
        {
            reconstructedState = NormalizeState(stateEvent.OldValue);
        }

        return reconstructedState;
    }

    private static bool IsStateEvent(FieldChangeEvent activityEvent)
    {
        return string.Equals(activityEvent.FieldRefName, StateFieldRefName, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime GetOrderingTimestampUtc(FieldChangeEvent activityEvent)
    {
        return activityEvent.TimestampUtc;
    }

    private static string? NormalizeState(string? state)
    {
        return string.IsNullOrWhiteSpace(state)
            ? null
            : state.Trim();
    }
}
