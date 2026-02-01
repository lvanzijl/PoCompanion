namespace PoTool.Shared.BugTriage;

/// <summary>
/// DTO for a triage tag in the catalog.
/// </summary>
public sealed record TriageTagDto(
    int Id,
    string Name,
    bool IsEnabled,
    int DisplayOrder,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Request DTO for creating a new triage tag.
/// </summary>
public sealed record CreateTriageTagRequest(
    string Name
);

/// <summary>
/// Request DTO for updating a triage tag.
/// </summary>
public sealed record UpdateTriageTagRequest(
    int Id,
    string? Name = null,
    bool? IsEnabled = null,
    int? DisplayOrder = null
);

/// <summary>
/// Response for triage tag operations.
/// </summary>
public sealed record TriageTagOperationResponse(
    bool Success,
    string? Message = null,
    TriageTagDto? Tag = null
);
