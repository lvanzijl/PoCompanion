using System.Net;
using Moq;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class OnboardingWorkspaceServiceTests
{
    private Mock<IOnboardingCrudClient> _crudClient = null!;
    private Mock<IOnboardingStatusClient> _statusClient = null!;
    private OnboardingWorkspaceService _service = null!;

    [TestInitialize]
    public void SetUp()
    {
        _crudClient = new Mock<IOnboardingCrudClient>(MockBehavior.Strict);
        _statusClient = new Mock<IOnboardingStatusClient>(MockBehavior.Strict);
        _service = new OnboardingWorkspaceService(_crudClient.Object, _statusClient.Object);
    }

    [TestMethod]
    public async Task GetWorkspaceDataAsync_UsesExistingBackendFiltersAndBuildsOptions()
    {
        var filter = new OnboardingWorkspaceFilter(1, 2, 3, OnboardingConfigurationStatus.PartiallyConfigured);
        var connection = CreateConnection(1, "https://dev.azure.com/example");
        var project = CreateProject(2, "project-1", "Project One");
        var root = CreateRoot(3, 2, "root-1", "Root One");
        var team = CreateTeam(4, 2, "team-1", "Team One");
        var pipeline = CreatePipeline(5, 2, "pipeline-1", "Pipeline One");
        var binding = CreateBinding(6, 3, 2, "project-1");

        _statusClient
            .Setup(client => client.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfOnboardingStatusDto
            {
                Data = new OnboardingStatusDto(
                    OnboardingConfigurationStatus.PartiallyConfigured,
                    OnboardingConfigurationStatus.Complete,
                    OnboardingConfigurationStatus.PartiallyConfigured,
                    OnboardingConfigurationStatus.PartiallyConfigured,
                    [],
                    [],
                    new OnboardingStatusCountsDto(1, 1, 1, 1, 1, 1, 1, 1, 1, 1))
            });

        _crudClient
            .Setup(client => client.ListConnectionsAsync(filter.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingTfsConnectionDto { Data = [connection] });
        _crudClient
            .Setup(client => client.ListProjectsAsync(filter.ConnectionId, filter.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProjectSourceDto { Data = [project] });
        _crudClient
            .Setup(client => client.ListTeamsAsync(filter.ConnectionId, filter.ProjectId, filter.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingTeamSourceDto { Data = [team] });
        _crudClient
            .Setup(client => client.ListPipelinesAsync(filter.ConnectionId, filter.ProjectId, filter.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingPipelineSourceDto { Data = [pipeline] });
        _crudClient
            .Setup(client => client.ListRootsAsync(filter.ConnectionId, filter.ProjectId, filter.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProductRootDto { Data = [root] });
        _crudClient
            .Setup(client => client.ListBindingsAsync(filter.ConnectionId, filter.ProjectId, filter.ProductRootId, filter.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProductSourceBindingDto { Data = [binding] });

        _crudClient
            .Setup(client => client.ListConnectionsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingTfsConnectionDto { Data = [connection] });
        _crudClient
            .Setup(client => client.ListProjectsAsync(filter.ConnectionId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProjectSourceDto { Data = [project] });
        _crudClient
            .Setup(client => client.ListRootsAsync(filter.ConnectionId, filter.ProjectId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProductRootDto { Data = [root] });

        var result = await _service.GetWorkspaceDataAsync(filter, CancellationToken.None);

        Assert.AreEqual(filter, result.AppliedFilter);
        Assert.HasCount(1, result.Connections);
        Assert.AreEqual("Project One", result.Projects[0].Snapshot.Name);
        Assert.AreEqual(1, result.FilterOptions.Connections.Count(option => option.Value == connection.Id));
        Assert.AreEqual(1, result.FilterOptions.Projects.Count(option => option.Value == project.Id));
        Assert.AreEqual(1, result.FilterOptions.ProductRoots.Count(option => option.Value == root.Id));
        Assert.IsTrue(result.FilterOptions.Statuses.Any(option => option.Value == OnboardingConfigurationStatus.Complete));
    }

    [TestMethod]
    public async Task GetWorkspaceDataAsync_TranslatesApiExceptions()
    {
        var filter = new OnboardingWorkspaceFilter(null, null, null, null);

        _statusClient
            .Setup(client => client.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("boom", (int)HttpStatusCode.BadGateway, string.Empty, new Dictionary<string, IEnumerable<string>>(), null));

        _crudClient.Setup(client => client.ListConnectionsAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingTfsConnectionDto());
        _crudClient.Setup(client => client.ListProjectsAsync(null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProjectSourceDto());
        _crudClient.Setup(client => client.ListTeamsAsync(null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingTeamSourceDto());
        _crudClient.Setup(client => client.ListPipelinesAsync(null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingPipelineSourceDto());
        _crudClient.Setup(client => client.ListRootsAsync(null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProductRootDto());
        _crudClient.Setup(client => client.ListBindingsAsync(null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProductSourceBindingDto());
        _crudClient.Setup(client => client.ListProjectsAsync(null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProjectSourceDto());
        _crudClient.Setup(client => client.ListRootsAsync(null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfOnboardingProductRootDto());

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => _service.GetWorkspaceDataAsync(filter, CancellationToken.None));
    }

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
            CreateEntityStatus(OnboardingConfigurationStatus.Complete),
            CreateAudit());

    private static OnboardingProjectSourceDto CreateProject(int id, string projectExternalId, string name)
        => new(
            id,
            1,
            projectExternalId,
            true,
            new ProjectSnapshotDto(projectExternalId, name, null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(OnboardingValidationStatus.Valid),
            CreateEntityStatus(OnboardingConfigurationStatus.Complete),
            CreateAudit());

    private static OnboardingTeamSourceDto CreateTeam(int id, int projectId, string teamExternalId, string name)
        => new(
            id,
            projectId,
            teamExternalId,
            true,
            new TeamSnapshotDto(teamExternalId, "project-1", name, "Area", null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(OnboardingValidationStatus.Valid),
            CreateEntityStatus(OnboardingConfigurationStatus.Complete),
            CreateAudit());

    private static OnboardingPipelineSourceDto CreatePipeline(int id, int projectId, string pipelineExternalId, string name)
        => new(
            id,
            projectId,
            pipelineExternalId,
            true,
            new PipelineSnapshotDto(pipelineExternalId, "project-1", name, null, null, null, null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(OnboardingValidationStatus.Valid),
            CreateEntityStatus(OnboardingConfigurationStatus.Complete),
            CreateAudit());

    private static OnboardingProductRootDto CreateRoot(int id, int projectId, string workItemExternalId, string title)
        => new(
            id,
            projectId,
            workItemExternalId,
            true,
            new ProductRootSnapshotDto(workItemExternalId, title, "Feature", "New", "project-1", "Area", new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(OnboardingValidationStatus.Valid),
            CreateEntityStatus(OnboardingConfigurationStatus.Complete),
            CreateAudit());

    private static OnboardingProductSourceBindingDto CreateBinding(int id, int rootId, int projectId, string sourceExternalId)
        => new(
            id,
            rootId,
            projectId,
            null,
            null,
            OnboardingProductSourceTypeDto.Project,
            sourceExternalId,
            true,
            CreateValidationState(OnboardingValidationStatus.Valid),
            CreateEntityStatus(OnboardingConfigurationStatus.Complete),
            CreateAudit());

    private static OnboardingEntityStatusDto CreateEntityStatus(OnboardingConfigurationStatus status)
        => new(status, [], []);

    private static OnboardingValidationStateDto CreateValidationState(OnboardingValidationStatus status)
        => new(status, DateTime.UtcNow, OnboardingValidationSource.Live, null, null, [], null, null, null);

    private static OnboardingAuditDto CreateAudit()
        => new(DateTime.UtcNow, DateTime.UtcNow, null, null);
}
