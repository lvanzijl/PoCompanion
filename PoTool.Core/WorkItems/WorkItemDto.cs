namespace PoTool.Core.WorkItems;

/// <summary>
/// Immutable DTO for work items.
/// </summary>
public sealed record WorkItemDto(
    int TfsId,
    string Type,
    string Title,
    string AreaPath,
    string IterationPath,
    string State,
    string JsonPayload,
    DateTimeOffset RetrievedAt
);
