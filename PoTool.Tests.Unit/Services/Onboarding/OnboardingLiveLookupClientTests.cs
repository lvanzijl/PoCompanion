using Moq;
using PoTool.Api.Configuration;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Api.Services.Onboarding;
using PoTool.Core.Contracts;
using PoTool.Shared.Onboarding;
using PoTool.Shared.Pipelines;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services.Onboarding;

[TestClass]
public sealed class OnboardingLiveLookupClientTests
{
    private Mock<ITfsClient> _tfsClient = null!;
    private OnboardingLiveLookupClient _service = null!;
    private TfsConnection _connection = null!;

    [TestInitialize]
    public void Setup()
    {
        _tfsClient = new Mock<ITfsClient>(MockBehavior.Strict);
        _connection = new TfsConnection
        {
            OrganizationUrl = "https://dev.azure.com/example",
            AuthenticationMode = "Ntlm",
            TimeoutSeconds = 30,
            ApiVersion = "7.1"
        };
        _service = new OnboardingLiveLookupClient(
            new FakeScopedTfsClientFactory(_tfsClient.Object),
            Mock.Of<IOnboardingObservability>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<OnboardingLiveLookupClient>>());
    }

    [TestMethod]
    public async Task GetProjectsAsync_ReturnsFilteredProjects()
    {
        _tfsClient
            .Setup(client => client.GetTfsProjectsAsync(_connection.OrganizationUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TfsProjectDto("project-1", "Alpha", "Primary"),
                new TfsProjectDto("project-2", "Beta", "Secondary")
            });

        var result = await _service.GetProjectsAsync(_connection, "alp", 10, 0, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.HasCount(1, result.Data!);
        Assert.AreEqual("project-1", result.Data![0].ProjectExternalId);
    }

    [TestMethod]
    public async Task GetTeamsAsync_ReturnsTeamsScopedToProject()
    {
        SetupProjectResolution();
        _tfsClient
            .Setup(client => client.GetTfsTeamsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TfsTeamDto("team-1", "Delivery Team", "Alpha", "Team description", "Alpha\\Delivery Team")
            });

        var result = await _service.GetTeamsAsync(_connection, "project-1", "delivery", 10, 0, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.HasCount(1, result.Data!);
        Assert.AreEqual("project-1", result.Data![0].ProjectExternalId);
        Assert.AreEqual("team-1", result.Data[0].TeamExternalId);
    }

    [TestMethod]
    public async Task GetPipelinesAsync_ReturnsPipelineDefinitionsScopedToProject()
    {
        SetupProjectResolution();
        _tfsClient
            .Setup(client => client.GetGitRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ("RepoA", "repo-a") });
        _tfsClient
            .Setup(client => client.GetPipelineDefinitionsForRepositoryAsync("RepoA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PipelineDefinitionDto
                {
                    PipelineDefinitionId = 42,
                    RepoId = "repo-a",
                    RepoName = "RepoA",
                    Name = "Deploy Alpha",
                    YamlPath = "/azure-pipelines.yml",
                    Folder = "\\Apps",
                    LastSyncedUtc = DateTimeOffset.UtcNow
                }
            });

        var result = await _service.GetPipelinesAsync(_connection, "project-1", "deploy", 10, 0, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.HasCount(1, result.Data!);
        Assert.AreEqual("42", result.Data![0].PipelineExternalId);
        Assert.AreEqual("repo-a", result.Data[0].RepositoryExternalId);
    }

    [TestMethod]
    public async Task SearchWorkItemsAsync_AppliesTypeFilter()
    {
        SetupProjectResolution();
        _tfsClient
            .Setup(client => client.GetWorkItemsAsync("Alpha", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WorkItemDto(100, "Epic", "Epic Root", null, "Alpha\\Area", "Alpha\\Sprint 1", "New", DateTimeOffset.UtcNow, null, null),
                new WorkItemDto(101, "Bug", "Bug Root", null, "Alpha\\Area", "Alpha\\Sprint 1", "New", DateTimeOffset.UtcNow, null, null)
            });

        var result = await _service.SearchWorkItemsAsync(_connection, "root", "project-1", new[] { "Epic" }, 10, 0, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.HasCount(1, result.Data!);
        Assert.AreEqual("100", result.Data![0].WorkItemExternalId);
        Assert.AreEqual("Epic", result.Data[0].WorkItemType);
    }

    [TestMethod]
    public async Task GetWorkItemAsync_ReturnsSingleWorkItem()
    {
        SetupProjectResolution();
        _tfsClient
            .Setup(client => client.GetWorkItemByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemDto(100, "Epic", "Epic Root", null, "Alpha\\Area", "Alpha\\Sprint 1", "New", DateTimeOffset.UtcNow, null, null));

        var result = await _service.GetWorkItemAsync(_connection, "100", CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("100", result.Data!.WorkItemExternalId);
        Assert.AreEqual("project-1", result.Data.ProjectExternalId);
    }

    private void SetupProjectResolution()
    {
        _tfsClient
            .Setup(client => client.GetTfsProjectsAsync(_connection.OrganizationUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TfsProjectDto("project-1", "Alpha", "Primary")
            });
    }

    private sealed class FakeScopedTfsClientFactory : IOnboardingScopedTfsClientFactory
    {
        private readonly ITfsClient _client;

        public FakeScopedTfsClientFactory(ITfsClient client)
        {
            _client = client;
        }

        public IAsyncDisposableTfsClientSession CreateSession(TfsConnection connection, string? projectName = null, string? defaultAreaPath = null)
            => new FakeSession(_client);

        private sealed class FakeSession : IAsyncDisposableTfsClientSession
        {
            public FakeSession(ITfsClient client)
            {
                Client = client;
            }

            public ITfsClient Client { get; }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
