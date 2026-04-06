using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public sealed record OnboardingMigrationRunCreateRequest(
    string MigrationVersion,
    string EnvironmentRing,
    string TriggerType,
    OnboardingMigrationExecutionMode ExecutionMode,
    string? SourceFingerprint);

public sealed record OnboardingMigrationUnitPlan(
    string UnitType,
    string UnitName,
    int ExecutionOrder);

public sealed record OnboardingMigrationUnitOutcome(
    int ProcessedEntityCount,
    int SucceededEntityCount,
    int FailedEntityCount,
    int SkippedEntityCount);

public sealed record OnboardingMigrationIssueCreateRequest(
    Guid? UnitIdentifier,
    string IssueType,
    string IssueCategory,
    OnboardingMigrationIssueSeverity Severity,
    string SourceLegacyReference,
    string TargetEntityType,
    string? TargetExternalIdentity,
    string SanitizedMessage,
    string? SanitizedDetails,
    bool IsBlocking);

public sealed record OnboardingMigrationDryRunIssuePlan(
    string IssueType,
    string IssueCategory,
    OnboardingMigrationIssueSeverity Severity,
    string SourceLegacyReference,
    string TargetEntityType,
    string? TargetExternalIdentity,
    string SanitizedMessage,
    string? SanitizedDetails,
    bool IsBlocking);

public sealed record OnboardingMigrationDryRunUnitPlan(
    string UnitType,
    string UnitName,
    int ExecutionOrder,
    OnboardingMigrationUnitStatus FinalStatus,
    OnboardingMigrationUnitOutcome Outcome,
    IReadOnlyList<OnboardingMigrationDryRunIssuePlan> Issues);

public sealed record OnboardingMigrationDryRunRequest(
    string MigrationVersion,
    string EnvironmentRing,
    string TriggerType,
    string? SourceFingerprint,
    IReadOnlyList<OnboardingMigrationDryRunUnitPlan> Units);

public sealed record OnboardingMigrationRunSummary(
    Guid RunIdentifier,
    string MigrationVersion,
    string EnvironmentRing,
    string TriggerType,
    OnboardingMigrationExecutionMode ExecutionMode,
    OnboardingMigrationRunStatus Status,
    DateTime? StartedAtUtc,
    DateTime? FinishedAtUtc,
    int TotalUnitCount,
    int SucceededUnitCount,
    int FailedUnitCount,
    int SkippedUnitCount,
    int ProcessedEntityCount,
    int SucceededEntityCount,
    int FailedEntityCount,
    int SkippedEntityCount,
    int IssueCount,
    int BlockingIssueCount,
    IReadOnlyList<OnboardingMigrationUnitSummary> Units,
    IReadOnlyList<OnboardingMigrationIssueSummary> Issues);

public sealed record OnboardingMigrationUnitSummary(
    Guid UnitIdentifier,
    string UnitType,
    string UnitName,
    int ExecutionOrder,
    OnboardingMigrationUnitStatus Status,
    DateTime? StartedAtUtc,
    DateTime? FinishedAtUtc,
    int ProcessedEntityCount,
    int SucceededEntityCount,
    int FailedEntityCount,
    int SkippedEntityCount);

public sealed record OnboardingMigrationIssueSummary(
    Guid IssueIdentifier,
    Guid RunIdentifier,
    Guid? UnitIdentifier,
    string IssueType,
    string IssueCategory,
    OnboardingMigrationIssueSeverity Severity,
    string SourceLegacyReference,
    string TargetEntityType,
    string? TargetExternalIdentity,
    string SanitizedMessage,
    string? SanitizedDetails,
    bool IsBlocking,
    DateTime CreatedAtUtc);
