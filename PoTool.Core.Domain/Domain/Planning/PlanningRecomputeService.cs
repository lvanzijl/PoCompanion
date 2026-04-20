namespace PoTool.Core.Domain.Planning;

/// <summary>
/// Recomputes planning state from a changed roadmap index using the locked deterministic formula.
/// </summary>
public sealed class PlanningRecomputeService
{
    /// <summary>
    /// Recomputes the roadmap suffix while preserving the earlier prefix unchanged.
    /// </summary>
    public PlanningState RecomputeFrom(PlanningState state, int changedIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        var orderedEpics = PlanningStateOrdering.OrderEpics(state).ToArray();
        if (orderedEpics.Length == 0)
        {
            return PlanningState.Empty;
        }

        if (changedIndex < 0 || changedIndex >= orderedEpics.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(changedIndex));
        }

        for (var index = changedIndex; index < orderedEpics.Length; index++)
        {
            var roadmapStartFloor = index == 0
                ? 0
                : orderedEpics[index - 1].ComputedStartSprintIndex;

            var trackAvailabilityFloor = GetTrackAvailabilityFloor(orderedEpics, index);
            var computedStart = Math.Max(
                orderedEpics[index].PlannedStartSprintIndex,
                Math.Max(roadmapStartFloor, trackAvailabilityFloor));

            orderedEpics[index] = orderedEpics[index] with
            {
                ComputedStartSprintIndex = computedStart,
            };
        }

        return new PlanningState(orderedEpics);
    }

    private static int GetTrackAvailabilityFloor(
        IReadOnlyList<PlanningEpicState> orderedEpics,
        int currentIndex)
    {
        var currentTrackIndex = orderedEpics[currentIndex].TrackIndex;

        for (var index = currentIndex - 1; index >= 0; index--)
        {
            if (orderedEpics[index].TrackIndex == currentTrackIndex)
            {
                return orderedEpics[index].EndSprintIndexExclusive;
            }
        }

        return 0;
    }
}
