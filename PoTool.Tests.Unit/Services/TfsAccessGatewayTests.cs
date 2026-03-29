using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Configuration;
using PoTool.Api.Exceptions;
using PoTool.Api.Services;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class TfsAccessGatewayTests
{
    [TestMethod]
    public async Task GetPipelinesAsync_RequestInCacheMode_ThrowsInvalidDataSourceUsageException()
    {
        var innerClient = new Mock<ITfsClient>(MockBehavior.Strict);
        var modeProvider = new Mock<IDataSourceModeProvider>();
        modeProvider.SetupGet(provider => provider.Mode).Returns(DataSourceMode.Cache);

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        httpContextAccessor.HttpContext.Request.Path = "/api/pipelines";

        var gateway = new TfsAccessGateway(
            innerClient.Object,
            new TfsRuntimeMode(useMockClient: false),
            modeProvider.Object,
            httpContextAccessor,
            Mock.Of<ILogger<TfsAccessGateway>>());

        await Assert.ThrowsExactlyAsync<InvalidDataSourceUsageException>(() => gateway.GetPipelinesAsync());

        innerClient.Verify(client => client.GetPipelinesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetPipelineDefinitionsForRepositoryAsync_BackgroundExecution_AllowsReadAccess()
    {
        var expectedDefinitions = new[]
        {
            new PipelineDefinitionDto
            {
                PipelineDefinitionId = 42,
                Name = "Repo Pipeline",
                RepoName = "RepoA",
                RepoId = "repo-a",
                YamlPath = "/azure-pipelines.yml",
                Url = "https://example.test/pipelines/42",
                ProductId = null,
                RepositoryId = null,
                DefaultBranch = "refs/heads/main",
                LastSyncedUtc = DateTimeOffset.UtcNow
            }
        };

        var innerClient = new Mock<ITfsClient>(MockBehavior.Strict);
        innerClient
            .Setup(client => client.GetPipelineDefinitionsForRepositoryAsync("RepoA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDefinitions);

        var modeProvider = new Mock<IDataSourceModeProvider>();
        modeProvider.SetupGet(provider => provider.Mode).Returns(DataSourceMode.Cache);

        var gateway = new TfsAccessGateway(
            innerClient.Object,
            new TfsRuntimeMode(useMockClient: false),
            modeProvider.Object,
            new HttpContextAccessor(),
            Mock.Of<ILogger<TfsAccessGateway>>());

        var result = await gateway.GetPipelineDefinitionsForRepositoryAsync("RepoA");

        CollectionAssert.AreEqual(expectedDefinitions, result.ToArray());
        innerClient.Verify(client => client.GetPipelineDefinitionsForRepositoryAsync("RepoA", It.IsAny<CancellationToken>()), Times.Once);
    }
}
