using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Services;

internal static class SprintCommitmentLookup
{
    private const string IterationPathFieldRefName = "System.IterationPath";

    public static DateTimeOffset GetCommitmentTimestamp(DateTimeOffset sprintStart)
    {
        return sprintStart.AddDays(1);
    }

    public static IReadOnlySet<int> BuildCommittedWorkItemIds(
        IReadOnlyDictionary<int, string?> currentIterationPathsById,
        IReadOnlyDictionary<int, IReadOnlyList<ActivityEventLedgerEntryEntity>> iterationEventsByWorkItem,
        string sprintPath,
        DateTimeOffset commitmentTimestamp)
    {
        var committedWorkItemIds = new HashSet<int>();

        foreach (var currentIterationPath in currentIterationPathsById)
        {
            var reconstructedIterationPath = GetIterationPathAtTimestamp(
                currentIterationPath.Value,
                iterationEventsByWorkItem.GetValueOrDefault(currentIterationPath.Key),
                commitmentTimestamp);

            if (string.Equals(reconstructedIterationPath, sprintPath, StringComparison.OrdinalIgnoreCase))
            {
                committedWorkItemIds.Add(currentIterationPath.Key);
            }
        }

        return committedWorkItemIds;
    }

    public static string? GetIterationPathAtTimestamp(
        string? currentIterationPath,
        IReadOnlyList<ActivityEventLedgerEntryEntity>? iterationEvents,
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
                     .ThenByDescending(iterationEvent => iterationEvent.Id)
                     .ThenByDescending(iterationEvent => iterationEvent.UpdateId))
        {
            reconstructedIterationPath = NormalizeIterationPath(iterationEvent.OldValue);
        }

        return reconstructedIterationPath;
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
