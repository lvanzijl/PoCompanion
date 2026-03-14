using PoTool.Core.Metrics.Models;

namespace PoTool.Core.Domain.Sprints;

/// <summary>
/// Reconstructs a work item's raw state at a point in time from canonical field-change history.
/// </summary>
public static class StateReconstructionLookup
{
    private const string StateFieldRefName = "System.State";

    /// <summary>
    /// Replays post-target state changes backward from the current snapshot state to recover the state at the requested timestamp.
    /// </summary>
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
