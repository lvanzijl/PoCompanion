using System.Net;
using System.Text.Json;
using Moq;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class OnboardingExecutionServiceTests
{
    private Mock<IOnboardingCrudClient> _crudClient = null!;
    private Mock<IOnboardingWorkspaceService> _workspaceService = null!;
    private OnboardingExecutionService _service = null!;

    [TestInitialize]
    public void SetUp()
    {
        _crudClient = new Mock<IOnboardingCrudClient>(MockBehavior.Strict);
        _workspaceService = new Mock<IOnboardingWorkspaceService>(MockBehavior.Strict);
        _service = new OnboardingExecutionService(_crudClient.Object, _workspaceService.Object);
    }

    [TestMethod]
    public async Task CreateProjectAsync_RejectsMutationWhenIntentDoesNotMatchProjectSurface()
    {
        var result = await _service.CreateProjectAsync(
            CreateIntent("configure-connection", OnboardingGraphSection.Connections, OnboardingExecutionConfidenceLevel.High, connectionId: 1),
            new CreateProjectSourceRequest(1, "project-1"),
            new OnboardingWorkspaceFilter(1, null, null, null));

        Assert.IsFalse(result.MutationApplied);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.ErrorCode);
        Assert.AreEqual(OnboardingExecutionFeedbackKind.Error, result.Feedback.Kind);
        _crudClient.VerifyNoOtherCalls();
        _workspaceService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task CreateConnectionAsync_BlocksInvalidLocalInputBeforeCallingBackend()
    {
        var result = await _service.CreateConnectionAsync(
            CreateIntent("configure-connection", OnboardingGraphSection.Connections, OnboardingExecutionConfidenceLevel.High),
            new CreateTfsConnectionRequest("", "Pat", 30, "7.1"),
            new OnboardingWorkspaceFilter(null, null, null, null));

        Assert.IsFalse(result.MutationApplied);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.ErrorCode);
        StringAssert.Contains(result.Feedback.Message, "Organization URL is required.");
        _crudClient.VerifyNoOtherCalls();
        _workspaceService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task CreateConnectionAsync_TreatsCreatedResponseAsSuccessAndRefreshesWorkspace()
    {
        var filter = new OnboardingWorkspaceFilter(null, null, null, null);
        var intent = CreateIntent("resolve-validation", OnboardingGraphSection.Connections, OnboardingExecutionConfidenceLevel.Fallback);
        var request = new CreateTfsConnectionRequest("https://dev.azure.com/battleship", "Pat", 30, "7.1");
        var createdEnvelope = new OnboardingSuccessEnvelopeOfOnboardingTfsConnectionDto
        {
            Data = CreateConnection(7, request.OrganizationUrl)
        };

        _crudClient
            .Setup(client => client.CreateConnectionAsync(
                It.Is<CreateTfsConnectionRequest>(item => item.OrganizationUrl == request.OrganizationUrl),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(
                "created",
                (int)HttpStatusCode.Created,
                JsonSerializer.Serialize(createdEnvelope),
                new Dictionary<string, IEnumerable<string>>(),
                null));

        _workspaceService
            .Setup(service => service.GetWorkspaceDataAsync(
                It.Is<OnboardingWorkspaceFilter>(item => item.ConnectionId == 7),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWorkspaceData(filter with { ConnectionId = 7 }));

        var result = await _service.CreateConnectionAsync(intent, request, filter);

        Assert.IsTrue(result.MutationApplied);
        Assert.IsNotNull(result.RefreshedData);
        Assert.AreEqual(7, result.AppliedFilter.ConnectionId);
        Assert.AreEqual(OnboardingExecutionFeedbackKind.Success, result.Feedback.Kind);
        StringAssert.Contains(result.Feedback.Message, request.OrganizationUrl);
    }

    [TestMethod]
    public async Task CreateTeamAsync_SuccessfullyRefreshesWorkspaceAndKeepsProjectContext()
    {
        var filter = new OnboardingWorkspaceFilter(1, 2, null, null);
        var intent = CreateIntent("assign-team", OnboardingGraphSection.Teams, OnboardingExecutionConfidenceLevel.High, connectionId: 1, projectId: 2);
        var request = new CreateTeamSourceRequest(2, "team-1", true, "Team One");

        _crudClient
            .Setup(client => client.CreateTeamAsync(
                It.Is<CreateTeamSourceRequest>(item => item.ProjectSourceId == 2 && item.TeamExternalId == "team-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfOnboardingTeamSourceDto
            {
                Data = CreateTeam(7, 2, "team-1", "Team One")
            });

        _workspaceService
            .Setup(service => service.GetWorkspaceDataAsync(
                It.Is<OnboardingWorkspaceFilter>(item => item.ProjectId == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWorkspaceData(filter));

        var result = await _service.CreateTeamAsync(intent, request, filter);

        Assert.IsTrue(result.MutationApplied);
        Assert.IsNotNull(result.RefreshedData);
        Assert.AreEqual(2, result.AppliedFilter.ProjectId);
        Assert.AreEqual(OnboardingExecutionFeedbackKind.Success, result.Feedback.Kind);
        StringAssert.Contains(result.Feedback.Message, "Team One");
    }

    [TestMethod]
    public async Task CreatePipelineAsync_SurfacesBackendValidationFailure()
    {
        var intent = CreateIntent("assign-pipeline", OnboardingGraphSection.Pipelines, OnboardingExecutionConfidenceLevel.High, connectionId: 1, projectId: 2);
        var request = new CreatePipelineSourceRequest(2, "pipeline-1");
        var apiError = new OnboardingErrorDto(OnboardingErrorCode.ValidationFailed, "Pipeline external ID is invalid.", "PipelineExternalId", false);

        _crudClient
            .Setup(client => client.CreatePipelineAsync(It.IsAny<CreatePipelineSourceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(
                "validation failed",
                (int)HttpStatusCode.BadRequest,
                JsonSerializer.Serialize(apiError),
                new Dictionary<string, IEnumerable<string>>(),
                null));

        var result = await _service.CreatePipelineAsync(intent, request, new OnboardingWorkspaceFilter(1, 2, null, null));

        Assert.IsFalse(result.MutationApplied);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.ErrorCode);
        Assert.AreEqual("Pipeline external ID is invalid.", result.Feedback.Message);
        Assert.IsNull(result.RefreshedData);
        _workspaceService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task CreateBindingAsync_BlocksFallbackIntentWithoutRequiredContext()
    {
        var result = await _service.CreateBindingAsync(
            CreateIntent("resolve-validation", OnboardingGraphSection.Bindings, OnboardingExecutionConfidenceLevel.Fallback, connectionId: 1, projectId: null, rootId: null),
            new CreateProductSourceBindingRequest(0, OnboardingProductSourceTypeDto.Project, ProjectSourceId: 2),
            new OnboardingWorkspaceFilter(1, null, null, null));

        Assert.IsFalse(result.MutationApplied);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.ErrorCode);
        _crudClient.VerifyNoOtherCalls();
        _workspaceService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeleteBindingAsync_RequiresReasonBeforeSoftDelete()
    {
        var result = await _service.DeleteBindingAsync(
            CreateIntent("create-binding", OnboardingGraphSection.Bindings, OnboardingExecutionConfidenceLevel.High, connectionId: 1, projectId: 2, rootId: 3, bindingId: 4),
            4,
            new OnboardingSoftDeleteRequest(""),
            new OnboardingWorkspaceFilter(1, 2, 3, null));

        Assert.IsFalse(result.MutationApplied);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.ErrorCode);
        StringAssert.Contains(result.Feedback.Message, "Deletion reason is required.");
        _crudClient.VerifyNoOtherCalls();
        _workspaceService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ReplaceBindingSourceAsync_SuccessfullyRefreshesWorkspaceWithSelectedTeamSource()
    {
        var filter = new OnboardingWorkspaceFilter(1, 2, 3, null);
        var intent = CreateIntent("replace-binding-source", OnboardingGraphSection.Bindings, OnboardingExecutionConfidenceLevel.High, connectionId: 1, projectId: 2, rootId: 3, bindingId: 4) with
        {
            SelectedReplacementSourceId = 9,
            SelectedReplacementSourceType = OnboardingProductSourceTypeDto.Team
        };

        _crudClient
            .Setup(client => client.UpdateBindingAsync(
                4,
                It.Is<UpdateProductSourceBindingRequest>(request =>
                    request.Enabled == true
                    && request.TeamSourceId == 9
                    && request.PipelineSourceId == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfOnboardingProductSourceBindingDto
            {
                Data = CreateBinding(4, 3, 2, OnboardingProductSourceTypeDto.Team, "team-9", teamSourceId: 9)
            });

        _workspaceService
            .Setup(service => service.GetWorkspaceDataAsync(
                It.Is<OnboardingWorkspaceFilter>(item => item.ProductRootId == 3),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWorkspaceData(filter));

        var result = await _service.ReplaceBindingSourceAsync(intent, true, filter);

        Assert.IsTrue(result.MutationApplied);
        Assert.IsNotNull(result.RefreshedData);
        Assert.AreEqual(OnboardingExecutionFeedbackKind.Success, result.Feedback.Kind);
        StringAssert.Contains(result.Feedback.Message, "team-9");
    }

    [TestMethod]
    public async Task ReplaceBindingSourceAsync_SurfacesPermissionDeniedFailure()
    {
        var intent = CreateIntent("replace-binding-source", OnboardingGraphSection.Bindings, OnboardingExecutionConfidenceLevel.High, connectionId: 1, projectId: 2, rootId: 3, bindingId: 4) with
        {
            SelectedReplacementSourceId = 9,
            SelectedReplacementSourceType = OnboardingProductSourceTypeDto.Pipeline
        };
        var apiError = new OnboardingErrorDto(OnboardingErrorCode.PermissionDenied, "Permission was denied for the requested onboarding mutation.", null, false);

        _crudClient
            .Setup(client => client.UpdateBindingAsync(4, It.IsAny<UpdateProductSourceBindingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(
                "forbidden",
                (int)HttpStatusCode.Forbidden,
                JsonSerializer.Serialize(apiError),
                new Dictionary<string, IEnumerable<string>>(),
                null));

        var result = await _service.ReplaceBindingSourceAsync(intent, true, new OnboardingWorkspaceFilter(1, 2, 3, null));

        Assert.IsFalse(result.MutationApplied);
        Assert.AreEqual(OnboardingErrorCode.PermissionDenied, result.ErrorCode);
        Assert.AreEqual("Permission was denied for the requested onboarding mutation.", result.Feedback.Message);
        _workspaceService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ReplaceBindingSourceAsync_WhenBindingIsStale_RefreshesWorkspace()
    {
        var filter = new OnboardingWorkspaceFilter(1, 2, 3, null);
        var intent = CreateIntent("replace-binding-source", OnboardingGraphSection.Bindings, OnboardingExecutionConfidenceLevel.High, connectionId: 1, projectId: 2, rootId: 3, bindingId: 4) with
        {
            SelectedReplacementSourceId = 9,
            SelectedReplacementSourceType = OnboardingProductSourceTypeDto.Team
        };
        var apiError = new OnboardingErrorDto(OnboardingErrorCode.NotFound, "Onboarding product source binding was not found.", "bindingId=4", false);

        _crudClient
            .Setup(client => client.UpdateBindingAsync(4, It.IsAny<UpdateProductSourceBindingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(
                "not found",
                (int)HttpStatusCode.NotFound,
                JsonSerializer.Serialize(apiError),
                new Dictionary<string, IEnumerable<string>>(),
                null));

        _workspaceService
            .Setup(service => service.GetWorkspaceDataAsync(
                It.Is<OnboardingWorkspaceFilter>(item => item.ProductRootId == 3),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWorkspaceData(filter));

        var result = await _service.ReplaceBindingSourceAsync(intent, true, filter);

        Assert.IsFalse(result.MutationApplied);
        Assert.AreEqual(OnboardingErrorCode.NotFound, result.ErrorCode);
        Assert.IsNotNull(result.RefreshedData);
    }

    [TestMethod]
    public async Task UpdateProjectAsync_WhenBackendReportsNotFound_RefreshesWorkspaceToAvoidStaleState()
    {
        var filter = new OnboardingWorkspaceFilter(1, 2, null, null);
        var intent = CreateIntent("link-project", OnboardingGraphSection.Projects, OnboardingExecutionConfidenceLevel.High, connectionId: 1, projectId: 2);
        var apiError = new OnboardingErrorDto(OnboardingErrorCode.NotFound, "Onboarding project source was not found.", "projectId=2", false);

        _crudClient
            .Setup(client => client.UpdateProjectAsync(2, It.IsAny<UpdateProjectSourceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(
                "not found",
                (int)HttpStatusCode.NotFound,
                JsonSerializer.Serialize(apiError),
                new Dictionary<string, IEnumerable<string>>(),
                null));

        _workspaceService
            .Setup(service => service.GetWorkspaceDataAsync(
                It.Is<OnboardingWorkspaceFilter>(item => item.ProjectId == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWorkspaceData(filter));

        var result = await _service.UpdateProjectAsync(
            intent,
            2,
            new UpdateProjectSourceRequest(true, "Project One", "Description", null, null),
            filter);

        Assert.IsFalse(result.MutationApplied);
        Assert.AreEqual(OnboardingErrorCode.NotFound, result.ErrorCode);
        Assert.IsNotNull(result.RefreshedData);
    }

    private static ExecutionIntentViewModel CreateIntent(
        string intentType,
        OnboardingGraphSection section,
        OnboardingExecutionConfidenceLevel confidenceLevel,
        int? connectionId = null,
        int? projectId = null,
        int? rootId = null,
        int? bindingId = null)
        => new(
            intentType,
            rootId.HasValue ? OnboardingProblemScope.Root : projectId.HasValue ? OnboardingProblemScope.Project : OnboardingProblemScope.Global,
            connectionId,
            projectId,
            rootId,
            bindingId,
            "Test intent",
            confidenceLevel,
            new ExecutionIntentNavigationTargetViewModel(
                "/home/onboarding",
                $"section-{section}",
                section,
                section == OnboardingGraphSection.Bindings && bindingId.HasValue ? $"binding-{bindingId.Value}" : $"section-{section}",
                [section]));

    private static OnboardingWorkspaceData CreateWorkspaceData(OnboardingWorkspaceFilter filter)
        => new(
            filter,
            new OnboardingWorkspaceFilterOptions(
                [new OnboardingFilterOption<int>(null, "All connections")],
                [new OnboardingFilterOption<int>(null, "All projects")],
                [new OnboardingFilterOption<int>(null, "All roots")],
                [new OnboardingFilterOption<OnboardingConfigurationStatus>(null, "All statuses")]),
            new OnboardingStatusDto(
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.Complete,
                OnboardingConfigurationStatus.PartiallyConfigured,
                OnboardingConfigurationStatus.PartiallyConfigured,
                [],
                [],
                new OnboardingStatusCountsDto(1, 1, 1, 1, 1, 1, 1, 1, 1, 1)),
            [CreateConnection(1, "https://dev.azure.com/example")],
            [CreateProject(2, "project-1", "Project One")],
            [],
            [],
            [],
            []);

    private static OnboardingTfsConnectionDto CreateConnection(int id, string organizationUrl)
        => new(
            id,
            $"connection-{id}",
            organizationUrl,
            "Pat",
            30,
            "7.1",
            new TfsConnectionValidationResultDto(
                organizationUrl,
                "Pat",
                30,
                "7.1",
                CreateValidationState(OnboardingValidationStatus.Valid),
                CreateValidationState(OnboardingValidationStatus.Valid),
                CreateValidationState(OnboardingValidationStatus.Valid),
                DateTime.UtcNow,
                DateTime.UtcNow,
                null,
                "Read"),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingProjectSourceDto CreateProject(int id, string projectExternalId, string name)
        => new(
            id,
            1,
            projectExternalId,
            true,
            new ProjectSnapshotDto(projectExternalId, name, null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(OnboardingValidationStatus.Valid),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingTeamSourceDto CreateTeam(int id, int projectId, string teamExternalId, string name)
        => new(
            id,
            projectId,
            teamExternalId,
            true,
            new TeamSnapshotDto(teamExternalId, "project-1", name, "Area", null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(OnboardingValidationStatus.Valid),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingProductSourceBindingDto CreateBinding(
        int id,
        int rootId,
        int projectId,
        OnboardingProductSourceTypeDto sourceType,
        string sourceExternalId,
        int? teamSourceId = null,
        int? pipelineSourceId = null)
        => new(
            id,
            rootId,
            projectId,
            teamSourceId,
            pipelineSourceId,
            sourceType,
            sourceExternalId,
            true,
            CreateValidationState(OnboardingValidationStatus.Valid),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingValidationStateDto CreateValidationState(OnboardingValidationStatus status)
        => new(status, DateTime.UtcNow, OnboardingValidationSource.Live, null, null, [], null, null, null);
}
