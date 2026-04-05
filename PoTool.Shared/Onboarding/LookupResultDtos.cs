namespace PoTool.Shared.Onboarding;

public sealed record ProjectLookupResultDto(
    string ProjectExternalId,
    string Name,
    string? Description);

public sealed record TeamLookupResultDto(
    string TeamExternalId,
    string ProjectExternalId,
    string Name,
    string? Description,
    string DefaultAreaPath);

public sealed record PipelineLookupResultDto(
    string PipelineExternalId,
    string ProjectExternalId,
    string Name,
    string? Folder,
    string? YamlPath,
    string? RepositoryExternalId,
    string? RepositoryName);

public sealed record WorkItemLookupResultDto(
    string WorkItemExternalId,
    string Title,
    string WorkItemType,
    string State,
    string ProjectExternalId,
    string AreaPath);
