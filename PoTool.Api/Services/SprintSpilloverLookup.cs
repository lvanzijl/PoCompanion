using PoTool.Core.Metrics.Models;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

internal static class SprintSpilloverLookup
{
    private const string IterationPathFieldRefName = "System.IterationPath";

    public static string? GetNextSprintPath(
        SprintDefinition sprint,
        IEnumerable<SprintDefinition> teamSprints)
    {
        var orderingAnchor = sprint.EndUtc ?? sprint.StartUtc;

        if (!orderingAnchor.HasValue)
        {
            return null;
        }

        return teamSprints
            .Where(candidate => candidate.TeamId == sprint.TeamId
                                && candidate.SprintId != sprint.SprintId
                                && candidate.StartUtc.HasValue
                                && candidate.StartUtc.Value >= orderingAnchor.Value)
            .OrderBy(candidate => candidate.StartUtc)
            .ThenBy(candidate => candidate.SprintId)
            .Select(candidate => NormalizeIterationPath(candidate.Path))
            .FirstOrDefault(path => path != null);
    }

    public static IReadOnlySet<int> BuildSpilloverWorkItemIds(
        IReadOnlySet<int> committedWorkItemIds,
        IReadOnlyDictionary<int, WorkItemSnapshot> workItemsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> stateEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> iterationEventsByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        SprintDefinition sprint,
        string? nextSprintPath,
        DateTimeOffset sprintEnd)
    {
        if (string.IsNullOrWhiteSpace(nextSprintPath))
        {
            return new HashSet<int>();
        }

        var normalizedSprintPath = NormalizeIterationPath(sprint.Path);
        var normalizedNextSprintPath = NormalizeIterationPath(nextSprintPath);
        var spilloverWorkItemIds = new HashSet<int>();

        foreach (var workItemId in committedWorkItemIds)
        {
            if (!workItemsById.TryGetValue(workItemId, out var workItem))
            {
                continue;
            }

            var stateAtSprintEnd = StateReconstructionLookup.GetStateAtTimestamp(
                workItem.CurrentState,
                stateEventsByWorkItem.GetValueOrDefault(workItemId),
                sprintEnd);

            if (StateClassificationLookup.IsDone(stateLookup, workItem.WorkItemType, stateAtSprintEnd))
            {
                continue;
            }

            if (MovedDirectlyToNextSprint(
                    iterationEventsByWorkItem.GetValueOrDefault(workItemId),
                    normalizedSprintPath,
                    normalizedNextSprintPath,
                    sprintEnd))
            {
                spilloverWorkItemIds.Add(workItemId);
            }
        }

        return spilloverWorkItemIds;
    }

    private static bool MovedDirectlyToNextSprint(
        IReadOnlyList<FieldChangeEvent>? iterationEvents,
        string? sprintPath,
        string? nextSprintPath,
        DateTimeOffset sprintEnd)
    {
        if (iterationEvents == null || iterationEvents.Count == 0 || sprintPath == null || nextSprintPath == null)
        {
            return false;
        }

        var firstPostSprintMove = iterationEvents
            .Where(IsIterationPathEvent)
            .Where(iterationEvent => FirstDoneDeliveryLookup.GetEventTimestamp(iterationEvent) >= sprintEnd)
            .OrderBy(GetOrderingTimestampUtc)
            .ThenBy(iterationEvent => iterationEvent.EventId)
            .ThenBy(iterationEvent => iterationEvent.UpdateId)
            .FirstOrDefault();

        return firstPostSprintMove != null
               && string.Equals(NormalizeIterationPath(firstPostSprintMove.OldValue), sprintPath, StringComparison.OrdinalIgnoreCase)
               && string.Equals(NormalizeIterationPath(firstPostSprintMove.NewValue), nextSprintPath, StringComparison.OrdinalIgnoreCase);
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
