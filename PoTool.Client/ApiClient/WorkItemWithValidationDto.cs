namespace PoTool.Client.ApiClient;

/// <summary>
/// Work item DTO with validation issues.
/// Must match the server-side DTO structure.
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
    List<ValidationIssueDto> ValidationIssues
);

/// <summary>
/// Validation issue DTO.
/// Must match the server-side DTO structure.
/// </summary>
public sealed record ValidationIssueDto(
    string Severity,
    string Message
);
