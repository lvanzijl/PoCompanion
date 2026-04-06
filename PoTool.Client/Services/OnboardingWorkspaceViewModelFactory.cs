using PoTool.Client.Models;
using PoTool.Shared.Onboarding;

namespace PoTool.Client.Services;

public sealed class OnboardingWorkspaceViewModelFactory
{
    private const int TopBlockerLimit = 5;

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
            null,
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
            null,
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

        var problems = CreateProblems(data);
        var orderedProblems = OrderProblems(problems);
        var problemSummary = CreateProblemSummary(orderedProblems);
        var problemGroups = CreateProblemGroups(orderedProblems);
        var graphSections = CreateGraphSectionStates(orderedProblems);

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
            problemSummary,
            problemGroups,
            graphSections,
            null);
    }

    private static IReadOnlyList<OnboardingProblemItemViewModel> CreateProblems(OnboardingWorkspaceData data)
    {
        var problems = new List<OnboardingProblemItemViewModel>();
        var visibleImpactCount = data.Connections.Count
                                 + data.Projects.Count
                                 + data.Teams.Count
                                 + data.Pipelines.Count
                                 + data.ProductRoots.Count
                                 + data.Bindings.Count;

        var projectsById = data.Projects.ToDictionary(project => project.Id);
        var rootsById = data.ProductRoots.ToDictionary(root => root.Id);

        foreach (var issue in data.Status.BlockingReasons)
        {
            problems.Add(CreateProblem(
                $"status-blocking-{issue.Code}",
                issue.Message,
                "Onboarding workspace",
                "Global onboarding scope",
                issue.Message,
                OnboardingProblemSeverity.Blocking,
                OnboardingProblemScope.Global,
                OnboardingGraphSection.Connections,
                GetSectionAnchorId(OnboardingGraphSection.Connections),
                visibleImpactCount));
        }

        foreach (var issue in data.Status.Warnings)
        {
            problems.Add(CreateProblem(
                $"status-warning-{issue.Code}",
                issue.Message,
                "Onboarding workspace",
                "Global onboarding scope",
                issue.Message,
                OnboardingProblemSeverity.Warning,
                OnboardingProblemScope.Global,
                OnboardingGraphSection.Connections,
                GetSectionAnchorId(OnboardingGraphSection.Connections),
                visibleImpactCount));
        }

        foreach (var connection in data.Connections)
        {
            AddEntityStatusProblems(
                problems,
                connection.Status,
                OnboardingProblemScope.Global,
                connection.OrganizationUrl,
                $"Connection {connection.OrganizationUrl}",
                OnboardingGraphSection.Connections,
                CreateElementId("connection", connection.Id),
                CountConnectionImpact(connection.Id, data),
                "connection-status");

            AddValidationProblem(
                problems,
                connection.Validation.AvailabilityValidationState,
                OnboardingProblemScope.Global,
                $"Connection availability — {connection.OrganizationUrl}",
                $"Connection {connection.OrganizationUrl}",
                OnboardingGraphSection.Connections,
                CreateElementId("connection", connection.Id),
                CountConnectionImpact(connection.Id, data),
                $"connection-{connection.Id}-availability");

            AddValidationProblem(
                problems,
                connection.Validation.PermissionValidationState,
                OnboardingProblemScope.Global,
                $"Connection permissions — {connection.OrganizationUrl}",
                $"Connection {connection.OrganizationUrl}",
                OnboardingGraphSection.Connections,
                CreateElementId("connection", connection.Id),
                CountConnectionImpact(connection.Id, data),
                $"connection-{connection.Id}-permission");

            AddValidationProblem(
                problems,
                connection.Validation.CapabilityValidationState,
                OnboardingProblemScope.Global,
                $"Connection capabilities — {connection.OrganizationUrl}",
                $"Connection {connection.OrganizationUrl}",
                OnboardingGraphSection.Connections,
                CreateElementId("connection", connection.Id),
                CountConnectionImpact(connection.Id, data),
                $"connection-{connection.Id}-capability");
        }

        foreach (var project in data.Projects)
        {
            var location = $"Project {project.Snapshot.Name} ({project.ProjectExternalId})";
            var impact = CountProjectImpact(project.Id, data);

            AddEntityStatusProblems(
                problems,
                project.Status,
                OnboardingProblemScope.Project,
                project.Snapshot.Name,
                location,
                OnboardingGraphSection.Projects,
                CreateElementId("project", project.Id),
                impact,
                "project-status");

            AddValidationProblem(
                problems,
                project.ValidationState,
                OnboardingProblemScope.Project,
                project.Snapshot.Name,
                location,
                OnboardingGraphSection.Projects,
                CreateElementId("project", project.Id),
                impact,
                $"project-{project.Id}-validation");
        }

        foreach (var team in data.Teams)
        {
            if (!projectsById.TryGetValue(team.ProjectSourceId, out var project))
            {
                continue;
            }

            var location = $"Project {project.Snapshot.Name} ({project.ProjectExternalId})";

            AddEntityStatusProblems(
                problems,
                team.Status,
                OnboardingProblemScope.Project,
                $"Team {team.Snapshot.Name}",
                location,
                OnboardingGraphSection.Teams,
                CreateElementId("team", team.Id),
                0,
                "team-status");

            AddValidationProblem(
                problems,
                team.ValidationState,
                OnboardingProblemScope.Project,
                $"Team {team.Snapshot.Name}",
                location,
                OnboardingGraphSection.Teams,
                CreateElementId("team", team.Id),
                0,
                $"team-{team.Id}-validation");
        }

        foreach (var pipeline in data.Pipelines)
        {
            if (!projectsById.TryGetValue(pipeline.ProjectSourceId, out var project))
            {
                continue;
            }

            var location = $"Project {project.Snapshot.Name} ({project.ProjectExternalId})";

            AddEntityStatusProblems(
                problems,
                pipeline.Status,
                OnboardingProblemScope.Project,
                $"Pipeline {pipeline.Snapshot.Name}",
                location,
                OnboardingGraphSection.Pipelines,
                CreateElementId("pipeline", pipeline.Id),
                0,
                "pipeline-status");

            AddValidationProblem(
                problems,
                pipeline.ValidationState,
                OnboardingProblemScope.Project,
                $"Pipeline {pipeline.Snapshot.Name}",
                location,
                OnboardingGraphSection.Pipelines,
                CreateElementId("pipeline", pipeline.Id),
                0,
                $"pipeline-{pipeline.Id}-validation");
        }

        foreach (var root in data.ProductRoots)
        {
            if (!projectsById.TryGetValue(root.ProjectSourceId, out var project))
            {
                continue;
            }

            var location = $"Root {root.Snapshot.Title} in project {project.Snapshot.Name}";
            var impact = CountRootImpact(root.Id, data);

            AddEntityStatusProblems(
                problems,
                root.Status,
                OnboardingProblemScope.Root,
                root.Snapshot.Title,
                location,
                OnboardingGraphSection.ProductRoots,
                CreateElementId("root", root.Id),
                impact,
                "root-status");

            AddValidationProblem(
                problems,
                root.ValidationState,
                OnboardingProblemScope.Root,
                root.Snapshot.Title,
                location,
                OnboardingGraphSection.ProductRoots,
                CreateElementId("root", root.Id),
                impact,
                $"root-{root.Id}-validation");
        }

        foreach (var binding in data.Bindings)
        {
            if (!rootsById.TryGetValue(binding.ProductRootId, out var root)
                || !projectsById.TryGetValue(binding.ProjectSourceId, out var project))
            {
                continue;
            }

            var affectedEntity = $"{binding.SourceType} binding — {binding.SourceExternalId}";
            var location = $"Root {root.Snapshot.Title} in project {project.Snapshot.Name}";

            AddEntityStatusProblems(
                problems,
                binding.Status,
                OnboardingProblemScope.Binding,
                affectedEntity,
                location,
                OnboardingGraphSection.Bindings,
                CreateElementId("binding", binding.Id),
                0,
                "binding-status");

            AddValidationProblem(
                problems,
                binding.ValidationState,
                OnboardingProblemScope.Binding,
                affectedEntity,
                location,
                OnboardingGraphSection.Bindings,
                CreateElementId("binding", binding.Id),
                0,
                $"binding-{binding.Id}-validation");
        }

        return problems;
    }

    private static void AddEntityStatusProblems(
        ICollection<OnboardingProblemItemViewModel> problems,
        OnboardingEntityStatusDto status,
        OnboardingProblemScope scope,
        string affectedEntity,
        string location,
        OnboardingGraphSection section,
        string targetElementId,
        int impactedChildrenCount,
        string keySuffix)
    {
        foreach (var issue in status.BlockingReasons)
        {
            problems.Add(CreateProblem(
                $"blocking-{scope}-{targetElementId}-{issue.Code}-{keySuffix}",
                issue.Message,
                affectedEntity,
                location,
                issue.Message,
                OnboardingProblemSeverity.Blocking,
                scope,
                section,
                targetElementId,
                impactedChildrenCount));
        }

        foreach (var issue in status.Warnings)
        {
            problems.Add(CreateProblem(
                $"warning-{scope}-{targetElementId}-{issue.Code}-{keySuffix}",
                issue.Message,
                affectedEntity,
                location,
                issue.Message,
                OnboardingProblemSeverity.Warning,
                scope,
                section,
                targetElementId,
                impactedChildrenCount));
        }
    }

    private static void AddValidationProblem(
        ICollection<OnboardingProblemItemViewModel> problems,
        OnboardingValidationStateDto validationState,
        OnboardingProblemScope scope,
        string affectedEntity,
        string location,
        OnboardingGraphSection section,
        string targetElementId,
        int impactedChildrenCount,
        string keySuffix)
    {
        if (!OnboardingSignalPresentation.TryMapValidationSeverity(validationState.Status, out var severity))
        {
            return;
        }

        problems.Add(CreateProblem(
            $"{severity}-{scope}-{targetElementId}-{validationState.Status}-{keySuffix}",
            $"{affectedEntity} requires attention",
            affectedEntity,
            location,
            OnboardingSignalPresentation.BuildValidationReason(validationState),
            severity,
            scope,
            section,
            targetElementId,
            impactedChildrenCount));
    }

    private static IReadOnlyList<OnboardingProblemItemViewModel> OrderProblems(IEnumerable<OnboardingProblemItemViewModel> problems)
    {
        var ordered = problems
            .OrderBy(problem => problem.Severity)
            .ThenBy(problem => problem.Scope)
            .ThenByDescending(problem => problem.ImpactedChildrenCount)
            .ThenBy(problem => problem.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(problem => problem.AffectedEntity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(problem => problem.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var topBlockers = ordered
            .Where(problem => problem.Severity == OnboardingProblemSeverity.Blocking)
            .Take(TopBlockerLimit)
            .Select(problem => problem.ProblemKey)
            .ToHashSet(StringComparer.Ordinal);

        return ordered
            .Select(problem => problem with
            {
                FixFirst = topBlockers.Contains(problem.ProblemKey)
            })
            .ToList();
    }

    private static OnboardingProblemSummaryViewModel CreateProblemSummary(IReadOnlyList<OnboardingProblemItemViewModel> problems)
    {
        var blockers = problems
            .Where(problem => problem.Severity == OnboardingProblemSeverity.Blocking)
            .ToList();
        var warnings = problems
            .Where(problem => problem.Severity == OnboardingProblemSeverity.Warning)
            .ToList();

        return new OnboardingProblemSummaryViewModel(
            blockers.Where(problem => problem.FixFirst).ToList(),
            blockers.Where(problem => !problem.FixFirst).ToList(),
            warnings,
            blockers.Count,
            warnings.Count,
            blockers.Count > 0 || warnings.Count > 0);
    }

    private static IReadOnlyList<OnboardingProblemGroupViewModel> CreateProblemGroups(IReadOnlyList<OnboardingProblemItemViewModel> problems)
        => problems
            .GroupBy(problem => problem.Scope)
            .OrderBy(group => group.Key)
            .Select(group => new OnboardingProblemGroupViewModel(
                group.Key,
                GetScopeTitle(group.Key),
                group.ToList()))
            .ToList();

    private static IReadOnlyList<OnboardingGraphSectionStateViewModel> CreateGraphSectionStates(IReadOnlyList<OnboardingProblemItemViewModel> problems)
        => Enum.GetValues<OnboardingGraphSection>()
            .Select(section =>
            {
                var sectionProblems = problems.Where(problem => problem.GraphSection == section).ToList();
                return new OnboardingGraphSectionStateViewModel(
                    section,
                    GetSectionTitle(section),
                    GetSectionAnchorId(section),
                    sectionProblems.Count(problem => problem.Severity == OnboardingProblemSeverity.Blocking),
                    sectionProblems.Count(problem => problem.Severity == OnboardingProblemSeverity.Warning),
                    sectionProblems.Count != 0);
            })
            .ToList();

    private static OnboardingProblemItemViewModel CreateProblem(
        string problemKey,
        string title,
        string affectedEntity,
        string location,
        string reason,
        OnboardingProblemSeverity severity,
        OnboardingProblemScope scope,
        OnboardingGraphSection section,
        string targetElementId,
        int impactedChildrenCount)
        => new(
            problemKey,
            title,
            affectedEntity,
            location,
            reason,
            severity,
            scope,
            impactedChildrenCount,
            false,
            section,
            targetElementId);

    private static string GetScopeTitle(OnboardingProblemScope scope)
        => scope switch
        {
            OnboardingProblemScope.Global => "Global / connection-level",
            OnboardingProblemScope.Project => "Project-level",
            OnboardingProblemScope.Root => "Root-level",
            OnboardingProblemScope.Binding => "Binding-level",
            _ => scope.ToString()
        };

    private static string GetSectionTitle(OnboardingGraphSection section)
        => section switch
        {
            OnboardingGraphSection.Connections => "Connections",
            OnboardingGraphSection.Projects => "Projects",
            OnboardingGraphSection.Teams => "Teams",
            OnboardingGraphSection.Pipelines => "Pipelines",
            OnboardingGraphSection.ProductRoots => "Product Roots",
            OnboardingGraphSection.Bindings => "Bindings",
            _ => section.ToString()
        };

    private static string GetSectionAnchorId(OnboardingGraphSection section)
        => $"section-{GetSectionTitle(section).ToLowerInvariant().Replace(' ', '-')}";

    private static string CreateElementId(string prefix, int id)
        => $"{prefix}-{id}";

    private static int CountConnectionImpact(int connectionId, OnboardingWorkspaceData data)
    {
        var projectIds = data.Projects
            .Where(project => project.TfsConnectionId == connectionId)
            .Select(project => project.Id)
            .ToHashSet();

        var rootIds = data.ProductRoots
            .Where(root => projectIds.Contains(root.ProjectSourceId))
            .Select(root => root.Id)
            .ToHashSet();

        return data.Projects.Count(project => project.TfsConnectionId == connectionId)
               + data.Teams.Count(team => projectIds.Contains(team.ProjectSourceId))
               + data.Pipelines.Count(pipeline => projectIds.Contains(pipeline.ProjectSourceId))
               + data.ProductRoots.Count(root => projectIds.Contains(root.ProjectSourceId))
               + data.Bindings.Count(binding => rootIds.Contains(binding.ProductRootId));
    }

    private static int CountProjectImpact(int projectId, OnboardingWorkspaceData data)
    {
        var rootIds = data.ProductRoots
            .Where(root => root.ProjectSourceId == projectId)
            .Select(root => root.Id)
            .ToHashSet();

        return data.Teams.Count(team => team.ProjectSourceId == projectId)
               + data.Pipelines.Count(pipeline => pipeline.ProjectSourceId == projectId)
               + data.ProductRoots.Count(root => root.ProjectSourceId == projectId)
               + data.Bindings.Count(binding => rootIds.Contains(binding.ProductRootId));
    }

    private static int CountRootImpact(int rootId, OnboardingWorkspaceData data)
        => data.Bindings.Count(binding => binding.ProductRootId == rootId);

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
