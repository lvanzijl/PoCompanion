namespace PoTool.Client.Services;

public enum RoadmapEpicPriorityReorderFailureReason
{
    MissingPriority,
    InvalidPriority,
    DuplicatePriority
}

public sealed record RoadmapEpicPriorityItem(
    int TfsId,
    double? BacklogPriority);

public sealed record RoadmapEpicPriorityWrite(
    int TfsId,
    double OriginalPriority,
    double NewPriority);

public static class RoadmapEpicPriorityReorderPlanner
{
    public static bool TryCreatePriorityPreservingPlan(
        IReadOnlyList<RoadmapEpicPriorityItem> orderedEpics,
        int currentIndex,
        int targetIndex,
        out IReadOnlyList<RoadmapEpicPriorityWrite>? writes,
        out RoadmapEpicPriorityReorderFailureReason? failureReason)
    {
        ArgumentNullException.ThrowIfNull(orderedEpics);

        if (orderedEpics.Count == 0 ||
            currentIndex < 0 ||
            currentIndex >= orderedEpics.Count ||
            targetIndex < 0)
        {
            writes = [];
            failureReason = null;
            return true;
        }

        var reordered = new List<RoadmapEpicPriorityItem>(orderedEpics);
        var movedEpic = reordered[currentIndex];
        reordered.RemoveAt(currentIndex);
        reordered.Insert(Math.Min(targetIndex, reordered.Count), movedEpic);

        var priorities = new List<double>(orderedEpics.Count);
        var seenPriorities = new HashSet<double>();

        foreach (var epic in orderedEpics)
        {
            if (!epic.BacklogPriority.HasValue)
            {
                writes = null;
                failureReason = RoadmapEpicPriorityReorderFailureReason.MissingPriority;
                return false;
            }

            var priority = epic.BacklogPriority.Value;
            if (double.IsNaN(priority) || double.IsInfinity(priority))
            {
                writes = null;
                failureReason = RoadmapEpicPriorityReorderFailureReason.InvalidPriority;
                return false;
            }

            if (!seenPriorities.Add(priority))
            {
                writes = null;
                failureReason = RoadmapEpicPriorityReorderFailureReason.DuplicatePriority;
                return false;
            }

            priorities.Add(priority);
        }

        var plannedWrites = new List<RoadmapEpicPriorityWrite>();
        for (var i = 0; i < reordered.Count; i++)
        {
            var epic = reordered[i];
            var newPriority = priorities[i];
            if (epic.BacklogPriority != newPriority)
            {
                plannedWrites.Add(new RoadmapEpicPriorityWrite(
                    epic.TfsId,
                    epic.BacklogPriority!.Value,
                    newPriority));
            }
        }

        writes = plannedWrites;
        failureReason = null;
        return true;
    }
}
