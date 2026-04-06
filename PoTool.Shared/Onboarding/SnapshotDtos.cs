namespace PoTool.Shared.Onboarding;

public sealed record ProjectSnapshotDto(
    string ProjectExternalId,
    string Name,
    string? Description,
    SnapshotMetadataDto Metadata);

public sealed record TeamSnapshotDto(
    string TeamExternalId,
    string ProjectExternalId,
    string Name,
    string DefaultAreaPath,
    string? Description,
    SnapshotMetadataDto Metadata);

public sealed record PipelineSnapshotDto(
    string PipelineExternalId,
    string ProjectExternalId,
    string Name,
    string? Folder,
    string? YamlPath,
    string? RepositoryExternalId,
    string? RepositoryName,
    SnapshotMetadataDto Metadata);

public sealed record ProductRootSnapshotDto(
    string WorkItemExternalId,
    string Title,
    string WorkItemType,
    string State,
    string ProjectExternalId,
    string AreaPath,
    SnapshotMetadataDto Metadata);
