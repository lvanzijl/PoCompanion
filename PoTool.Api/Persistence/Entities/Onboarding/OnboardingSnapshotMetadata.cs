namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class OnboardingSnapshotMetadata
{
    public DateTime ConfirmedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsCurrent { get; set; }

    public bool RenameDetected { get; set; }

    public string? StaleReason { get; set; }
}
