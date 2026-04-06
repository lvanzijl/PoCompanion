namespace PoTool.Shared.Onboarding;

public sealed record OnboardingEntityStatusDto(
    OnboardingConfigurationStatus Status,
    IReadOnlyList<OnboardingStatusIssueDto> BlockingReasons,
    IReadOnlyList<OnboardingStatusIssueDto> Warnings);

public sealed record OnboardingAuditDto(
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? DeletedAtUtc,
    string? DeletionReason);

public sealed record OnboardingTfsConnectionDto(
    int Id,
    string ConnectionKey,
    string OrganizationUrl,
    string AuthenticationMode,
    int TimeoutSeconds,
    string ApiVersion,
    TfsConnectionValidationResultDto Validation,
    OnboardingEntityStatusDto Status,
    OnboardingAuditDto Audit);

public sealed record OnboardingProjectSourceDto(
    int Id,
    int TfsConnectionId,
    string ProjectExternalId,
    bool Enabled,
    ProjectSnapshotDto Snapshot,
    OnboardingValidationStateDto ValidationState,
    OnboardingEntityStatusDto Status,
    OnboardingAuditDto Audit);

public sealed record OnboardingTeamSourceDto(
    int Id,
    int ProjectSourceId,
    string TeamExternalId,
    bool Enabled,
    TeamSnapshotDto Snapshot,
    OnboardingValidationStateDto ValidationState,
    OnboardingEntityStatusDto Status,
    OnboardingAuditDto Audit);

public sealed record OnboardingPipelineSourceDto(
    int Id,
    int ProjectSourceId,
    string PipelineExternalId,
    bool Enabled,
    PipelineSnapshotDto Snapshot,
    OnboardingValidationStateDto ValidationState,
    OnboardingEntityStatusDto Status,
    OnboardingAuditDto Audit);

public sealed record OnboardingProductRootDto(
    int Id,
    int ProjectSourceId,
    string WorkItemExternalId,
    bool Enabled,
    ProductRootSnapshotDto Snapshot,
    OnboardingValidationStateDto ValidationState,
    OnboardingEntityStatusDto Status,
    OnboardingAuditDto Audit);

public sealed record OnboardingProductSourceBindingDto(
    int Id,
    int ProductRootId,
    int ProjectSourceId,
    int? TeamSourceId,
    int? PipelineSourceId,
    OnboardingProductSourceTypeDto SourceType,
    string SourceExternalId,
    bool Enabled,
    OnboardingValidationStateDto ValidationState,
    OnboardingEntityStatusDto Status,
    OnboardingAuditDto Audit);

public sealed record CreateTfsConnectionRequest(
    string OrganizationUrl,
    string AuthenticationMode,
    int TimeoutSeconds,
    string ApiVersion);

public sealed record UpdateTfsConnectionRequest(
    string? AuthenticationMode,
    int? TimeoutSeconds,
    string? ApiVersion,
    string? ConnectionKey,
    string? OrganizationUrl);

public sealed record CreateProjectSourceRequest(
    int TfsConnectionId,
    string ProjectExternalId,
    bool Enabled = true,
    string? Name = null,
    string? Description = null);

public sealed record UpdateProjectSourceRequest(
    bool? Enabled,
    string? Name,
    string? Description,
    int? TfsConnectionId,
    string? ProjectExternalId);

public sealed record CreateTeamSourceRequest(
    int ProjectSourceId,
    string TeamExternalId,
    bool Enabled = true,
    string? Name = null,
    string? DefaultAreaPath = null,
    string? Description = null);

public sealed record UpdateTeamSourceRequest(
    bool? Enabled,
    string? Name,
    string? DefaultAreaPath,
    string? Description,
    int? ProjectSourceId,
    string? TeamExternalId);

public sealed record CreatePipelineSourceRequest(
    int ProjectSourceId,
    string PipelineExternalId,
    bool Enabled = true,
    string? Name = null,
    string? Folder = null,
    string? YamlPath = null,
    string? RepositoryExternalId = null,
    string? RepositoryName = null);

public sealed record UpdatePipelineSourceRequest(
    bool? Enabled,
    string? Name,
    string? Folder,
    string? YamlPath,
    string? RepositoryExternalId,
    string? RepositoryName,
    int? ProjectSourceId,
    string? PipelineExternalId);

public sealed record CreateProductRootRequest(
    int ProjectSourceId,
    string WorkItemExternalId,
    bool Enabled = true,
    string? Title = null,
    string? WorkItemType = null,
    string? State = null,
    string? AreaPath = null);

public sealed record UpdateProductRootRequest(
    bool? Enabled,
    string? Title,
    string? WorkItemType,
    string? State,
    string? AreaPath,
    int? ProjectSourceId,
    string? WorkItemExternalId);

public sealed record CreateProductSourceBindingRequest(
    int ProductRootId,
    OnboardingProductSourceTypeDto SourceType,
    int? TeamSourceId = null,
    int? PipelineSourceId = null,
    int? ProjectSourceId = null,
    bool Enabled = true);

public sealed record UpdateProductSourceBindingRequest(
    bool? Enabled,
    int? ProductRootId,
    int? ProjectSourceId,
    int? TeamSourceId,
    int? PipelineSourceId,
    OnboardingProductSourceTypeDto? SourceType,
    string? SourceExternalId);

public sealed record OnboardingSoftDeleteRequest(string Reason);

public sealed record OnboardingSoftDeleteResultDto(
    int Id,
    DateTime DeletedAtUtc,
    string DeletionReason);
