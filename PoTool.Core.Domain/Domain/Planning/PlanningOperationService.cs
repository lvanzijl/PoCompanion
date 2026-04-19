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

    /// <summary>
    /// Reorders an epic within the roadmap, renumbers orders contiguously, and recomputes the affected suffix.
    /// </summary>
    public PlanningOperationResult ReorderEpic(PlanningState state, int epicId, int targetRoadmapOrder)
    {
        ArgumentNullException.ThrowIfNull(state);

        var baselineIssues = _validationService.Validate(state);
        if (baselineIssues.Count > 0)
        {
            return CreateIssueResult(state, baselineIssues);
        }

        var orderedEpics = PlanningStateOrdering.OrderEpics(state).ToList();
        var epicIndex = orderedEpics.FindIndex(epic => epic.EpicId == epicId);
        if (epicIndex < 0)
        {
            return CreateIssueResult(
                state,
                new PlanningValidationIssue(
                    PlanningValidationIssueCode.EpicNotFound,
                    $"Epic '{epicId}' was not found in planning state.",
                    epicId));
        }

        if (targetRoadmapOrder < 1 || targetRoadmapOrder > orderedEpics.Count)
        {
            return CreateIssueResult(
                state,
                new PlanningValidationIssue(
                    PlanningValidationIssueCode.InvalidOperationInput,
                    $"ReorderEpic requires a target roadmap order between 1 and {orderedEpics.Count}.",
                    epicId));
        }

        var targetIndex = targetRoadmapOrder - 1;
        if (targetIndex == epicIndex)
        {
            return new PlanningOperationResult(state, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<PlanningValidationIssue>());
        }

        var movedEpic = orderedEpics[epicIndex];
        orderedEpics.RemoveAt(epicIndex);
        orderedEpics.Insert(targetIndex, movedEpic);

        var reorderedEpics = orderedEpics
            .Select((epic, index) => epic with { RoadmapOrder = index + 1 })
            .ToArray();

        var recomputeFromIndex = Math.Min(epicIndex, targetIndex);
        var affectedEpicIds = reorderedEpics
            .Skip(recomputeFromIndex)
            .Select(epic => epic.EpicId)
            .ToArray();

        return FinalizeOperation(state, reorderedEpics, recomputeFromIndex, affectedEpicIds);
    }

    /// <summary>
    /// Shifts the selected epic and every later roadmap epic right by the requested number of sprints.
    /// </summary>
    public PlanningOperationResult ShiftPlan(PlanningState state, int epicId, int deltaSprints)
    {
        ArgumentNullException.ThrowIfNull(state);

        var baselineIssues = _validationService.Validate(state);
        if (baselineIssues.Count > 0)
        {
            return CreateIssueResult(state, baselineIssues);
        }

        var orderedEpics = PlanningStateOrdering.OrderEpics(state).ToArray();
        var epicIndex = Array.FindIndex(orderedEpics, epic => epic.EpicId == epicId);
        if (epicIndex < 0)
        {
            return CreateIssueResult(
                state,
                new PlanningValidationIssue(
                    PlanningValidationIssueCode.EpicNotFound,
                    $"Epic '{epicId}' was not found in planning state.",
                    epicId));
        }

        if (deltaSprints <= 0)
        {
            return CreateIssueResult(
                state,
                new PlanningValidationIssue(
                    PlanningValidationIssueCode.InvalidOperationInput,
                    "ShiftPlan requires a positive delta.",
                    epicId));
        }

        for (var index = epicIndex; index < orderedEpics.Length; index++)
        {
            orderedEpics[index] = orderedEpics[index] with
            {
                PlannedStartSprintIndex = orderedEpics[index].PlannedStartSprintIndex + deltaSprints,
            };
        }

        var affectedEpicIds = orderedEpics
            .Skip(epicIndex)
            .Select(epic => epic.EpicId)
            .ToArray();

        return FinalizeOperation(state, orderedEpics, epicIndex, affectedEpicIds);
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
            return CreateIssueResult(state, baselineIssues);
        }

        if (upfrontIssue is not null)
        {
            return CreateIssueResult(state, upfrontIssue);
        }

        var orderedEpics = PlanningStateOrdering.OrderEpics(state).ToArray();
        var epicIndex = Array.FindIndex(orderedEpics, epic => epic.EpicId == epicId);
        if (epicIndex < 0)
        {
            return CreateIssueResult(
                state,
                new PlanningValidationIssue(
                    PlanningValidationIssueCode.EpicNotFound,
                    $"Epic '{epicId}' was not found in planning state.",
                    epicId));
        }

        orderedEpics[epicIndex] = mutateEpic(orderedEpics, epicIndex);

        var affectedEpicIds = PlanningStateOrdering
            .OrderEpics(new PlanningState(orderedEpics))
            .Skip(epicIndex)
            .Select(epic => epic.EpicId)
            .ToArray();

        return FinalizeOperation(state, orderedEpics, epicIndex, affectedEpicIds);
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

    private PlanningOperationResult FinalizeOperation(
        PlanningState previousState,
        IReadOnlyList<PlanningEpicState> orderedEpics,
        int recomputeFromIndex,
        IReadOnlyList<int> affectedEpicIds)
    {
        var updatedState = _recomputeService.RecomputeFrom(new PlanningState(orderedEpics.ToArray()), recomputeFromIndex);
        var validationIssues = _validationService.Validate(updatedState);
        var changedEpicIds = GetChangedEpicIds(previousState, updatedState);

        return new PlanningOperationResult(updatedState, changedEpicIds, affectedEpicIds, validationIssues);
    }

    private static PlanningOperationResult CreateIssueResult(
        PlanningState state,
        IReadOnlyList<PlanningValidationIssue> validationIssues)
    {
        return new PlanningOperationResult(state, Array.Empty<int>(), Array.Empty<int>(), validationIssues);
    }

    private static PlanningOperationResult CreateIssueResult(
        PlanningState state,
        params PlanningValidationIssue[] validationIssues)
    {
        return new PlanningOperationResult(state, Array.Empty<int>(), Array.Empty<int>(), validationIssues);
    }
}
