using System.Net;
using System.Text.Json;
using Moq;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class OnboardingBindingReplacementLookupServiceTests
{
    private Mock<IOnboardingLookupClient> _lookupClient = null!;
    private OnboardingBindingReplacementLookupService _service = null!;

    [TestInitialize]
    public void SetUp()
    {
        _lookupClient = new Mock<IOnboardingLookupClient>(MockBehavior.Strict);
        _service = new OnboardingBindingReplacementLookupService(_lookupClient.Object);
    }

    [TestMethod]
    public async Task GetCandidatesAsync_ReturnsTeamCandidatesThatExistInCurrentOnboardingContext()
    {
        _lookupClient
            .Setup(client => client.GetTeamsAsync("project-1", null, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfTeamLookupResultDto
            {
                Data =
                [
                    new TeamLookupResultDto("team-2", "project-1", "Team Two", null, "Area"),
                    new TeamLookupResultDto("team-9", "project-1", "Team Nine", null, "Area")
                ]
            });

        var result = await _service.GetCandidatesAsync(
            new OnboardingProjectContextViewModel(1, "project-1", "Project One"),
            CreateBinding(OnboardingProductSourceTypeDto.Team, "team-1"),
            OnboardingProductSourceTypeDto.Team,
            [
                CreateTeam(6, 1, "team-1", "Team One", OnboardingValidationStatus.Invalid),
                CreateTeam(7, 1, "team-2", "Team Two"),
                CreateTeam(8, 1, "team-3", "Team Three")
            ],
            []);

        Assert.IsFalse(result.LookupFailed);
        Assert.IsNull(result.Message);
        Assert.HasCount(1, result.Candidates);
        Assert.AreEqual(7, result.Candidates[0].SourceId);
        Assert.AreEqual("Team Two", result.Candidates[0].DisplayName);
        Assert.AreEqual("team-2", result.Candidates[0].Identifier);
    }

    [TestMethod]
    public async Task GetCandidatesAsync_ReturnsEmptyMessageWhenPipelineLookupHasNoCandidates()
    {
        _lookupClient
            .Setup(client => client.GetPipelinesAsync("project-1", null, 100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnboardingSuccessEnvelopeOfIReadOnlyListOfPipelineLookupResultDto
            {
                Data = []
            });

        var result = await _service.GetCandidatesAsync(
            new OnboardingProjectContextViewModel(1, "project-1", "Project One"),
            CreateBinding(OnboardingProductSourceTypeDto.Pipeline, "pipeline-1"),
            OnboardingProductSourceTypeDto.Pipeline,
            [],
            [CreatePipeline(9, 1, "pipeline-1", "Pipeline One")]);

        Assert.IsFalse(result.LookupFailed);
        Assert.AreEqual("No replacement pipeline candidates are available for Project One.", result.Message);
        Assert.IsEmpty(result.Candidates);
    }

    [TestMethod]
    public async Task GetCandidatesAsync_ReturnsPermissionDeniedMessageWhenLookupFails()
    {
        var apiError = new OnboardingErrorDto(OnboardingErrorCode.PermissionDenied, "TFS denied the requested lookup.", null, false);
        _lookupClient
            .Setup(client => client.GetTeamsAsync("project-1", null, 100, 0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException(
                "permission denied",
                (int)HttpStatusCode.Forbidden,
                JsonSerializer.Serialize(apiError),
                new Dictionary<string, IEnumerable<string>>(),
                null));

        var result = await _service.GetCandidatesAsync(
            new OnboardingProjectContextViewModel(1, "project-1", "Project One"),
            CreateBinding(OnboardingProductSourceTypeDto.Team, "team-1"),
            OnboardingProductSourceTypeDto.Team,
            [CreateTeam(7, 1, "team-2", "Team Two")],
            []);

        Assert.IsTrue(result.LookupFailed);
        Assert.AreEqual("TFS denied the requested lookup.", result.Message);
        Assert.IsEmpty(result.Candidates);
    }

    private static OnboardingTeamSourceDto CreateTeam(int id, int projectId, string teamExternalId, string name, OnboardingValidationStatus validationStatus = OnboardingValidationStatus.Valid)
        => new(
            id,
            projectId,
            teamExternalId,
            true,
            new TeamSnapshotDto(teamExternalId, "project-1", name, "Area", null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(validationStatus),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingPipelineSourceDto CreatePipeline(int id, int projectId, string pipelineExternalId, string name)
        => new(
            id,
            projectId,
            pipelineExternalId,
            true,
            new PipelineSnapshotDto(pipelineExternalId, "project-1", name, null, null, null, null, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)),
            CreateValidationState(OnboardingValidationStatus.Valid),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.Complete, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingProductSourceBindingDto CreateBinding(OnboardingProductSourceTypeDto sourceType, string sourceExternalId)
        => new(
            4,
            3,
            1,
            sourceType == OnboardingProductSourceTypeDto.Team ? 6 : null,
            sourceType == OnboardingProductSourceTypeDto.Pipeline ? 9 : null,
            sourceType,
            sourceExternalId,
            true,
            CreateValidationState(OnboardingValidationStatus.Valid),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.PartiallyConfigured, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));

    private static OnboardingValidationStateDto CreateValidationState(OnboardingValidationStatus status)
        => new(status, DateTime.UtcNow, OnboardingValidationSource.Live, null, null, [], null, null, null);
}
