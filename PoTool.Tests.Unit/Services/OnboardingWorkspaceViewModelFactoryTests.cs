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

    private static OnboardingTfsConnectionDto CreateConnection()
        => new(
            1,
            "connection-1",
            "https://dev.azure.com/example",
            "Pat",
            30,
            "7.1",
            new TfsConnectionValidationResultDto(
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
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingProjectSourceDto CreateProject()
        => new(
            10,
            1,
            "project-1",
            true,
            new ProjectSnapshotDto("project-1", "Project One", null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingTeamSourceDto CreateTeam(int projectId)
        => new(
            11,
            projectId,
            "team-1",
            true,
            new TeamSnapshotDto("team-1", "project-1", "Team One", "Area", null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingPipelineSourceDto CreatePipeline(int projectId)
        => new(
            12,
            projectId,
            "pipeline-1",
            true,
            new PipelineSnapshotDto("pipeline-1", "project-1", "Pipeline One", null, null, null, null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingProductRootDto CreateRoot(int projectId)
        => new(
            13,
            projectId,
            "root-1",
            true,
            new ProductRootSnapshotDto("root-1", "Root One", "Feature", "New", "project-1", "Area", new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingProductSourceBindingDto CreateBinding(int rootId, int projectId)
        => new(
            14,
            rootId,
            projectId,
            null,
            null,
            OnboardingProductSourceTypeDto.Project,
            "project-1",
            true,
            CreateValidationState(),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingValidationStateDto CreateValidationState()
        => new(OnboardingValidationStatus.Valid, DateTime.UtcNow, OnboardingValidationSource.Live, null, null, [], null, null, null);
}
