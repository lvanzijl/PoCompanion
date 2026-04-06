namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class ProjectSnapshot
{
    public string ProjectExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public OnboardingSnapshotMetadata Metadata { get; set; } = new();
}
