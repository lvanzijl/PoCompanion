using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Exceptions;
using PoTool.Api.Services;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class LivePipelineReadProviderDataSourceEnforcementTests
{
    [TestMethod]
    public async Task GetAllAsync_CacheMode_ThrowsInvalidDataSourceUsageException()
    {
        var mockTfsClient = new Mock<ITfsClient>();
        var mockProductRepository = new Mock<IProductRepository>();
        var mockRepositoryConfigRepository = new Mock<IRepositoryConfigRepository>();
        var mockLogger = new Mock<ILogger<LivePipelineReadProvider>>();
        var mockModeProvider = new Mock<IDataSourceModeProvider>();
        mockModeProvider.SetupGet(provider => provider.Mode).Returns(DataSourceMode.Cache);

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        httpContextAccessor.HttpContext.Request.Path = "/api/pipelines";

        var provider = new LivePipelineReadProvider(
            mockTfsClient.Object,
            mockProductRepository.Object,
            mockRepositoryConfigRepository.Object,
            mockLogger.Object,
            mockModeProvider.Object,
            httpContextAccessor);

        try
        {
            await provider.GetAllAsync();
            Assert.Fail("Expected InvalidDataSourceUsageException was not thrown.");
        }
        catch (InvalidDataSourceUsageException)
        {
            // Expected path
        }
    }

    [TestMethod]
    public async Task GetAllAsync_LiveMode_AllowsLiveProviderCall()
    {
        var mockTfsClient = new Mock<ITfsClient>();
        mockTfsClient
            .Setup(client => client.GetPipelinesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PipelineDto(1, "Pipeline 1", PipelineType.Build, "/api", DateTimeOffset.UtcNow)
            ]);

        var mockProductRepository = new Mock<IProductRepository>();
        var mockRepositoryConfigRepository = new Mock<IRepositoryConfigRepository>();
        var mockLogger = new Mock<ILogger<LivePipelineReadProvider>>();
        var mockModeProvider = new Mock<IDataSourceModeProvider>();
        mockModeProvider.SetupGet(provider => provider.Mode).Returns(DataSourceMode.Live);

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        httpContextAccessor.HttpContext.Request.Path = "/api/pipelines/definitions";

        var provider = new LivePipelineReadProvider(
            mockTfsClient.Object,
            mockProductRepository.Object,
            mockRepositoryConfigRepository.Object,
            mockLogger.Object,
            mockModeProvider.Object,
            httpContextAccessor);

        var result = (await provider.GetAllAsync()).ToList();

        Assert.HasCount(1, result);
        Assert.AreEqual(1, result[0].Id);
    }
}
