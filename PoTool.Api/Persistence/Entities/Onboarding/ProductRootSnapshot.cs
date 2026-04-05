namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class ProductRootSnapshot
{
    public string WorkItemExternalId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string WorkItemType { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string ProjectExternalId { get; set; } = string.Empty;

    public string AreaPath { get; set; } = string.Empty;

    public OnboardingSnapshotMetadata Metadata { get; set; } = new();
}
