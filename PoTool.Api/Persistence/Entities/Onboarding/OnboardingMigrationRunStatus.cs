namespace PoTool.Api.Persistence.Entities.Onboarding;

public enum OnboardingMigrationRunStatus
{
    NotStarted,
    Running,
    NoOp,
    Succeeded,
    Failed,
    PartiallySucceeded,
    Cancelled
}
