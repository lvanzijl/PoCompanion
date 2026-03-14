namespace PoTool.Shared.Metrics;

/// <summary>
/// Historical sprint metrics DTO.
/// PlannedStoryPoints reflects reconstructed committed scope at the sprint commitment timestamp,
/// while CompletedStoryPoints and completion counts reflect scope items whose first canonical Done transition
/// happened inside the sprint window.
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
