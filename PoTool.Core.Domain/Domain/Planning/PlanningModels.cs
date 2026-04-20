namespace PoTool.Core.Domain.Planning;

/// <summary>
/// Canonical planning-engine state for one roadmap epic.
/// </summary>
public sealed record PlanningEpicState(
    int EpicId,
    int RoadmapOrder,
    int PlannedStartSprintIndex,
    int ComputedStartSprintIndex,
    int DurationInSprints = 1,
    int TrackIndex = 0)
{
    /// <summary>
    /// Gets the exclusive end sprint index.
    /// </summary>
    public int EndSprintIndexExclusive => ComputedStartSprintIndex + DurationInSprints;
}

/// <summary>
/// Canonical planning-engine state for the current roadmap scope.
/// </summary>
public sealed record PlanningState(IReadOnlyList<PlanningEpicState> Epics)
{
    /// <summary>
    /// Gets an empty planning state.
    /// </summary>
    public static PlanningState Empty { get; } = new(Array.Empty<PlanningEpicState>());
}

/// <summary>
/// Operation result for deterministic planning-engine mutations.
/// </summary>
public sealed record PlanningOperationResult(
    PlanningState State,
    IReadOnlyList<int> ChangedEpicIds,
    IReadOnlyList<int> AffectedEpicIds,
    IReadOnlyList<PlanningValidationIssue> ValidationIssues);

/// <summary>
/// Stable planning validation codes for hard-constraint failures.
/// </summary>
public enum PlanningValidationIssueCode
{
    EpicNotFound,
    InvalidOperationInput,
    DuplicateEpicId,
    InvalidDuration,
    NegativePlannedStart,
    InvalidTrackIndex,
    InvalidRoadmapOrder,
    RoadmapStartOrderViolation,
    SameTrackOverlap,
}

/// <summary>
/// Hard-constraint validation issue for planning state or operations.
/// </summary>
public sealed record PlanningValidationIssue(
    PlanningValidationIssueCode Code,
    string Message,
    int? EpicId = null);
