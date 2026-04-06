namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class MigrationRun : OnboardingEntityBase
{
    public Guid RunIdentifier { get; set; } = Guid.NewGuid();

    public string MigrationVersion { get; set; } = string.Empty;

    public string EnvironmentRing { get; set; } = string.Empty;

    public string TriggerType { get; set; } = string.Empty;

    public OnboardingMigrationExecutionMode ExecutionMode { get; set; }

    public string? SourceFingerprint { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public OnboardingMigrationRunStatus Status { get; set; } = OnboardingMigrationRunStatus.NotStarted;

    public int TotalUnitCount { get; set; }

    public int SucceededUnitCount { get; set; }

    public int FailedUnitCount { get; set; }

    public int SkippedUnitCount { get; set; }

    public int ProcessedEntityCount { get; set; }

    public int SucceededEntityCount { get; set; }

    public int FailedEntityCount { get; set; }

    public int SkippedEntityCount { get; set; }

    public int IssueCount { get; set; }

    public int BlockingIssueCount { get; set; }

    public ICollection<MigrationUnit> Units { get; set; } = new List<MigrationUnit>();

    public ICollection<MigrationIssue> Issues { get; set; } = new List<MigrationIssue>();
}
