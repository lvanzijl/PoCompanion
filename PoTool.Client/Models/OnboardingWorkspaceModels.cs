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

public enum OnboardingWorkspaceViewMode
{
    Graph,
    Problems
}

public enum OnboardingProblemSeverity
{
    Blocking,
    Warning
}

public enum OnboardingProblemScope
{
    Global,
    Project,
    Root,
    Binding
}

public enum OnboardingGraphSection
{
    Connections,
    Projects,
    Teams,
    Pipelines,
    ProductRoots,
    Bindings
}

public enum OnboardingExecutionConfidenceLevel
{
    High,
    Medium,
    Fallback
}

public static class OnboardingExecutionIntentQueryKeys
{
    public const string ConnectionId = "onboardingConnectionId";
    public const string ProjectId = "onboardingProjectId";
    public const string RootId = "onboardingRootId";
    public const string BindingId = "onboardingBindingId";
    public const string Section = "onboardingSection";
    public const string TargetElementId = "onboardingTarget";
    public const string IntentType = "onboardingIntentType";
}

public sealed record ExecutionIntentNavigationTargetViewModel(
    string Route,
    string AnchorId,
    OnboardingGraphSection Section,
    string TargetElementId,
    IReadOnlyList<OnboardingGraphSection> ExpandedSections);

public sealed record ExecutionIntentViewModel(
    string IntentType,
    OnboardingProblemScope TargetScope,
    int? ConnectionId,
    int? ProjectId,
    int? RootId,
    int? BindingId,
    string SuggestedActionLabel,
    OnboardingExecutionConfidenceLevel ConfidenceLevel,
    ExecutionIntentNavigationTargetViewModel NavigationTarget);

public sealed record ActionableProblemViewModel(
    string ProblemKey,
    string Title,
    string AffectedEntity,
    string Location,
    string Reason,
    OnboardingProblemSeverity Severity,
    OnboardingProblemScope Scope,
    int ImpactedChildrenCount,
    bool FixFirst,
    OnboardingGraphSection GraphSection,
    string TargetElementId,
    string SuggestedAction,
    string ExpectedImpact,
    string RootCauseGroupingKey,
    string RootCauseEntity,
    OnboardingGraphSection RootCauseGraphSection,
    string RootCauseTargetElementId,
    string RootCauseLabel,
    int? ConnectionId,
    int? ProjectId,
    int? RootId,
    int? BindingId,
    ExecutionIntentViewModel ExecutionIntent);

public sealed record OnboardingProblemGroupViewModel(
    OnboardingProblemScope Scope,
    string Title,
    IReadOnlyList<ActionableProblemViewModel> Items);

public sealed record OnboardingRootCauseGroupViewModel(
    string RootCauseGroupingKey,
    string Title,
    string RootCauseEntity,
    string RootCauseLabel,
    OnboardingProblemSeverity Severity,
    OnboardingProblemScope Scope,
    string SuggestedAction,
    string ExpectedImpact,
    int VisibleIssueCount,
    int DerivedIssueCount,
    bool FixFirst,
    OnboardingGraphSection GraphSection,
    string TargetElementId,
    ActionableProblemViewModel PrimaryProblem,
    IReadOnlyList<ActionableProblemViewModel> Items,
    ExecutionIntentViewModel ExecutionIntent);

public sealed record OnboardingProblemSummaryViewModel(
    IReadOnlyList<OnboardingRootCauseGroupViewModel> TopBlockers,
    IReadOnlyList<OnboardingRootCauseGroupViewModel> OtherBlockers,
    IReadOnlyList<OnboardingRootCauseGroupViewModel> Warnings,
    int BlockingCount,
    int WarningCount,
    int BlockingRootCauseCount,
    int WarningRootCauseCount,
    bool HasProblems);

public sealed record OnboardingGraphSectionStateViewModel(
    OnboardingGraphSection Section,
    string Title,
    string AnchorId,
    int BlockingCount,
    int WarningCount,
    bool DefaultExpanded);

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
    OnboardingProblemSummaryViewModel? ProblemSummary,
    IReadOnlyList<OnboardingRootCauseGroupViewModel> RootCauseGroups,
    IReadOnlyList<OnboardingProblemGroupViewModel> ProblemGroups,
    IReadOnlyList<OnboardingGraphSectionStateViewModel> GraphSections,
    string? ErrorMessage);
