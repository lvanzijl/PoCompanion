namespace PoTool.Shared.BugTriage;

/// <summary>
/// DTO for bug triage state.
/// Represents whether a bug has been triaged and when.
/// </summary>
public sealed record BugTriageStateDto(
    int BugId,
    DateTimeOffset FirstSeenAt,
    string FirstObservedCriticality,
    bool IsTriaged,
    DateTimeOffset? LastTriageActionAt
);

/// <summary>
/// Request DTO for marking a bug as triaged or updating its triage state.
/// </summary>
public sealed record UpdateBugTriageStateRequest(
    int BugId,
    string? NewCriticality = null,
    List<string>? TagsAdded = null,
    List<string>? TagsRemoved = null
);

/// <summary>
/// Response for triage state update (confirmation).
/// </summary>
public sealed record UpdateBugTriageStateResponse(
    bool Success,
    string? Message = null
);
