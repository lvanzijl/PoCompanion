namespace PoTool.Core.Domain.Planning;

/// <summary>
/// Applies explicit planning operations using deterministic recompute and hard-constraint validation.
/// </summary>
public sealed class PlanningOperationService
{
    private readonly PlanningRecomputeService _recomputeService;
    private readonly PlanningValidationService _validationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanningOperationService"/> class.
    /// </summary>
    public PlanningOperationService()
        : this(new PlanningRecomputeService(), new PlanningValidationService())
    {
    }

    internal PlanningOperationService(
        PlanningRecomputeService recomputeService,
        PlanningValidationService validationService)
    {
        _recomputeService = recomputeService ?? throw new ArgumentNullException(nameof(recomputeService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    /// <summary>
    /// Moves an epic's planned start by the requested sprint delta and recomputes the suffix.
    /// </summary>
    public PlanningOperationResult MoveEpicBySprints(PlanningState state, int epicId, int deltaSprints)
    {
        return ApplySingleEpicOperation(
            state,
            epicId,
            deltaSprints == 0
                ? new PlanningValidationIssue(
                    PlanningValidationIssueCode.InvalidOperationInput,
                    "MoveEpicBySprints requires a non-zero delta.",
                    epicId)
                : null,
            epic => epic with
            {
                PlannedStartSprintIndex = Math.Max(0, epic.PlannedStartSprintIndex + deltaSprints),
            });
    }

    /// <summary>
    /// Adjusts requested spacing by changing planned start and recomputing the suffix.
    /// </summary>
    public PlanningOperationResult AdjustSpacingBefore(PlanningState state, int epicId, int deltaSprints)
    {
        return ApplySingleEpicOperation(
            state,
            epicId,
            deltaSprints == 0
                ? new PlanningValidationIssue(
                    PlanningValidationIssueCode.InvalidOperationInput,
                    "AdjustSpacingBefore requires a non-zero delta.",
                    epicId)
                : null,
            epic => epic with
            {
                PlannedStartSprintIndex = Math.Max(0, epic.PlannedStartSprintIndex + deltaSprints),
            });
    }

    /// <summary>
    /// Moves an epic to the lowest reusable positive track and recomputes the suffix.
    /// </summary>
    public PlanningOperationResult RunInParallel(PlanningState state, int epicId)
    {
        return ApplySingleEpicOperation(
            state,
            epicId,
            null,
            (orderedEpics, epicIndex) =>
            {
                var roadmapStartFloor = epicIndex == 0
                    ? 0
                    : orderedEpics[epicIndex - 1].ComputedStartSprintIndex;
                var concurrencyTargetStart = Math.Max(
                    orderedEpics[epicIndex].PlannedStartSprintIndex,
                    roadmapStartFloor);

                var nextTrackIndex = GetLowestReusablePositiveTrack(orderedEpics, epicIndex, concurrencyTargetStart);
                return orderedEpics[epicIndex] with { TrackIndex = nextTrackIndex };
            });
    }

    /// <summary>
    /// Returns an epic to the main lane and recomputes the suffix.
    /// </summary>
    public PlanningOperationResult ReturnToMain(PlanningState state, int epicId)
    {
        return ApplySingleEpicOperation(
            state,
            epicId,
            null,
            epic => epic with { TrackIndex = 0 });
    }

    private PlanningOperationResult ApplySingleEpicOperation(
        PlanningState state,
        int epicId,
        PlanningValidationIssue? upfrontIssue,
        Func<PlanningEpicState, PlanningEpicState> mutateEpic)
    {
        return ApplySingleEpicOperation(
            state,
            epicId,
            upfrontIssue,
            (orderedEpics, epicIndex) => mutateEpic(orderedEpics[epicIndex]));
    }

    private PlanningOperationResult ApplySingleEpicOperation(
        PlanningState state,
        int epicId,
        PlanningValidationIssue? upfrontIssue,
        Func<IReadOnlyList<PlanningEpicState>, int, PlanningEpicState> mutateEpic)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(mutateEpic);

        var baselineIssues = _validationService.Validate(state);
        if (baselineIssues.Count > 0)
        {
            return new PlanningOperationResult(state, Array.Empty<int>(), Array.Empty<int>(), baselineIssues);
        }

        if (upfrontIssue is not null)
        {
            return new PlanningOperationResult(state, Array.Empty<int>(), Array.Empty<int>(), new[] { upfrontIssue });
        }

        var orderedEpics = PlanningStateOrdering.OrderEpics(state).ToArray();
        var epicIndex = Array.FindIndex(orderedEpics, epic => epic.EpicId == epicId);
        if (epicIndex < 0)
        {
            return new PlanningOperationResult(
                state,
                Array.Empty<int>(),
                Array.Empty<int>(),
                new[]
                {
                    new PlanningValidationIssue(
                        PlanningValidationIssueCode.EpicNotFound,
                        $"Epic '{epicId}' was not found in planning state.",
                        epicId),
                });
        }

        orderedEpics[epicIndex] = mutateEpic(orderedEpics, epicIndex);

        var updatedState = _recomputeService.RecomputeFrom(new PlanningState(orderedEpics), epicIndex);
        var validationIssues = _validationService.Validate(updatedState);
        var changedEpicIds = GetChangedEpicIds(state, updatedState);
        var affectedEpicIds = PlanningStateOrdering
            .OrderEpics(updatedState)
            .Skip(epicIndex)
            .Select(epic => epic.EpicId)
            .ToArray();

        return new PlanningOperationResult(updatedState, changedEpicIds, affectedEpicIds, validationIssues);
    }

    private static int GetLowestReusablePositiveTrack(
        IReadOnlyList<PlanningEpicState> orderedEpics,
        int epicIndex,
        int concurrencyTargetStart)
    {
        var highestTrackIndex = orderedEpics
            .Take(epicIndex)
            .Select(epic => epic.TrackIndex)
            .DefaultIfEmpty(0)
            .Max();

        for (var candidateTrackIndex = 1; candidateTrackIndex <= highestTrackIndex; candidateTrackIndex++)
        {
            var trackAvailability = 0;
            for (var index = epicIndex - 1; index >= 0; index--)
            {
                if (orderedEpics[index].TrackIndex == candidateTrackIndex)
                {
                    trackAvailability = orderedEpics[index].EndSprintIndexExclusive;
                    break;
                }
            }

            if (trackAvailability <= concurrencyTargetStart)
            {
                return candidateTrackIndex;
            }
        }

        return highestTrackIndex + 1;
    }

    private static IReadOnlyList<int> GetChangedEpicIds(PlanningState previousState, PlanningState updatedState)
    {
        var previousById = previousState.Epics.ToDictionary(epic => epic.EpicId);
        return PlanningStateOrdering
            .OrderEpics(updatedState)
            .Where(epic => !previousById.TryGetValue(epic.EpicId, out var previousEpic) || previousEpic != epic)
            .Select(epic => epic.EpicId)
            .ToArray();
    }
}
