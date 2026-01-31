namespace PoTool.Shared.Settings;

/// <summary>
/// DTO for a TFS/Azure DevOps project retrieved from the server.
/// Maps to the TFS projects API response.
/// </summary>
public sealed record TfsProjectDto(
    string Id,
    string Name,
    string? Description
);
