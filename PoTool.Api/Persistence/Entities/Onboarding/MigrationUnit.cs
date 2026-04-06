namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class MigrationUnit : OnboardingEntityBase
{
    public Guid UnitIdentifier { get; set; } = Guid.NewGuid();

    public int MigrationRunId { get; set; }

    public string UnitType { get; set; } = string.Empty;

    public string UnitName { get; set; } = string.Empty;

    public int ExecutionOrder { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public OnboardingMigrationUnitStatus Status { get; set; } = OnboardingMigrationUnitStatus.Pending;

    public int ProcessedEntityCount { get; set; }

    public int SucceededEntityCount { get; set; }

    public int FailedEntityCount { get; set; }

    public int SkippedEntityCount { get; set; }

    public MigrationRun MigrationRun { get; set; } = null!;

    public ICollection<MigrationIssue> Issues { get; set; } = new List<MigrationIssue>();
}
