namespace PoTool.Shared.Settings;

/// <summary>
/// DTO for Git repository information from TFS/Azure DevOps.
/// Used during onboarding and product configuration to display available repositories.
/// </summary>
public sealed record GitRepositoryInfoDto(
    string Name,
    string Id
);
