namespace PoTool.Core.Domain.Cdc.Sprints;

/// <summary>
/// Canonical sprint story-point totals produced by the SprintCommitment CDC slice.
/// </summary>
public sealed record SprintFactResult(
    double CommittedStoryPoints,
    double AddedStoryPoints,
    double RemovedStoryPoints,
    double DeliveredStoryPoints,
    double DeliveredFromAddedStoryPoints,
    double SpilloverStoryPoints,
    double RemainingStoryPoints);
