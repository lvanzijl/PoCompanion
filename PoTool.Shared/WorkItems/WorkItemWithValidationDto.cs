namespace PoTool.Shared.WorkItems;

/// <summary>
/// Work item DTO with validation issues attached.
/// </summary>
public sealed record WorkItemWithValidationDto(
    int TfsId,
    string Type,
    string Title,
    int? ParentTfsId,
    string AreaPath,
    string IterationPath,
    string State,
    string JsonPayload,
    DateTimeOffset RetrievedAt,
    int? Effort,
    List<ValidationIssue> ValidationIssues
);
