namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class OnboardingSnapshotMetadata
{
    public OnboardingSnapshotMetadata()
    {
        var utcNow = DateTime.UtcNow;
        ConfirmedAtUtc = utcNow;
        LastSeenAtUtc = utcNow;
    }

    public DateTime ConfirmedAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }

    public bool IsCurrent { get; set; }

    public bool RenameDetected { get; set; }

    public string? StaleReason { get; set; }
}
