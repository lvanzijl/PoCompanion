using PoTool.Client.Models;
using PoTool.Shared.Onboarding;

namespace PoTool.Client.Services;

public sealed class OnboardingWorkspaceViewModelFactory
{
    private const int TopBlockerLimit = 5;
    private readonly OnboardingActionSuggestionService _actionSuggestionService = new();
    private readonly OnboardingExecutionIntentService _executionIntentService = new();

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

        var orderedProblems = OrderProblems(CreateProblems(data));
        var rootCauseGroups = CreateRootCauseGroups(orderedProblems);
        var fixFirstRootCauseKeys = rootCauseGroups
            .Where(group => group.Severity == OnboardingProblemSeverity.Blocking)
            .Take(TopBlockerLimit)
            .Select(group => group.RootCauseGroupingKey)
            .ToHashSet(StringComparer.Ordinal);

        orderedProblems = ApplyFixFirst(orderedProblems, fixFirstRootCauseKeys);
        rootCauseGroups = ApplyFixFirst(rootCauseGroups, fixFirstRootCauseKeys);

        var problemSummary = CreateProblemSummary(orderedProblems, rootCauseGroups);
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
            rootCauseGroups,
            problemGroups,
            graphSections,
            null);
    }

    private IReadOnlyList<ActionableProblemViewModel> CreateProblems(OnboardingWorkspaceData data)
    {
        var problems = new List<ActionableProblemViewModel>();
        var globalImpact = BuildGlobalImpactDescriptor(data);
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
                globalImpact.Total,
                BuildExpectedImpact(OnboardingGraphSection.Connections, globalImpact),
                "Global onboarding scope",
                OnboardingGraphSection.Connections,
                GetSectionAnchorId(OnboardingGraphSection.Connections),
                null,
                null,
                null,
                null,
                globalImpact.Projects,
                globalImpact.Teams,
                globalImpact.Pipelines));
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
                globalImpact.Total,
                BuildExpectedImpact(OnboardingGraphSection.Connections, globalImpact),
                "Global onboarding scope",
                OnboardingGraphSection.Connections,
                GetSectionAnchorId(OnboardingGraphSection.Connections),
                null,
                null,
                null,
                null,
                globalImpact.Projects,
                globalImpact.Teams,
                globalImpact.Pipelines));
        }

        foreach (var connection in data.Connections)
        {
            var elementId = CreateElementId("connection", connection.Id);
            var location = $"Connection {connection.OrganizationUrl}";
            var impact = BuildConnectionImpactDescriptor(connection.Id, data);

            AddEntityStatusProblems(
                problems,
                connection.Status,
                OnboardingProblemScope.Global,
                connection.OrganizationUrl,
                location,
                OnboardingGraphSection.Connections,
                elementId,
                impact,
                location,
                OnboardingGraphSection.Connections,
                elementId,
                "connection-status",
                connection.Id,
                null,
                null,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);

            AddValidationProblem(
                problems,
                connection.Validation.AvailabilityValidationState,
                OnboardingProblemScope.Global,
                $"Connection availability — {connection.OrganizationUrl}",
                location,
                OnboardingGraphSection.Connections,
                elementId,
                impact,
                location,
                OnboardingGraphSection.Connections,
                elementId,
                $"connection-{connection.Id}-availability",
                connection.Id,
                null,
                null,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);

            AddValidationProblem(
                problems,
                connection.Validation.PermissionValidationState,
                OnboardingProblemScope.Global,
                $"Connection permissions — {connection.OrganizationUrl}",
                location,
                OnboardingGraphSection.Connections,
                elementId,
                impact,
                location,
                OnboardingGraphSection.Connections,
                elementId,
                $"connection-{connection.Id}-permission",
                connection.Id,
                null,
                null,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);

            AddValidationProblem(
                problems,
                connection.Validation.CapabilityValidationState,
                OnboardingProblemScope.Global,
                $"Connection capabilities — {connection.OrganizationUrl}",
                location,
                OnboardingGraphSection.Connections,
                elementId,
                impact,
                location,
                OnboardingGraphSection.Connections,
                elementId,
                $"connection-{connection.Id}-capability",
                connection.Id,
                null,
                null,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);
        }

        foreach (var project in data.Projects)
        {
            var elementId = CreateElementId("project", project.Id);
            var location = $"Project {project.Snapshot.Name} ({project.ProjectExternalId})";
            var impact = BuildProjectImpactDescriptor(project.Id, data);

            AddEntityStatusProblems(
                problems,
                project.Status,
                OnboardingProblemScope.Project,
                project.Snapshot.Name,
                location,
                OnboardingGraphSection.Projects,
                elementId,
                impact,
                $"Project {project.Snapshot.Name}",
                OnboardingGraphSection.Projects,
                elementId,
                "project-status",
                project.TfsConnectionId,
                project.Id,
                null,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);

            AddValidationProblem(
                problems,
                project.ValidationState,
                OnboardingProblemScope.Project,
                project.Snapshot.Name,
                location,
                OnboardingGraphSection.Projects,
                elementId,
                impact,
                $"Project {project.Snapshot.Name}",
                OnboardingGraphSection.Projects,
                elementId,
                $"project-{project.Id}-validation",
                project.TfsConnectionId,
                project.Id,
                null,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);
        }

        foreach (var team in data.Teams)
        {
            if (!projectsById.TryGetValue(team.ProjectSourceId, out var project))
            {
                continue;
            }

            var elementId = CreateElementId("team", team.Id);
            var location = $"Project {project.Snapshot.Name} ({project.ProjectExternalId})";
            var impact = BuildProjectImpactDescriptor(project.Id, data);

            AddEntityStatusProblems(
                problems,
                team.Status,
                OnboardingProblemScope.Project,
                $"Team {team.Snapshot.Name}",
                location,
                OnboardingGraphSection.Teams,
                elementId,
                impact,
                $"Project {project.Snapshot.Name}",
                OnboardingGraphSection.Projects,
                CreateElementId("project", project.Id),
                "team-status",
                project.TfsConnectionId,
                project.Id,
                null,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);

            AddValidationProblem(
                problems,
                team.ValidationState,
                OnboardingProblemScope.Project,
                $"Team {team.Snapshot.Name}",
                location,
                OnboardingGraphSection.Teams,
                elementId,
                impact,
                $"Project {project.Snapshot.Name}",
                OnboardingGraphSection.Projects,
                CreateElementId("project", project.Id),
                $"team-{team.Id}-validation",
                project.TfsConnectionId,
                project.Id,
                null,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);
        }

        foreach (var pipeline in data.Pipelines)
        {
            if (!projectsById.TryGetValue(pipeline.ProjectSourceId, out var project))
            {
                continue;
            }

            var elementId = CreateElementId("pipeline", pipeline.Id);
            var location = $"Project {project.Snapshot.Name} ({project.ProjectExternalId})";
            var impact = BuildProjectImpactDescriptor(project.Id, data);

            AddEntityStatusProblems(
                problems,
                pipeline.Status,
                OnboardingProblemScope.Project,
                $"Pipeline {pipeline.Snapshot.Name}",
                location,
                OnboardingGraphSection.Pipelines,
                elementId,
                impact,
                $"Project {project.Snapshot.Name}",
                OnboardingGraphSection.Projects,
                CreateElementId("project", project.Id),
                "pipeline-status",
                project.TfsConnectionId,
                project.Id,
                null,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);

            AddValidationProblem(
                problems,
                pipeline.ValidationState,
                OnboardingProblemScope.Project,
                $"Pipeline {pipeline.Snapshot.Name}",
                location,
                OnboardingGraphSection.Pipelines,
                elementId,
                impact,
                $"Project {project.Snapshot.Name}",
                OnboardingGraphSection.Projects,
                CreateElementId("project", project.Id),
                $"pipeline-{pipeline.Id}-validation",
                project.TfsConnectionId,
                project.Id,
                null,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);
        }

        foreach (var root in data.ProductRoots)
        {
            if (!projectsById.TryGetValue(root.ProjectSourceId, out var project))
            {
                continue;
            }

            var elementId = CreateElementId("root", root.Id);
            var location = $"Root {root.Snapshot.Title} in project {project.Snapshot.Name}";
            var impact = BuildRootImpactDescriptor(root.Id, data);

            AddEntityStatusProblems(
                problems,
                root.Status,
                OnboardingProblemScope.Root,
                root.Snapshot.Title,
                location,
                OnboardingGraphSection.ProductRoots,
                elementId,
                impact,
                $"Root {root.Snapshot.Title}",
                OnboardingGraphSection.ProductRoots,
                elementId,
                "root-status",
                project.TfsConnectionId,
                project.Id,
                root.Id,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);

            AddValidationProblem(
                problems,
                root.ValidationState,
                OnboardingProblemScope.Root,
                root.Snapshot.Title,
                location,
                OnboardingGraphSection.ProductRoots,
                elementId,
                impact,
                $"Root {root.Snapshot.Title}",
                OnboardingGraphSection.ProductRoots,
                elementId,
                $"root-{root.Id}-validation",
                project.TfsConnectionId,
                project.Id,
                root.Id,
                null,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);
        }

        foreach (var binding in data.Bindings)
        {
            if (!rootsById.TryGetValue(binding.ProductRootId, out var root)
                || !projectsById.TryGetValue(binding.ProjectSourceId, out var project))
            {
                continue;
            }

            var elementId = CreateElementId("binding", binding.Id);
            var affectedEntity = $"{binding.SourceType} binding — {binding.SourceExternalId}";
            var location = $"Root {root.Snapshot.Title} in project {project.Snapshot.Name}";
            var impact = BuildRootImpactDescriptor(root.Id, data);

            AddEntityStatusProblems(
                problems,
                binding.Status,
                OnboardingProblemScope.Binding,
                affectedEntity,
                location,
                OnboardingGraphSection.Bindings,
                elementId,
                impact,
                $"Root {root.Snapshot.Title}",
                OnboardingGraphSection.ProductRoots,
                CreateElementId("root", root.Id),
                "binding-status",
                project.TfsConnectionId,
                project.Id,
                root.Id,
                binding.Id,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);

            AddValidationProblem(
                problems,
                binding.ValidationState,
                OnboardingProblemScope.Binding,
                affectedEntity,
                location,
                OnboardingGraphSection.Bindings,
                elementId,
                impact,
                $"Root {root.Snapshot.Title}",
                OnboardingGraphSection.ProductRoots,
                CreateElementId("root", root.Id),
                $"binding-{binding.Id}-validation",
                project.TfsConnectionId,
                project.Id,
                root.Id,
                binding.Id,
                impact.Projects,
                impact.Teams,
                impact.Pipelines);
        }

        return problems;
    }

    private void AddEntityStatusProblems(
        ICollection<ActionableProblemViewModel> problems,
        OnboardingEntityStatusDto status,
        OnboardingProblemScope scope,
        string affectedEntity,
        string location,
        OnboardingGraphSection section,
        string targetElementId,
        ImpactDescriptor impact,
        string rootCauseEntity,
        OnboardingGraphSection rootCauseSection,
        string rootCauseTargetElementId,
        string keySuffix,
        int? connectionId,
        int? projectId,
        int? rootId,
        int? bindingId,
        int visibleProjectCount,
        int visibleTeamCount,
        int visiblePipelineCount)
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
                impact.Total,
                BuildExpectedImpact(section, impact),
                rootCauseEntity,
                rootCauseSection,
                rootCauseTargetElementId,
                connectionId,
                projectId,
                rootId,
                bindingId,
                visibleProjectCount,
                visibleTeamCount,
                visiblePipelineCount));
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
                impact.Total,
                BuildExpectedImpact(section, impact),
                rootCauseEntity,
                rootCauseSection,
                rootCauseTargetElementId,
                connectionId,
                projectId,
                rootId,
                bindingId,
                visibleProjectCount,
                visibleTeamCount,
                visiblePipelineCount));
        }
    }

    private void AddValidationProblem(
        ICollection<ActionableProblemViewModel> problems,
        OnboardingValidationStateDto validationState,
        OnboardingProblemScope scope,
        string affectedEntity,
        string location,
        OnboardingGraphSection section,
        string targetElementId,
        ImpactDescriptor impact,
        string rootCauseEntity,
        OnboardingGraphSection rootCauseSection,
        string rootCauseTargetElementId,
        string keySuffix,
        int? connectionId,
        int? projectId,
        int? rootId,
        int? bindingId,
        int visibleProjectCount,
        int visibleTeamCount,
        int visiblePipelineCount)
    {
        if (!OnboardingSignalPresentation.TryMapValidationSeverity(validationState.Status, out var severity))
        {
            return;
        }

        problems.Add(CreateProblem(
            $"{severity}-{scope}-{targetElementId}-{validationState.Status}-{keySuffix}",
            OnboardingSignalPresentation.BuildValidationTitle(affectedEntity, validationState.Status),
            affectedEntity,
            location,
            OnboardingSignalPresentation.BuildValidationReason(validationState),
            severity,
            scope,
            section,
            targetElementId,
            impact.Total,
            BuildExpectedImpact(section, impact),
            rootCauseEntity,
            rootCauseSection,
            rootCauseTargetElementId,
            connectionId,
            projectId,
            rootId,
            bindingId,
            visibleProjectCount,
            visibleTeamCount,
            visiblePipelineCount));
    }

    private IReadOnlyList<ActionableProblemViewModel> OrderProblems(IEnumerable<ActionableProblemViewModel> problems)
        => problems
            .OrderBy(problem => problem.Severity)
            .ThenBy(problem => problem.Scope)
            .ThenByDescending(problem => problem.ImpactedChildrenCount)
            .ThenBy(problem => problem.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(problem => problem.AffectedEntity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(problem => problem.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<ActionableProblemViewModel> ApplyFixFirst(
        IReadOnlyList<ActionableProblemViewModel> problems,
        IReadOnlySet<string> fixFirstRootCauseKeys)
        => problems
            .Select(problem => problem with
            {
                FixFirst = fixFirstRootCauseKeys.Contains(problem.RootCauseGroupingKey)
            })
            .ToList();

    private static IReadOnlyList<OnboardingRootCauseGroupViewModel> CreateRootCauseGroups(IReadOnlyList<ActionableProblemViewModel> problems)
        => problems
            .GroupBy(problem => problem.RootCauseGroupingKey)
            .Select(group =>
            {
                var items = group.ToList();
                var primary = items[0];
                var visibleIssueCount = items.Count;
                var derivedIssueCount = Math.Max(0, visibleIssueCount - 1);
                var title = visibleIssueCount > 1
                    ? primary.Reason
                    : primary.Title;
                var expectedImpact = derivedIssueCount > 0
                    ? $"{primary.ExpectedImpact} Groups {visibleIssueCount} visible issue(s) under the same root cause."
                    : primary.ExpectedImpact;

                return new OnboardingRootCauseGroupViewModel(
                    primary.RootCauseGroupingKey,
                    title,
                    primary.RootCauseEntity,
                    primary.RootCauseLabel,
                    primary.Severity,
                    primary.Scope,
                    primary.SuggestedAction,
                    expectedImpact,
                    visibleIssueCount,
                    derivedIssueCount,
                    false,
                    primary.RootCauseGraphSection,
                    primary.RootCauseTargetElementId,
                    primary,
                    items,
                    primary.ExecutionIntent);
            })
            .OrderBy(group => group.Severity)
            .ThenBy(group => group.Scope)
            .ThenByDescending(group => group.PrimaryProblem.ImpactedChildrenCount)
            .ThenByDescending(group => group.VisibleIssueCount)
            .ThenBy(group => group.RootCauseEntity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<OnboardingRootCauseGroupViewModel> ApplyFixFirst(
        IReadOnlyList<OnboardingRootCauseGroupViewModel> groups,
        IReadOnlySet<string> fixFirstRootCauseKeys)
        => groups
            .Select(group =>
            {
                var fixFirst = fixFirstRootCauseKeys.Contains(group.RootCauseGroupingKey);
                return group with
                {
                    FixFirst = fixFirst,
                    PrimaryProblem = group.PrimaryProblem with { FixFirst = fixFirst },
                    Items = group.Items.Select(item => item with { FixFirst = fixFirst }).ToList()
                };
            })
            .ToList();

    private static OnboardingProblemSummaryViewModel CreateProblemSummary(
        IReadOnlyList<ActionableProblemViewModel> problems,
        IReadOnlyList<OnboardingRootCauseGroupViewModel> rootCauseGroups)
    {
        var blockers = problems
            .Where(problem => problem.Severity == OnboardingProblemSeverity.Blocking)
            .ToList();
        var warnings = problems
            .Where(problem => problem.Severity == OnboardingProblemSeverity.Warning)
            .ToList();
        var blockingRootCauses = rootCauseGroups
            .Where(group => group.Severity == OnboardingProblemSeverity.Blocking)
            .ToList();
        var warningRootCauses = rootCauseGroups
            .Where(group => group.Severity == OnboardingProblemSeverity.Warning)
            .ToList();

        return new OnboardingProblemSummaryViewModel(
            blockingRootCauses.Where(group => group.FixFirst).ToList(),
            blockingRootCauses.Where(group => !group.FixFirst).ToList(),
            warningRootCauses,
            blockers.Count,
            warnings.Count,
            blockingRootCauses.Count,
            warningRootCauses.Count,
            blockers.Count > 0 || warnings.Count > 0);
    }

    private static IReadOnlyList<OnboardingProblemGroupViewModel> CreateProblemGroups(IReadOnlyList<ActionableProblemViewModel> problems)
        => problems
            .GroupBy(problem => problem.Scope)
            .OrderBy(group => group.Key)
            .Select(group => new OnboardingProblemGroupViewModel(
                group.Key,
                GetScopeTitle(group.Key),
                group.ToList()))
            .ToList();

    private static IReadOnlyList<OnboardingGraphSectionStateViewModel> CreateGraphSectionStates(IReadOnlyList<ActionableProblemViewModel> problems)
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

    private ActionableProblemViewModel CreateProblem(
        string problemKey,
        string title,
        string affectedEntity,
        string location,
        string reason,
        OnboardingProblemSeverity severity,
        OnboardingProblemScope scope,
        OnboardingGraphSection section,
        string targetElementId,
        int impactedChildrenCount,
        string expectedImpact,
        string rootCauseEntity,
        OnboardingGraphSection rootCauseGraphSection,
        string rootCauseTargetElementId,
        int? connectionId,
        int? projectId,
        int? rootId,
        int? bindingId,
        int visibleProjectCount,
        int visibleTeamCount,
        int visiblePipelineCount)
    {
        var suggestedAction = _actionSuggestionService.GetSuggestedAction(scope, title, reason, affectedEntity);
        var executionIntent = _executionIntentService.CreateIntent(
            suggestedAction,
            scope,
            connectionId,
            projectId,
            rootId,
            bindingId,
            rootCauseGraphSection,
            GetSectionAnchorId(rootCauseGraphSection),
            rootCauseTargetElementId,
            visibleProjectCount,
            visibleTeamCount,
            visiblePipelineCount);

        return new ActionableProblemViewModel(
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
            targetElementId,
            suggestedAction,
            expectedImpact,
            BuildRootCauseGroupingKey(rootCauseTargetElementId, reason, severity),
            rootCauseEntity,
            rootCauseGraphSection,
            rootCauseTargetElementId,
            $"{rootCauseEntity} — {reason}",
            connectionId,
            projectId,
            rootId,
            bindingId,
            executionIntent);
    }

    private static string BuildRootCauseGroupingKey(string rootCauseTargetElementId, string reason, OnboardingProblemSeverity severity)
        => $"{severity}:{rootCauseTargetElementId}:{NormalizeRootCauseFragment(reason)}";

    private static string NormalizeRootCauseFragment(string value)
        => value
            .Trim()
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace(':', '-')
            .Replace('/', '-')
            .Replace('.', '-')
            .Replace(',', '-')
            .Replace('—', '-');

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

    private static ImpactDescriptor BuildGlobalImpactDescriptor(OnboardingWorkspaceData data)
        => new(
            data.Projects.Count,
            data.Teams.Count,
            data.Pipelines.Count,
            data.ProductRoots.Count,
            data.Bindings.Count);

    private static ImpactDescriptor BuildConnectionImpactDescriptor(int connectionId, OnboardingWorkspaceData data)
    {
        var projectIds = data.Projects
            .Where(project => project.TfsConnectionId == connectionId)
            .Select(project => project.Id)
            .ToHashSet();
        var rootIds = data.ProductRoots
            .Where(root => projectIds.Contains(root.ProjectSourceId))
            .Select(root => root.Id)
            .ToHashSet();

        return new ImpactDescriptor(
            data.Projects.Count(project => project.TfsConnectionId == connectionId),
            data.Teams.Count(team => projectIds.Contains(team.ProjectSourceId)),
            data.Pipelines.Count(pipeline => projectIds.Contains(pipeline.ProjectSourceId)),
            data.ProductRoots.Count(root => projectIds.Contains(root.ProjectSourceId)),
            data.Bindings.Count(binding => rootIds.Contains(binding.ProductRootId)));
    }

    private static ImpactDescriptor BuildProjectImpactDescriptor(int projectId, OnboardingWorkspaceData data)
    {
        var rootIds = data.ProductRoots
            .Where(root => root.ProjectSourceId == projectId)
            .Select(root => root.Id)
            .ToHashSet();

        return new ImpactDescriptor(
            0,
            data.Teams.Count(team => team.ProjectSourceId == projectId),
            data.Pipelines.Count(pipeline => pipeline.ProjectSourceId == projectId),
            data.ProductRoots.Count(root => root.ProjectSourceId == projectId),
            data.Bindings.Count(binding => rootIds.Contains(binding.ProductRootId)));
    }

    private static ImpactDescriptor BuildRootImpactDescriptor(int rootId, OnboardingWorkspaceData data)
        => new(0, 0, 0, 0, data.Bindings.Count(binding => binding.ProductRootId == rootId));

    private static ImpactDescriptor BuildDirectImpactDescriptor()
        => new(0, 0, 0, 0, 0);

    private static string BuildExpectedImpact(OnboardingGraphSection section, ImpactDescriptor impact)
        => section switch
        {
            OnboardingGraphSection.Connections when impact.Total > 0
                => $"Affects {FormatImpactCounts(impact)} across the visible onboarding graph.",
            OnboardingGraphSection.Connections
                => "Affects the visible onboarding workspace.",
            OnboardingGraphSection.Projects when impact.Total > 0
                => $"Affects {FormatImpactCounts(impact)} in this project.",
            OnboardingGraphSection.Projects
                => "Affects this project only.",
            OnboardingGraphSection.Teams
                => "Affects this team source only.",
            OnboardingGraphSection.Pipelines
                => "Affects this pipeline source only.",
            OnboardingGraphSection.ProductRoots when impact.Bindings > 0
                => $"Blocks {impact.Bindings} binding(s) for this root.",
            OnboardingGraphSection.ProductRoots
                => "Prevents this root from being usable.",
            OnboardingGraphSection.Bindings
                => "Prevents this binding from being usable.",
            _ when impact.Total > 0
                => $"Affects {FormatImpactCounts(impact)}.",
            _ => "Affects the current onboarding scope."
        };

    private static string FormatImpactCounts(ImpactDescriptor impact)
    {
        var parts = new List<string>();

        if (impact.Projects > 0)
        {
            parts.Add($"{impact.Projects} project(s)");
        }

        if (impact.Teams > 0)
        {
            parts.Add($"{impact.Teams} team source(s)");
        }

        if (impact.Pipelines > 0)
        {
            parts.Add($"{impact.Pipelines} pipeline source(s)");
        }

        if (impact.Roots > 0)
        {
            parts.Add($"{impact.Roots} root(s)");
        }

        if (impact.Bindings > 0)
        {
            parts.Add($"{impact.Bindings} binding(s)");
        }

        return parts.Count switch
        {
            0 => "the current onboarding scope",
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => $"{string.Join(", ", parts.Take(parts.Count - 1))}, and {parts[^1]}"
        };
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

    private sealed record ImpactDescriptor(
        int Projects,
        int Teams,
        int Pipelines,
        int Roots,
        int Bindings)
    {
        public int Total => Projects + Teams + Pipelines + Roots + Bindings;
    }
}
