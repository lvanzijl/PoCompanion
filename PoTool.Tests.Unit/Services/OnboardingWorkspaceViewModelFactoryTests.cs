using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class OnboardingWorkspaceViewModelFactoryTests
{
    private readonly OnboardingWorkspaceViewModelFactory _factory = new();

    [TestMethod]
    public void CreateFailure_ReturnsFailedReadonlyState()
    {
        var filter = new OnboardingWorkspaceFilter(null, null, null, null);

        var result = _factory.CreateFailure(filter, "API unavailable");

        Assert.AreEqual(OnboardingWorkspaceLoadState.Failed, result.LoadState);
        Assert.IsTrue(result.IsReadOnly);
        Assert.AreEqual("API unavailable", result.ErrorMessage);
    }

    [TestMethod]
    public void Create_WhenNoVisibleData_ReturnsEmptyState()
    {
        var result = _factory.Create(CreateWorkspaceData(
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.NotConfigured,
                OnboardingConfigurationStatus.NotConfigured,
                OnboardingConfigurationStatus.NotConfigured,
                OnboardingConfigurationStatus.NotConfigured,
                [],
                [],
                new OnboardingStatusCountsDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0)),
            [],
            [],
            [],
            [],
            [],
            []));

        Assert.AreEqual(OnboardingWorkspaceLoadState.Empty, result.LoadState);
        Assert.AreEqual("Onboarding Workspace", result.Title);
        Assert.IsTrue(result.IsReadOnly);
    }

    [TestMethod]
    public void Create_BuildsSummaryFromStatusData()
    {
        var result = _factory.Create(CreateWorkspaceData(
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                [new OnboardingStatusIssueDto("BLOCKED", "Connection required", null, null)],
                [new OnboardingStatusIssueDto("WARN", "Binding warning", null, null)],
                new OnboardingStatusCountsDto(1, 1, 2, 1, 1, 1, 1, 1, 2, 1)),
            [CreateConnection()],
            [CreateProject()],
            [],
            [],
            [],
            []));

        Assert.AreEqual(OnboardingWorkspaceLoadState.Ready, result.LoadState);
        Assert.IsNotNull(result.Summary);
        Assert.AreEqual(OnboardingConfigurationStatus.PartiallyConfigured, result.Summary.OverallStatus);
        Assert.HasCount(1, result.Summary.Blockers);
        Assert.HasCount(1, result.Summary.Warnings);
        Assert.AreEqual(2, result.Summary.Counts.TeamSourcesTotal);
        Assert.IsNotNull(result.ProblemSummary);
        Assert.AreEqual(1, result.ProblemSummary.BlockingCount);
        Assert.AreEqual(1, result.ProblemSummary.WarningCount);
        Assert.AreEqual(1, result.ProblemSummary.BlockingRootCauseCount);
        Assert.AreEqual(1, result.ProblemSummary.WarningRootCauseCount);
    }

    [TestMethod]
    public void Create_GlobalStatusIssues_MapToDomainSpecificExecutionSections()
    {
        var result = _factory.Create(CreateWorkspaceData(
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                [
                    new OnboardingStatusIssueDto("PROJECT_SOURCE_REQUIRED", "At least one enabled valid project source is required.", null, null),
                    new OnboardingStatusIssueDto("PRODUCT_ROOT_REQUIRED", "At least one enabled valid product root is required.", null, null)
                ],
                [],
                new OnboardingStatusCountsDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0)),
            [CreateConnection()],
            [],
            [],
            [],
            [],
            []));

        var projectProblem = result.ProblemGroups
            .Single(group => group.Scope == OnboardingProblemScope.Global)
            .Items
            .Single(problem => problem.Title.Contains("project source", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(OnboardingGraphSection.Projects, projectProblem.RootCauseGraphSection);
        Assert.AreEqual("link-project", projectProblem.ExecutionIntent.IntentType);
        Assert.AreEqual(1, projectProblem.ExecutionIntent.ConnectionId);
        StringAssert.Contains(projectProblem.ExecutionIntent.NavigationTarget.Route, "onboardingSection=Projects");

        var rootProblem = result.ProblemGroups
            .Single(group => group.Scope == OnboardingProblemScope.Global)
            .Items
            .Single(problem => problem.Title.Contains("product root", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(OnboardingGraphSection.ProductRoots, rootProblem.RootCauseGraphSection);
        Assert.AreEqual("resolve-root-validation", rootProblem.ExecutionIntent.IntentType);
        Assert.AreEqual(1, rootProblem.ExecutionIntent.ConnectionId);
        StringAssert.Contains(rootProblem.ExecutionIntent.NavigationTarget.Route, "onboardingSection=ProductRoots");
    }

    [TestMethod]
    public void Create_GroupsEntitiesAndBindingsWithRelationshipContext()
    {
        var project = CreateProject();
        var root = CreateRoot(project.Id);
        var team = CreateTeam(project.Id);
        var pipeline = CreatePipeline(project.Id);
        var binding = CreateBinding(root.Id, project.Id);

        var result = _factory.Create(CreateWorkspaceData(
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.Complete,
                [],
                [],
                new OnboardingStatusCountsDto(1, 1, 1, 1, 1, 1, 1, 1, 1, 1)),
            [CreateConnection()],
            [project],
            [team],
            [pipeline],
            [root],
            [binding]));

        Assert.HasCount(1, result.TeamGroups);
        Assert.AreEqual(project.ProjectExternalId, result.TeamGroups[0].Project.ProjectExternalId);
        Assert.HasCount(1, result.PipelineGroups[0].Items);
        Assert.HasCount(1, result.ProductRootGroups[0].Items);
        Assert.AreEqual(root.WorkItemExternalId, result.BindingGroups[0].Root.WorkItemExternalId);
        Assert.AreEqual(project.ProjectExternalId, result.BindingGroups[0].Root.ProjectExternalId);
    }

    [TestMethod]
    public void Create_ProblemSummary_IsRootCauseAware()
    {
        var connection = CreateConnection(
            status: new OnboardingEntityStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                [new OnboardingStatusIssueDto("CONNECTION", "Connection access is blocking onboarding", null, null)],
                []));
        var project = CreateProject(
            status: new OnboardingEntityStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                [new OnboardingStatusIssueDto("PROJECT", "Project mapping is incomplete", null, null)],
                []));
        var root = CreateRoot(
            project.Id,
            status: new OnboardingEntityStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                [],
                [new OnboardingStatusIssueDto("ROOT", "Root metadata needs review", null, null)]));
        var binding = CreateBinding(
            root.Id,
            project.Id,
            validationState: CreateValidationState(OnboardingValidationStatus.PermissionDenied));

        var result = _factory.Create(CreateWorkspaceData(
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                [new OnboardingStatusIssueDto("GLOBAL", "Global blocker", null, null)],
                [new OnboardingStatusIssueDto("WARN", "Global warning", null, null)],
                new OnboardingStatusCountsDto(1, 1, 1, 0, 0, 0, 1, 0, 1, 1)),
            [connection],
            [project],
            [],
            [],
            [root],
            [binding]));

        Assert.IsNotNull(result.ProblemSummary);
        Assert.AreEqual(4, result.ProblemSummary.BlockingCount);
        Assert.AreEqual(2, result.ProblemSummary.WarningCount);
        Assert.AreEqual(4, result.ProblemSummary.BlockingRootCauseCount);
        Assert.AreEqual(2, result.ProblemSummary.WarningRootCauseCount);
        Assert.IsTrue(result.ProblemSummary.TopBlockers.All(problem => problem.FixFirst));
        Assert.IsTrue(result.ProblemSummary.TopBlockers.All(problem => problem.Severity == OnboardingProblemSeverity.Blocking));
        CollectionAssert.Contains(result.ProblemSummary.TopBlockers.Select(group => group.Title).ToArray(), "Global blocker");
        CollectionAssert.Contains(result.ProblemSummary.TopBlockers.Select(group => group.Title).ToArray(), "Connection access is blocking onboarding");
        CollectionAssert.AreEquivalent(
            new[] { "Global warning", "Root metadata needs review" },
            result.ProblemSummary.Warnings.Select(group => group.Title).ToArray());
    }

    [TestMethod]
    public void Create_ActionableProblemsExposeScopeReasonActionAndNavigationTarget()
    {
        var project = CreateProject(
            status: new OnboardingEntityStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                [new OnboardingStatusIssueDto("PROJECT", "Project onboarding is blocked", null, null)],
                []));
        var team = CreateTeam(
            project.Id,
            validationState: CreateValidationState(OnboardingValidationStatus.Unavailable));

        var result = _factory.Create(CreateWorkspaceData(
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                [],
                [],
                new OnboardingStatusCountsDto(1, 1, 1, 1, 0, 0, 0, 0, 0, 0)),
            [CreateConnection()],
            [project],
            [team],
            [],
            [],
            []));

        var projectProblem = result.ProblemGroups
            .Single(group => group.Scope == OnboardingProblemScope.Project)
            .Items
            .First();

        Assert.AreEqual("Project onboarding is blocked", projectProblem.Title);
        Assert.AreEqual($"Project {project.Snapshot.Name} ({project.ProjectExternalId})", projectProblem.Location);
        Assert.AreEqual(OnboardingGraphSection.Projects, projectProblem.GraphSection);
        Assert.AreEqual("project-10", projectProblem.TargetElementId);
        Assert.AreEqual("Link project to connection", projectProblem.SuggestedAction);
        Assert.AreEqual("link-project", projectProblem.ExecutionIntent.IntentType);
        Assert.AreEqual(OnboardingExecutionConfidenceLevel.High, projectProblem.ExecutionIntent.ConfidenceLevel);
        Assert.AreEqual(1, projectProblem.ConnectionId);
        Assert.AreEqual(10, projectProblem.ProjectId);
        StringAssert.Contains(projectProblem.ExecutionIntent.NavigationTarget.Route, "onboardingProjectId=10");
        StringAssert.Contains(projectProblem.ExpectedImpact, "project");

        var teamProblem = result.ProblemGroups
            .Single(group => group.Scope == OnboardingProblemScope.Project)
            .Items
            .Single(problem => problem.TargetElementId == "team-11");

        Assert.AreEqual(OnboardingProblemSeverity.Warning, teamProblem.Severity);
        StringAssert.Contains(teamProblem.Reason, "external source is currently unavailable");
        Assert.AreEqual("Project Project One", teamProblem.RootCauseEntity);
        Assert.AreEqual("project-10", teamProblem.RootCauseTargetElementId);
        Assert.AreEqual("Resolve validation issue", teamProblem.SuggestedAction);
        Assert.AreEqual(OnboardingExecutionConfidenceLevel.Fallback, teamProblem.ExecutionIntent.ConfidenceLevel);
    }

    [TestMethod]
    public void Create_RootCauseGroups_MergeProblemsByReasonAndUpstreamEntity()
    {
        var project = CreateProject();
        var root = CreateRoot(project.Id);
        var bindingOne = CreateBinding(
            root.Id,
            project.Id,
            status: new OnboardingEntityStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                [new OnboardingStatusIssueDto("BINDING", "Project not linked", null, null)],
                []));
        var bindingTwo = CreateBinding(
            root.Id,
            project.Id,
            status: new OnboardingEntityStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                [new OnboardingStatusIssueDto("BINDING-2", "Project not linked", null, null)],
                [])) with { Id = 15 };

        var result = _factory.Create(CreateWorkspaceData(
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                [],
                [],
                new OnboardingStatusCountsDto(0, 0, 1, 0, 0, 0, 1, 0, 1, 2)),
            [],
            [project],
            [],
            [],
            [root],
            [bindingOne, bindingTwo]));

        Assert.AreEqual(2, result.ProblemSummary?.BlockingCount);
        Assert.AreEqual(1, result.ProblemSummary?.BlockingRootCauseCount);
        Assert.HasCount(1, result.RootCauseGroups);
        Assert.AreEqual(2, result.RootCauseGroups[0].VisibleIssueCount);
        Assert.AreEqual(1, result.RootCauseGroups[0].DerivedIssueCount);
        Assert.AreEqual("root-13", result.RootCauseGroups[0].TargetElementId);
        Assert.AreEqual("Link project to connection", result.RootCauseGroups[0].SuggestedAction);
        Assert.AreEqual("link-project", result.RootCauseGroups[0].ExecutionIntent.IntentType);
        Assert.AreEqual(13, result.RootCauseGroups[0].ExecutionIntent.RootId);
    }

    [TestMethod]
    public void Create_ImpactStatements_AreDerivedFromVisibleGraphOnly()
    {
        var project = CreateProject(
            status: new OnboardingEntityStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                [new OnboardingStatusIssueDto("PROJECT", "Project mapping is incomplete", null, null)],
                []));
        var root = CreateRoot(project.Id);
        var team = CreateTeam(project.Id);
        var pipeline = CreatePipeline(project.Id);
        var binding = CreateBinding(root.Id, project.Id);

        var result = _factory.Create(CreateWorkspaceData(
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                [],
                [],
                new OnboardingStatusCountsDto(0, 1, 1, 1, 1, 0, 1, 1, 1, 1)),
            [],
            [project],
            [team],
            [pipeline],
            [root],
            [binding]));

        var problem = result.ProblemGroups
            .Single(group => group.Scope == OnboardingProblemScope.Project)
            .Items
            .Single(item => item.TargetElementId == "project-10");

        StringAssert.Contains(problem.ExpectedImpact, "1 team source(s)");
        StringAssert.Contains(problem.ExpectedImpact, "1 pipeline source(s)");
        StringAssert.Contains(problem.ExpectedImpact, "1 root(s)");
        StringAssert.Contains(problem.ExpectedImpact, "1 binding(s)");
    }

    [TestMethod]
    public void Create_FilteredProblemsKeepOnlyVisibleScopeAndExplicitSectionSignals()
    {
        var project = CreateProject();
        var root = CreateRoot(project.Id);
        var binding = CreateBinding(
            rootId: root.Id,
            projectId: project.Id,
            validationState: CreateValidationState(OnboardingValidationStatus.PermissionDenied));

        var result = _factory.Create(new OnboardingWorkspaceData(
            new OnboardingWorkspaceFilter(null, 10, 13, OnboardingConfigurationStatus.PartiallyConfigured),
            new OnboardingWorkspaceFilterOptions([], [], [], []),
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                [],
                [],
                new OnboardingStatusCountsDto(0, 0, 1, 0, 0, 0, 1, 0, 1, 1)),
            [],
            [project],
            [],
            [],
            [root],
            [binding]));

        Assert.AreEqual(OnboardingWorkspaceLoadState.Ready, result.LoadState);
        Assert.AreEqual(1, result.ProblemSummary?.BlockingCount);
        Assert.AreEqual(1, result.ProblemSummary?.BlockingRootCauseCount);
        Assert.AreEqual(0, result.GraphSections.Single(section => section.Section == OnboardingGraphSection.Connections).BlockingCount);
        Assert.IsTrue(result.GraphSections.Single(section => section.Section == OnboardingGraphSection.Bindings).DefaultExpanded);
        Assert.AreEqual("root-13", result.ProblemSummary?.TopBlockers[0].TargetElementId);
        Assert.AreEqual("binding-14", result.ProblemGroups.Single().Items[0].TargetElementId);
        StringAssert.Contains(result.ProblemGroups.Single().Items[0].ExecutionIntent.NavigationTarget.Route, "onboardingRootId=13");
    }

    [TestMethod]
    public void Create_BindingAssignmentProblemsRouteToReplacementIntentOnBindingTarget()
    {
        var project = CreateProject();
        var root = CreateRoot(project.Id);
        var binding = CreateBinding(
            root.Id,
            project.Id,
            sourceType: OnboardingProductSourceTypeDto.Team,
            sourceExternalId: "team-incident-response",
            status: new OnboardingEntityStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                [new OnboardingStatusIssueDto("TEAM_BINDING_SOURCE_INVALID", "The team binding references a team source that is not enabled and valid.", null, null)],
                []));

        var result = _factory.Create(CreateWorkspaceData(
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                [],
                [],
                new OnboardingStatusCountsDto(0, 1, 1, 0, 0, 0, 1, 0, 1, 1)),
            [CreateConnection()],
            [project],
            [],
            [],
            [root],
            [binding]));

        var problem = result.ProblemGroups
            .Single(group => group.Scope == OnboardingProblemScope.Binding)
            .Items
            .Single();

        Assert.AreEqual("Assign team to project", problem.SuggestedAction);
        Assert.AreEqual("replace-binding-source", problem.ExecutionIntent.IntentType);
        Assert.AreEqual(OnboardingGraphSection.Bindings, problem.ExecutionIntent.NavigationTarget.Section);
        Assert.AreEqual("binding-14", problem.ExecutionIntent.NavigationTarget.TargetElementId);
        StringAssert.Contains(problem.ExecutionIntent.NavigationTarget.Route, "onboardingTarget=binding-14");
        Assert.AreEqual("Assign team to project", result.RootCauseGroups[0].SuggestedAction);
        Assert.AreEqual(problem.ExecutionIntent.IntentType, result.RootCauseGroups[0].ExecutionIntent.IntentType);
        Assert.AreEqual(problem.ExecutionIntent.NavigationTarget.TargetElementId, result.RootCauseGroups[0].ExecutionIntent.NavigationTarget.TargetElementId);
    }

    [TestMethod]
    public void Create_PipelineBindingAssignmentProblemsRouteToReplacementIntentWithReplacementLabels()
    {
        var project = CreateProject();
        var root = CreateRoot(project.Id);
        var pipeline = CreatePipeline(project.Id);
        var binding = CreateBinding(
            root.Id,
            project.Id,
            sourceType: OnboardingProductSourceTypeDto.Pipeline,
            sourceExternalId: "pipeline-incident-response",
            pipelineSourceId: pipeline.Id,
            status: new OnboardingEntityStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                [new OnboardingStatusIssueDto("PIPELINE_BINDING_SOURCE_INVALID", "The pipeline binding references a pipeline source that is not enabled and valid.", null, null)],
                []));

        var result = _factory.Create(CreateWorkspaceData(
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                [],
                [],
                new OnboardingStatusCountsDto(0, 0, 1, 1, 1, 0, 1, 1, 1, 1)),
            [CreateConnection()],
            [project],
            [],
            [pipeline],
            [root],
            [binding]));

        var problem = result.ProblemGroups
            .Single(group => group.Scope == OnboardingProblemScope.Binding)
            .Items
            .Single();
        var rootCause = result.RootCauseGroups.Single();
        var topBlocker = result.ProblemSummary!.TopBlockers.Single();

        Assert.AreEqual("Assign pipeline to project", problem.SuggestedAction);
        Assert.AreEqual("replace-binding-source", problem.ExecutionIntent.IntentType);
        Assert.AreEqual(OnboardingGraphSection.Bindings, problem.ExecutionIntent.NavigationTarget.Section);
        Assert.AreEqual("binding-14", problem.ExecutionIntent.NavigationTarget.TargetElementId);
        StringAssert.Contains(problem.ExecutionIntent.NavigationTarget.Route, "onboardingTarget=binding-14");

        Assert.AreEqual("Assign pipeline to project", rootCause.SuggestedAction);
        Assert.AreEqual("replace-binding-source", rootCause.ExecutionIntent.IntentType);
        Assert.AreEqual("binding-14", rootCause.ExecutionIntent.NavigationTarget.TargetElementId);

        Assert.AreEqual("Assign pipeline to project", topBlocker.SuggestedAction);
        Assert.AreEqual("replace-binding-source", topBlocker.ExecutionIntent.IntentType);
        Assert.AreEqual("binding-14", topBlocker.ExecutionIntent.NavigationTarget.TargetElementId);

        CollectionAssert.DoesNotContain(
            new[] { problem.SuggestedAction, rootCause.SuggestedAction, topBlocker.SuggestedAction },
            "Create binding for product root");
    }

    private static OnboardingWorkspaceData CreateWorkspaceData(
        OnboardingStatusDto status,
        IReadOnlyList<OnboardingTfsConnectionDto> connections,
        IReadOnlyList<OnboardingProjectSourceDto> projects,
        IReadOnlyList<OnboardingTeamSourceDto> teams,
        IReadOnlyList<OnboardingPipelineSourceDto> pipelines,
        IReadOnlyList<OnboardingProductRootDto> roots,
        IReadOnlyList<OnboardingProductSourceBindingDto> bindings)
        => new(
            new OnboardingWorkspaceFilter(null, null, null, null),
            new OnboardingWorkspaceFilterOptions([], [], [], []),
            status,
            connections,
            projects,
            teams,
            pipelines,
            roots,
            bindings);

    private static OnboardingTfsConnectionDto CreateConnection(
        OnboardingEntityStatusDto? status = null,
        TfsConnectionValidationResultDto? validation = null)
        => new(
            1,
            "connection-1",
            "https://dev.azure.com/example",
            "Pat",
            30,
            "7.1",
            validation ?? new TfsConnectionValidationResultDto(
                "https://dev.azure.com/example",
                "Pat",
                30,
                "7.1",
                CreateValidationState(),
                CreateValidationState(),
                CreateValidationState(),
                DateTime.UtcNow,
                DateTime.UtcNow,
                null,
                null),
            status ?? new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingProjectSourceDto CreateProject(
        OnboardingEntityStatusDto? status = null,
        OnboardingValidationStateDto? validationState = null)
        => new(
            10,
            1,
            "project-1",
            true,
            new ProjectSnapshotDto("project-1", "Project One", null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            validationState ?? CreateValidationState(),
            status ?? new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingTeamSourceDto CreateTeam(
        int projectId,
        OnboardingEntityStatusDto? status = null,
        OnboardingValidationStateDto? validationState = null)
        => new(
            11,
            projectId,
            "team-1",
            true,
            new TeamSnapshotDto("team-1", "project-1", "Team One", "Area", null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            validationState ?? CreateValidationState(),
            status ?? new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingPipelineSourceDto CreatePipeline(
        int projectId,
        OnboardingEntityStatusDto? status = null,
        OnboardingValidationStateDto? validationState = null)
        => new(
            12,
            projectId,
            "pipeline-1",
            true,
            new PipelineSnapshotDto("pipeline-1", "project-1", "Pipeline One", null, null, null, null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            validationState ?? CreateValidationState(),
            status ?? new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingProductRootDto CreateRoot(
        int projectId,
        OnboardingEntityStatusDto? status = null,
        OnboardingValidationStateDto? validationState = null)
        => new(
            13,
            projectId,
            "root-1",
            true,
            new ProductRootSnapshotDto("root-1", "Root One", "Feature", "New", "project-1", "Area", new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            validationState ?? CreateValidationState(),
            status ?? new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingProductSourceBindingDto CreateBinding(
        int rootId,
        int projectId,
        OnboardingProductSourceTypeDto sourceType = OnboardingProductSourceTypeDto.Project,
        string sourceExternalId = "project-1",
        OnboardingEntityStatusDto? status = null,
        OnboardingValidationStateDto? validationState = null)
        => new(
            14,
            rootId,
            projectId,
            sourceType == OnboardingProductSourceTypeDto.Team ? 11 : null,
            sourceType == OnboardingProductSourceTypeDto.Pipeline ? 12 : null,
            sourceType,
            sourceExternalId,
            true,
            validationState ?? CreateValidationState(),
            status ?? new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingValidationStateDto CreateValidationState(OnboardingValidationStatus status = OnboardingValidationStatus.Valid)
        => new(status, DateTime.UtcNow, OnboardingValidationSource.Live, null, null, [], null, null, null);
}
