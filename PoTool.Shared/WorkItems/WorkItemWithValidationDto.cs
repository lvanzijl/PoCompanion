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
    DateTimeOffset RetrievedAt,
    int? Effort,
    string? Description,
    List<ValidationIssue> ValidationIssues,
    DateTimeOffset? CreatedDate = null,
    DateTimeOffset? ClosedDate = null,
    string? Severity = null,
    string? Tags = null,
    bool? IsBlocked = null,
    int? BusinessValue = null,
    double? BacklogPriority = null
);
