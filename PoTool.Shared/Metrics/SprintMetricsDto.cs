namespace PoTool.Shared.Metrics;

/// <summary>
/// Sprint-level metrics DTO for velocity tracking and sprint analysis.
/// </summary>
public sealed record SprintMetricsDto(
    string IterationPath,
    string SprintName,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    int CompletedStoryPoints,
    int PlannedStoryPoints,
    int CompletedWorkItemCount,
    int TotalWorkItemCount,
    int CompletedPBIs,
    int CompletedBugs,
    int CompletedTasks
);
