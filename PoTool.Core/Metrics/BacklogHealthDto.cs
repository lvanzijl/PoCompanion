namespace PoTool.Core.Metrics;

/// <summary>
/// DTO representing backlog health metrics across one or more iterations.
/// Provides insights into validation issues, progress blockers, and sprint health.
/// </summary>
public sealed record BacklogHealthDto(
    string IterationPath,
    string SprintName,
    int TotalWorkItems,
    int WorkItemsWithoutEffort,
    int WorkItemsInProgressWithoutEffort,
    int ParentProgressIssues,
    int BlockedItems,
    int InProgressAtIterationEnd,
    DateTimeOffset? IterationStart,
    DateTimeOffset? IterationEnd,
    IReadOnlyList<ValidationIssueSummary> ValidationIssues
);

/// <summary>
/// Summary of validation issues for a specific validation type.
/// </summary>
public sealed record ValidationIssueSummary(
    string ValidationType,
    int Count,
    IReadOnlyList<int> AffectedWorkItemIds
);
