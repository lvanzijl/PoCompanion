namespace PoTool.Shared.Settings;

/// <summary>
/// DTO for a TFS/Azure DevOps team retrieved from the server.
/// Maps to the TFS teams API response.
/// </summary>
public sealed record TfsTeamDto(
    string Id,
    string Name,
    string ProjectName,
    string? Description,
    string DefaultAreaPath
);
