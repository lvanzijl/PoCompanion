namespace PoTool.Api.Persistence.Entities.Onboarding;

public enum OnboardingMigrationRunStatus
{
    NotStarted,
    Running,
    Succeeded,
    Failed,
    PartiallySucceeded,
    Cancelled
}
