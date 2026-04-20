namespace PoTool.Shared.Planning;

/// <summary>
/// Request contract for product planning operations that target a single epic.
/// </summary>
public sealed record ProductPlanningEpicRequest(
    int EpicId);

/// <summary>
/// Request contract for product planning operations that target a single epic and delta.
/// </summary>
public sealed record ProductPlanningEpicDeltaRequest(
    int EpicId,
    int DeltaSprints);

/// <summary>
/// Request contract for roadmap reorder operations on the product planning board.
/// </summary>
public sealed record ReorderProductPlanningEpicRequest(
    int EpicId,
    int TargetRoadmapOrder);
