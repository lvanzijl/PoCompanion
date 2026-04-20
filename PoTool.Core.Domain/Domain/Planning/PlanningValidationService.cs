namespace PoTool.Core.Domain.Planning;

/// <summary>
/// Validates locked hard constraints for planning-engine state.
/// </summary>
public sealed class PlanningValidationService
{
    /// <summary>
    /// Validates the supplied planning state without mutating it.
    /// </summary>
    public IReadOnlyList<PlanningValidationIssue> Validate(PlanningState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var issues = new List<PlanningValidationIssue>();
        var orderedEpics = PlanningStateOrdering.OrderEpics(state).ToArray();

        var seenEpicIds = new HashSet<int>();
        for (var index = 0; index < orderedEpics.Length; index++)
        {
            var epic = orderedEpics[index];

            if (!seenEpicIds.Add(epic.EpicId))
            {
                issues.Add(new PlanningValidationIssue(
                    PlanningValidationIssueCode.DuplicateEpicId,
                    $"Epic '{epic.EpicId}' appears more than once in planning state.",
                    epic.EpicId));
            }

            if (epic.DurationInSprints < 1)
            {
                issues.Add(new PlanningValidationIssue(
                    PlanningValidationIssueCode.InvalidDuration,
                    $"Epic '{epic.EpicId}' has invalid duration '{epic.DurationInSprints}'.",
                    epic.EpicId));
            }

            if (epic.PlannedStartSprintIndex < 0)
            {
                issues.Add(new PlanningValidationIssue(
                    PlanningValidationIssueCode.NegativePlannedStart,
                    $"Epic '{epic.EpicId}' has negative planned start '{epic.PlannedStartSprintIndex}'.",
                    epic.EpicId));
            }

            if (epic.TrackIndex < 0)
            {
                issues.Add(new PlanningValidationIssue(
                    PlanningValidationIssueCode.InvalidTrackIndex,
                    $"Epic '{epic.EpicId}' has invalid track '{epic.TrackIndex}'.",
                    epic.EpicId));
            }

            if (epic.RoadmapOrder != index + 1)
            {
                issues.Add(new PlanningValidationIssue(
                    PlanningValidationIssueCode.InvalidRoadmapOrder,
                    $"Epic '{epic.EpicId}' has roadmap order '{epic.RoadmapOrder}' but expected '{index + 1}'.",
                    epic.EpicId));
            }

            if (index > 0 && epic.ComputedStartSprintIndex < orderedEpics[index - 1].ComputedStartSprintIndex)
            {
                issues.Add(new PlanningValidationIssue(
                    PlanningValidationIssueCode.RoadmapStartOrderViolation,
                    $"Epic '{epic.EpicId}' starts before roadmap predecessor '{orderedEpics[index - 1].EpicId}'.",
                    epic.EpicId));
            }

            var sameTrackPredecessor = FindSameTrackPredecessor(orderedEpics, index);
            if (sameTrackPredecessor is not null &&
                epic.ComputedStartSprintIndex < sameTrackPredecessor.EndSprintIndexExclusive)
            {
                issues.Add(new PlanningValidationIssue(
                    PlanningValidationIssueCode.SameTrackOverlap,
                    $"Epic '{epic.EpicId}' overlaps track predecessor '{sameTrackPredecessor.EpicId}' on track '{epic.TrackIndex}'.",
                    epic.EpicId));
            }
        }

        return issues;
    }

    private static PlanningEpicState? FindSameTrackPredecessor(
        IReadOnlyList<PlanningEpicState> orderedEpics,
        int currentIndex)
    {
        var trackIndex = orderedEpics[currentIndex].TrackIndex;
        for (var index = currentIndex - 1; index >= 0; index--)
        {
            if (orderedEpics[index].TrackIndex == trackIndex)
            {
                return orderedEpics[index];
            }
        }

        return null;
    }
}
