namespace PoTool.Client.ApiClient;

/// <summary>
/// Work item DTO returned from API.
/// Generated from OpenAPI specification.
/// </summary>
public sealed record WorkItemDto(
    int TfsId,
    string Type,
    string Title,
    int? ParentTfsId,
    string AreaPath,
    string IterationPath,
    string State,
    string JsonPayload,
    DateTimeOffset RetrievedAt
);
