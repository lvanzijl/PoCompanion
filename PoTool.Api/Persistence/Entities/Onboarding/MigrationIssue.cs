namespace PoTool.Api.Persistence.Entities.Onboarding;

public sealed class MigrationIssue : OnboardingEntityBase
{
    public Guid IssueIdentifier { get; set; } = Guid.NewGuid();

    public int MigrationRunId { get; set; }

    public int? MigrationUnitId { get; set; }

    public string IssueType { get; set; } = string.Empty;

    public string IssueCategory { get; set; } = string.Empty;

    public OnboardingMigrationIssueSeverity Severity { get; set; }

    public string SourceLegacyReference { get; set; } = string.Empty;

    public string TargetEntityType { get; set; } = string.Empty;

    public string? TargetExternalIdentity { get; set; }

    public string SanitizedMessage { get; set; } = string.Empty;

    public string? SanitizedDetails { get; set; }

    public bool IsBlocking { get; set; }

    public MigrationRun MigrationRun { get; set; } = null!;

    public MigrationUnit? MigrationUnit { get; set; }
}
