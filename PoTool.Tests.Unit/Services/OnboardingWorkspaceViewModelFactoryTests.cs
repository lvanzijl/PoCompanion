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
    public void Create_OrdersTopBlockersAndWarnings_ByExistingSignalsOnly()
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
        Assert.AreEqual("Global blocker", result.ProblemSummary.TopBlockers[0].Title);
        Assert.IsTrue(result.ProblemSummary.TopBlockers.All(problem => problem.FixFirst));
        Assert.AreEqual(OnboardingProblemScope.Global, result.ProblemSummary.TopBlockers[0].Scope);
        Assert.AreEqual(OnboardingGraphSection.Connections, result.ProblemSummary.TopBlockers[0].GraphSection);
        Assert.AreEqual("binding-14", result.ProblemSummary.TopBlockers[3].TargetElementId);
        CollectionAssert.AreEquivalent(
            new[] { "Global warning", "Root metadata needs review" },
            result.ProblemSummary.Warnings.Select(problem => problem.Title).ToArray());
    }

    [TestMethod]
    public void Create_ProblemItemsExposeScopeReasonAndNavigationTarget()
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

        var teamProblem = result.ProblemGroups
            .Single(group => group.Scope == OnboardingProblemScope.Project)
            .Items
            .Single(problem => problem.TargetElementId == "team-11");

        Assert.AreEqual(OnboardingProblemSeverity.Warning, teamProblem.Severity);
        StringAssert.Contains(teamProblem.Reason, "external source is currently unavailable");
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
        Assert.AreEqual(0, result.GraphSections.Single(section => section.Section == OnboardingGraphSection.Connections).BlockingCount);
        Assert.IsTrue(result.GraphSections.Single(section => section.Section == OnboardingGraphSection.Bindings).DefaultExpanded);
        Assert.AreEqual("binding-14", result.ProblemSummary?.TopBlockers[0].TargetElementId);
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
        OnboardingEntityStatusDto? status = null,
        OnboardingValidationStateDto? validationState = null)
        => new(
            14,
            rootId,
            projectId,
            null,
            null,
            OnboardingProductSourceTypeDto.Project,
            "project-1",
            true,
            validationState ?? CreateValidationState(),
            status ?? new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingValidationStateDto CreateValidationState(OnboardingValidationStatus status = OnboardingValidationStatus.Valid)
        => new(status, DateTime.UtcNow, OnboardingValidationSource.Live, null, null, [], null, null, null);
}
