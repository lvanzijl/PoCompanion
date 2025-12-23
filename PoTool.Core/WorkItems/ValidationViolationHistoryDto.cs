namespace PoTool.Core.WorkItems;

/// <summary>
/// DTO representing a historical validation violation record.
/// Used for tracking violation patterns over time.
/// </summary>
public sealed record ValidationViolationHistoryDto(
    int WorkItemId,
    string WorkItemType,
    string WorkItemTitle,
    string ValidationType,
    string Severity,
    string ViolationMessage,
    string AreaPath,
    string IterationPath,
    DateTimeOffset DetectedAt
);
