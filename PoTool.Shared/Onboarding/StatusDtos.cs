namespace PoTool.Shared.Onboarding;

public enum OnboardingConfigurationStatus
{
    NotConfigured,
    PartiallyConfigured,
    Complete
}

public sealed record OnboardingStatusIssueDto(
    string Code,
    string Message,
    string? EntityType,
    string? EntityExternalId);

public sealed record OnboardingStatusCountsDto(
    int ProjectSourcesTotal,
    int ProjectSourcesValid,
    int TeamSourcesTotal,
    int TeamSourcesValid,
    int PipelineSourcesTotal,
    int PipelineSourcesValid,
    int ProductRootsTotal,
    int ProductRootsValid,
    int BindingsTotal,
    int BindingsValid);

public sealed record OnboardingStatusDto(
    OnboardingConfigurationStatus OverallStatus,
    OnboardingConfigurationStatus ConnectionStatus,
    OnboardingConfigurationStatus DataSourceSetupStatus,
    OnboardingConfigurationStatus DomainConfigurationStatus,
    IReadOnlyList<OnboardingStatusIssueDto> BlockingReasons,
    IReadOnlyList<OnboardingStatusIssueDto> Warnings,
    OnboardingStatusCountsDto Counts);
