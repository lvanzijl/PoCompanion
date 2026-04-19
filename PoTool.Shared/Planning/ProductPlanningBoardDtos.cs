namespace PoTool.Shared.Planning;

/// <summary>
/// Read model for a single product planning board built from the planning engine.
/// </summary>
public sealed record ProductPlanningBoardDto(
    int ProductId,
    string ProductName,
    IReadOnlyList<PlanningBoardTrackDto> Tracks,
    IReadOnlyList<PlanningBoardEpicItemDto> EpicItems,
    IReadOnlyList<PlanningBoardIssueDto> Issues,
    IReadOnlyList<int> ChangedEpicIds,
    IReadOnlyList<int> AffectedEpicIds);

/// <summary>
/// A track on the planning board.
/// </summary>
public sealed record PlanningBoardTrackDto(
    int TrackIndex,
    bool IsMainLane,
    IReadOnlyList<int> EpicIds);

/// <summary>
/// A read-model epic item for planning board consumers.
/// </summary>
public sealed record PlanningBoardEpicItemDto(
    int EpicId,
    string EpicTitle,
    int RoadmapOrder,
    int TrackIndex,
    int PlannedStartSprintIndex,
    int ComputedStartSprintIndex,
    int DurationInSprints,
    int EndSprintIndexExclusive,
    IReadOnlyList<PlanningBoardIssueDto> Issues,
    bool IsChanged,
    bool IsAffected);

/// <summary>
/// A surfaced planning issue for read-model consumers.
/// </summary>
public sealed record PlanningBoardIssueDto(
    string Severity,
    string Code,
    string Message,
    int? EpicId);
