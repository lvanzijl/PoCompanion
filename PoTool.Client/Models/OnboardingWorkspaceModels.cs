using PoTool.Shared.Onboarding;

namespace PoTool.Client.Models;

public static class FeatureFlagKeys
{
    public const string OnboardingWorkspace = "OnboardingWorkspace";
}

public sealed record OnboardingWorkspaceFilter(
    int? ConnectionId,
    int? ProjectId,
    int? ProductRootId,
    OnboardingConfigurationStatus? Status);

public sealed record OnboardingFilterOption<TValue>(
    TValue? Value,
    string Label)
    where TValue : struct;

public sealed record OnboardingWorkspaceFilterOptions(
    IReadOnlyList<OnboardingFilterOption<int>> Connections,
    IReadOnlyList<OnboardingFilterOption<int>> Projects,
    IReadOnlyList<OnboardingFilterOption<int>> ProductRoots,
    IReadOnlyList<OnboardingFilterOption<OnboardingConfigurationStatus>> Statuses);

public sealed record OnboardingWorkspaceData(
    OnboardingWorkspaceFilter AppliedFilter,
    OnboardingWorkspaceFilterOptions FilterOptions,
    OnboardingStatusDto Status,
    IReadOnlyList<OnboardingTfsConnectionDto> Connections,
    IReadOnlyList<OnboardingProjectSourceDto> Projects,
    IReadOnlyList<OnboardingTeamSourceDto> Teams,
    IReadOnlyList<OnboardingPipelineSourceDto> Pipelines,
    IReadOnlyList<OnboardingProductRootDto> ProductRoots,
    IReadOnlyList<OnboardingProductSourceBindingDto> Bindings);

public enum OnboardingWorkspaceLoadState
{
    Loading,
    Ready,
    Empty,
    Failed
}

public sealed record OnboardingSummaryViewModel(
    OnboardingConfigurationStatus OverallStatus,
    OnboardingConfigurationStatus ConnectionStatus,
    OnboardingConfigurationStatus DataSourceSetupStatus,
    OnboardingConfigurationStatus DomainConfigurationStatus,
    IReadOnlyList<OnboardingStatusIssueDto> Blockers,
    IReadOnlyList<OnboardingStatusIssueDto> Warnings,
    OnboardingStatusCountsDto Counts,
    bool HasVisibleData,
    bool HasBlockingIssues,
    bool HasWarnings);

public sealed record OnboardingProjectContextViewModel(
    int ProjectId,
    string ProjectExternalId,
    string ProjectName);

public sealed record OnboardingRootContextViewModel(
    int RootId,
    string WorkItemExternalId,
    string Title,
    string ProjectExternalId);

public sealed record OnboardingProjectGroupViewModel<TItem>(
    OnboardingProjectContextViewModel Project,
    IReadOnlyList<TItem> Items);

public sealed record OnboardingBindingGroupViewModel(
    OnboardingRootContextViewModel Root,
    IReadOnlyList<OnboardingProductSourceBindingDto> Bindings);

public sealed record OnboardingWorkspaceViewModel(
    OnboardingWorkspaceLoadState LoadState,
    bool IsReadOnly,
    string Title,
    string Message,
    OnboardingWorkspaceFilter AppliedFilter,
    OnboardingWorkspaceFilterOptions FilterOptions,
    OnboardingSummaryViewModel? Summary,
    IReadOnlyList<OnboardingTfsConnectionDto> Connections,
    IReadOnlyList<OnboardingProjectSourceDto> Projects,
    IReadOnlyList<OnboardingProjectGroupViewModel<OnboardingTeamSourceDto>> TeamGroups,
    IReadOnlyList<OnboardingProjectGroupViewModel<OnboardingPipelineSourceDto>> PipelineGroups,
    IReadOnlyList<OnboardingProjectGroupViewModel<OnboardingProductRootDto>> ProductRootGroups,
    IReadOnlyList<OnboardingBindingGroupViewModel> BindingGroups,
    string? ErrorMessage);
