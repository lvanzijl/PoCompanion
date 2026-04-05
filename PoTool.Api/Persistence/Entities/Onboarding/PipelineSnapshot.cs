namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class PipelineSnapshot
{
    public string PipelineExternalId { get; set; } = string.Empty;

    public string ProjectExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Folder { get; set; }

    public string? YamlPath { get; set; }

    public string? RepositoryExternalId { get; set; }

    public string? RepositoryName { get; set; }

    public OnboardingSnapshotMetadata Metadata { get; set; } = new();
}
