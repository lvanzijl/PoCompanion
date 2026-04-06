using Moq;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Api.Services.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Services.Onboarding;

[TestClass]
public sealed class OnboardingValidationServiceTests
{
    private Mock<IOnboardingLiveLookupClient> _lookupClient = null!;
    private OnboardingValidationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _lookupClient = new Mock<IOnboardingLiveLookupClient>(MockBehavior.Strict);
        _service = new OnboardingValidationService(
            _lookupClient.Object,
            new OnboardingSnapshotMapper(),
            Mock.Of<IOnboardingObservability>());
    }

    [TestMethod]
    public async Task ValidateConnectionAsync_WithAccessibleProjects_ReturnsValid()
    {
        var connection = CreateConnection();
        _lookupClient
            .Setup(client => client.GetProjectsAsync(connection, null, 1, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(new[]
            {
                new ProjectLookupResultDto("project-1", "Project One", null)
            }));

        var result = await _service.ValidateConnectionAsync(connection, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(OnboardingValidationStatus.Valid, result.Data!.AvailabilityValidationState.Status);
        Assert.AreEqual(OnboardingValidationStatus.Valid, result.Data.PermissionValidationState.Status);
        Assert.AreEqual(OnboardingValidationStatus.Valid, result.Data.CapabilityValidationState.Status);
    }

    [TestMethod]
    public async Task ValidateConnectionAsync_WithValidationFailure_ReturnsInvalid()
    {
        var connection = CreateConnection();
        _lookupClient
            .Setup(client => client.GetProjectsAsync(connection, null, 1, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Failure(
                new OnboardingErrorDto(OnboardingErrorCode.ValidationFailed, "Invalid connection", "organizationUrl", false)));

        var result = await _service.ValidateConnectionAsync(connection, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.Error!.Code);
    }

    [TestMethod]
    public async Task ValidateProjectSourceAsync_WithPermissionDenied_ReturnsPermissionDenied()
    {
        var connection = CreateConnection();
        var projectSource = new ProjectSource { ProjectExternalId = "project-1" };
        _lookupClient
            .Setup(client => client.GetProjectsAsync(connection, null, int.MaxValue, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Failure(
                new OnboardingErrorDto(OnboardingErrorCode.PermissionDenied, "Denied", null, false)));

        var result = await _service.ValidateProjectSourceAsync(connection, projectSource, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.PermissionDenied, result.Error!.Code);
    }

    [TestMethod]
    public async Task ValidateProductRootAsync_WhenWorkItemMissing_ReturnsNotFound()
    {
        var connection = CreateConnection();
        var projectSource = new ProjectSource { ProjectExternalId = "project-1" };
        var productRoot = new ProductRoot { WorkItemExternalId = "101" };
        _lookupClient
            .Setup(client => client.GetWorkItemAsync(connection, "101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<WorkItemLookupResultDto>.Failure(
                new OnboardingErrorDto(OnboardingErrorCode.NotFound, "Missing", "101", false)));

        var result = await _service.ValidateProductRootAsync(connection, projectSource, productRoot, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.NotFound, result.Error!.Code);
    }

    [TestMethod]
    public async Task ValidateTeamSourceAsync_WhenProjectScopeDiffers_ReturnsValidationFailed()
    {
        var connection = CreateConnection();
        var projectSource = new ProjectSource { ProjectExternalId = "project-1" };
        var teamSource = new TeamSource { TeamExternalId = "team-1" };
        _lookupClient
            .Setup(client => client.GetTeamsAsync(connection, "project-1", null, int.MaxValue, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Success(new[]
            {
                new TeamLookupResultDto("team-1", "project-2", "Team One", null, "Project\\Team One")
            }));

        var result = await _service.ValidateTeamSourceAsync(connection, projectSource, teamSource, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.Error!.Code);
    }

    [TestMethod]
    public async Task ValidatePipelineSourceAsync_WhenProjectScopeDiffers_ReturnsValidationFailed()
    {
        var connection = CreateConnection();
        var projectSource = new ProjectSource { ProjectExternalId = "project-1" };
        var pipelineSource = new PipelineSource { PipelineExternalId = "55" };
        _lookupClient
            .Setup(client => client.GetPipelinesAsync(connection, "project-1", null, int.MaxValue, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Success(new[]
            {
                new PipelineLookupResultDto("55", "project-2", "Pipeline", "\\Folder", "/azure-pipelines.yml", "repo-1", "Repo")
            }));

        var result = await _service.ValidatePipelineSourceAsync(connection, projectSource, pipelineSource, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.Error!.Code);
    }

    [TestMethod]
    public async Task ValidateProductSourceBindingAsync_WhenBindingScopeMismatches_ReturnsValidationFailed()
    {
        var connection = CreateConnection();
        var projectSource = new ProjectSource { Id = 1, ProjectExternalId = "project-1" };
        var productRoot = new ProductRoot { WorkItemExternalId = "101" };
        var binding = new ProductSourceBinding
        {
            SourceType = ProductSourceType.Team,
            SourceExternalId = "team-2",
            ProjectSourceId = 1
        };
        var teamSource = new TeamSource { TeamExternalId = "team-1", ProjectSourceId = 1 };

        _lookupClient
            .Setup(client => client.GetWorkItemAsync(connection, "101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<WorkItemLookupResultDto>.Success(
                new WorkItemLookupResultDto("101", "Root", "Epic", "New", "project-1", "Project\\Area")));
        _lookupClient
            .Setup(client => client.GetTeamsAsync(connection, "project-1", null, int.MaxValue, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Success(new[]
            {
                new TeamLookupResultDto("team-1", "project-1", "Team One", null, "Project\\Team One")
            }));

        var result = await _service.ValidateProductSourceBindingAsync(connection, projectSource, productRoot, binding, teamSource, null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.Error!.Code);
    }

    [TestMethod]
    public async Task ValidatePipelineSourceAsync_WhenLookupUnavailable_ReturnsTfsUnavailable()
    {
        var connection = CreateConnection();
        var projectSource = new ProjectSource { ProjectExternalId = "project-1" };
        var pipelineSource = new PipelineSource { PipelineExternalId = "55" };
        _lookupClient
            .Setup(client => client.GetPipelinesAsync(connection, "project-1", null, int.MaxValue, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Failure(
                new OnboardingErrorDto(OnboardingErrorCode.TfsUnavailable, "Unavailable", null, true)));

        var result = await _service.ValidatePipelineSourceAsync(connection, projectSource, pipelineSource, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.TfsUnavailable, result.Error!.Code);
    }

    private static TfsConnection CreateConnection()
        => new()
        {
            OrganizationUrl = "https://dev.azure.com/example",
            AuthenticationMode = "Ntlm",
            TimeoutSeconds = 30,
            ApiVersion = "7.1"
        };
}
