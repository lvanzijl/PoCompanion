namespace PoTool.Shared.Onboarding;

public sealed record TfsConnectionValidationResultDto(
    string OrganizationUrl,
    string AuthenticationMode,
    int TimeoutSeconds,
    string ApiVersion,
    OnboardingValidationStateDto AvailabilityValidationState,
    OnboardingValidationStateDto PermissionValidationState,
    OnboardingValidationStateDto CapabilityValidationState,
    DateTime? LastSuccessfulValidationAtUtc,
    DateTime LastAttemptedValidationAtUtc,
    string? ValidationFailureReason,
    string? LastVerifiedCapabilitiesSummary);

public sealed record ProjectSourceValidationResultDto(
    string ProjectExternalId,
    ProjectSnapshotDto Snapshot,
    OnboardingValidationStateDto ValidationState);

public sealed record TeamSourceValidationResultDto(
    string TeamExternalId,
    string ProjectExternalId,
    TeamSnapshotDto Snapshot,
    OnboardingValidationStateDto ValidationState);

public sealed record PipelineSourceValidationResultDto(
    string PipelineExternalId,
    string ProjectExternalId,
    PipelineSnapshotDto Snapshot,
    OnboardingValidationStateDto ValidationState);

public sealed record ProductRootValidationResultDto(
    string WorkItemExternalId,
    ProductRootSnapshotDto Snapshot,
    OnboardingValidationStateDto ValidationState);

public sealed record ProductSourceBindingValidationResultDto(
    string WorkItemExternalId,
    OnboardingProductSourceTypeDto SourceType,
    string SourceExternalId,
    string ProjectExternalId,
    OnboardingValidationStateDto ValidationState);
