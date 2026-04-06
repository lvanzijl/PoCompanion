using PoTool.Client.Models;
using PoTool.Shared.Onboarding;

namespace PoTool.Client.Services;

public sealed class OnboardingWorkspaceViewModelFactory
{
    public OnboardingWorkspaceViewModel CreateLoading(OnboardingWorkspaceFilter filter)
        => new(
            OnboardingWorkspaceLoadState.Loading,
            true,
            "Onboarding Workspace",
            "Loading read-only onboarding data.",
            filter,
            new OnboardingWorkspaceFilterOptions([], [], [], []),
            null,
            [],
            [],
            [],
            [],
            [],
            [],
            null);

    public OnboardingWorkspaceViewModel CreateFailure(OnboardingWorkspaceFilter filter, string errorMessage)
        => new(
            OnboardingWorkspaceLoadState.Failed,
            true,
            "Onboarding Workspace",
            "The onboarding workspace could not load.",
            filter,
            new OnboardingWorkspaceFilterOptions([], [], [], []),
            null,
            [],
            [],
            [],
            [],
            [],
            [],
            errorMessage);

    public OnboardingWorkspaceViewModel Create(OnboardingWorkspaceData data)
    {
        var hasVisibleData = data.Connections.Count > 0
            || data.Projects.Count > 0
            || data.Teams.Count > 0
            || data.Pipelines.Count > 0
            || data.ProductRoots.Count > 0
            || data.Bindings.Count > 0;

        var summary = new OnboardingSummaryViewModel(
            data.Status.OverallStatus,
            data.Status.ConnectionStatus,
            data.Status.DataSourceSetupStatus,
            data.Status.DomainConfigurationStatus,
            data.Status.BlockingReasons,
            data.Status.Warnings,
            data.Status.Counts,
            hasVisibleData,
            data.Status.BlockingReasons.Count > 0,
            data.Status.Warnings.Count > 0);

        var loadState = hasVisibleData || data.Status.OverallStatus != OnboardingConfigurationStatus.NotConfigured
            ? OnboardingWorkspaceLoadState.Ready
            : OnboardingWorkspaceLoadState.Empty;

        var message = loadState == OnboardingWorkspaceLoadState.Empty
            ? "No onboarding data is currently visible. Configure the backend onboarding graph before using this read-only workspace."
            : "Read-only onboarding status and graph view. No changes can be made from this workspace.";

        return new OnboardingWorkspaceViewModel(
            loadState,
            true,
            "Onboarding Workspace",
            message,
            data.AppliedFilter,
            data.FilterOptions,
            summary,
            data.Connections,
            data.Projects,
            GroupTeams(data.Projects, data.Teams),
            GroupPipelines(data.Projects, data.Pipelines),
            GroupRoots(data.Projects, data.ProductRoots),
            GroupBindings(data.Projects, data.ProductRoots, data.Bindings),
            null);
    }

    private static IReadOnlyList<OnboardingProjectGroupViewModel<OnboardingTeamSourceDto>> GroupTeams(
        IReadOnlyList<OnboardingProjectSourceDto> projects,
        IReadOnlyList<OnboardingTeamSourceDto> teams)
        => GroupByProject(
            projects,
            teams,
            item => item.ProjectSourceId);

    private static IReadOnlyList<OnboardingProjectGroupViewModel<OnboardingPipelineSourceDto>> GroupPipelines(
        IReadOnlyList<OnboardingProjectSourceDto> projects,
        IReadOnlyList<OnboardingPipelineSourceDto> pipelines)
        => GroupByProject(
            projects,
            pipelines,
            item => item.ProjectSourceId);

    private static IReadOnlyList<OnboardingProjectGroupViewModel<OnboardingProductRootDto>> GroupRoots(
        IReadOnlyList<OnboardingProjectSourceDto> projects,
        IReadOnlyList<OnboardingProductRootDto> roots)
        => GroupByProject(
            projects,
            roots,
            item => item.ProjectSourceId);

    private static IReadOnlyList<OnboardingBindingGroupViewModel> GroupBindings(
        IReadOnlyList<OnboardingProjectSourceDto> projects,
        IReadOnlyList<OnboardingProductRootDto> roots,
        IReadOnlyList<OnboardingProductSourceBindingDto> bindings)
    {
        var projectsById = projects.ToDictionary(
            project => project.Id,
            project => project);
        var rootsById = roots.ToDictionary(
            root => root.Id,
            root => root);

        return bindings
            .GroupBy(binding => binding.ProductRootId)
            .Select(group =>
            {
                var root = rootsById[group.Key];
                var project = projectsById[root.ProjectSourceId];
                return new OnboardingBindingGroupViewModel(
                    new OnboardingRootContextViewModel(
                        root.Id,
                        root.WorkItemExternalId,
                        root.Snapshot.Title,
                        project.ProjectExternalId),
                    group
                        .OrderBy(item => item.SourceType)
                        .ThenBy(item => item.SourceExternalId)
                        .ToList());
            })
            .OrderBy(group => group.Root.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<OnboardingProjectGroupViewModel<TItem>> GroupByProject<TItem>(
        IReadOnlyList<OnboardingProjectSourceDto> projects,
        IReadOnlyList<TItem> items,
        Func<TItem, int> projectIdSelector)
    {
        var projectsById = projects.ToDictionary(
            project => project.Id,
            project => new OnboardingProjectContextViewModel(
                project.Id,
                project.ProjectExternalId,
                project.Snapshot.Name));

        return items
            .GroupBy(projectIdSelector)
            .Where(group => projectsById.ContainsKey(group.Key))
            .Select(group => new OnboardingProjectGroupViewModel<TItem>(
                projectsById[group.Key],
                group.ToList()))
            .OrderBy(group => group.Project.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
