namespace PoTool.Core.Domain.Planning;

internal static class PlanningStateOrdering
{
    public static IEnumerable<PlanningEpicState> OrderEpics(PlanningState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Epics
            .OrderBy(epic => epic.RoadmapOrder)
            .ThenBy(epic => epic.EpicId);
    }
}
