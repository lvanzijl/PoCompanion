using PoTool.Api.Persistence.Entities;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

internal static class SprintSpilloverLookup
{
    private const string IterationPathFieldRefName = "System.IterationPath";

    public static string? GetNextSprintPath(
        SprintEntity sprint,
        IEnumerable<SprintEntity> teamSprints)
    {
        var orderingAnchor = sprint.EndDateUtc ?? sprint.StartDateUtc;

        if (!orderingAnchor.HasValue)
        {
            return null;
        }

        return teamSprints
            .Where(candidate => candidate.TeamId == sprint.TeamId
                                && candidate.Id != sprint.Id
                                && candidate.StartDateUtc.HasValue
                                && candidate.StartDateUtc.Value >= orderingAnchor.Value)
            .OrderBy(candidate => candidate.StartDateUtc)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => NormalizeIterationPath(candidate.Path))
            .FirstOrDefault(path => path != null);
    }

    public static IReadOnlySet<int> BuildSpilloverWorkItemIds(
        IReadOnlySet<int> committedWorkItemIds,
        IReadOnlyDictionary<int, string?> currentStatesById,
        IReadOnlyDictionary<int, string> workItemTypesById,
        IReadOnlyDictionary<int, IReadOnlyList<ActivityEventLedgerEntryEntity>> stateEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<ActivityEventLedgerEntryEntity>> iterationEventsByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup,
        string sprintPath,
        string? nextSprintPath,
        DateTimeOffset sprintEnd)
    {
        if (string.IsNullOrWhiteSpace(nextSprintPath))
        {
            return new HashSet<int>();
        }

        var normalizedSprintPath = NormalizeIterationPath(sprintPath);
        var normalizedNextSprintPath = NormalizeIterationPath(nextSprintPath);
        var spilloverWorkItemIds = new HashSet<int>();

        foreach (var workItemId in committedWorkItemIds)
        {
            if (!workItemTypesById.TryGetValue(workItemId, out var workItemType))
            {
                continue;
            }

            var stateAtSprintEnd = StateReconstructionLookup.GetStateAtTimestamp(
                currentStatesById.GetValueOrDefault(workItemId),
                stateEventsByWorkItem.GetValueOrDefault(workItemId),
                sprintEnd);

            if (StateClassificationLookup.IsDone(stateLookup, workItemType, stateAtSprintEnd))
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
        IReadOnlyList<ActivityEventLedgerEntryEntity>? iterationEvents,
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
            .ThenBy(iterationEvent => iterationEvent.Id)
            .ThenBy(iterationEvent => iterationEvent.UpdateId)
            .FirstOrDefault();

        return firstPostSprintMove != null
               && string.Equals(NormalizeIterationPath(firstPostSprintMove.OldValue), sprintPath, StringComparison.OrdinalIgnoreCase)
               && string.Equals(NormalizeIterationPath(firstPostSprintMove.NewValue), nextSprintPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIterationPathEvent(ActivityEventLedgerEntryEntity activityEvent)
    {
        return string.Equals(activityEvent.FieldRefName, IterationPathFieldRefName, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime GetOrderingTimestampUtc(ActivityEventLedgerEntryEntity activityEvent)
    {
        return activityEvent.EventTimestampUtc != default
            ? DateTime.SpecifyKind(activityEvent.EventTimestampUtc, DateTimeKind.Utc)
            : FirstDoneDeliveryLookup.GetEventTimestamp(activityEvent).UtcDateTime;
    }

    private static string? NormalizeIterationPath(string? iterationPath)
    {
        return string.IsNullOrWhiteSpace(iterationPath)
            ? null
            : iterationPath.Trim();
    }
}
