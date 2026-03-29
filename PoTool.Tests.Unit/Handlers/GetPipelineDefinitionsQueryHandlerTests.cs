using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Pipelines;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines.Queries;
using PoTool.Shared.Pipelines;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetPipelineDefinitionsQueryHandlerTests
{
    private ServiceProvider _serviceProvider = null!;
    private Mock<IPipelineReadProvider> _mockLiveProvider = null!;
    private Mock<ILogger<GetPipelineDefinitionsQueryHandler>> _mockLogger = null!;
    private GetPipelineDefinitionsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        _mockLiveProvider = new Mock<IPipelineReadProvider>();
        _mockLogger = new Mock<ILogger<GetPipelineDefinitionsQueryHandler>>();

        services.AddKeyedScoped<IPipelineReadProvider>("Live", (_, _) => _mockLiveProvider.Object);
        _serviceProvider = services.BuildServiceProvider();

        _handler = new GetPipelineDefinitionsQueryHandler(
            _serviceProvider,
            _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }

    [TestMethod]
    public async Task Handle_WithNoProductIdOrRepositoryId_ThrowsInvalidOperationException()
    {
        // Arrange
        var query = new GetPipelineDefinitionsQuery
        {
            ProductId = null,
            RepositoryId = null
        };

        // Act & Assert
        try
        {
            await _handler.Handle(query, CancellationToken.None);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "ProductId or RepositoryId");
        }
    }

    [TestMethod]
    public async Task Handle_WithProductId_CallsGetDefinitionsByProductIdAsync()
    {
        // Arrange
        var productId = 123;
        var query = new GetPipelineDefinitionsQuery
        {
            ProductId = productId,
            RepositoryId = null
        };

        var expectedDefinitions = new List<PipelineDefinitionDto>
        {
            new PipelineDefinitionDto
            {
                PipelineDefinitionId = 1,
                ProductId = productId,
                RepoId = "repo-guid",
                RepoName = "TestRepo",
                Name = "TestPipeline",
                LastSyncedUtc = DateTimeOffset.UtcNow
            }
        };

        _mockLiveProvider
            .Setup(p => p.GetDefinitionsByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDefinitions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count());
        _mockLiveProvider.Verify(p => p.GetDefinitionsByProductIdAsync(productId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithRepositoryId_CallsGetDefinitionsByRepositoryIdAsync()
    {
        // Arrange
        var repositoryId = 456;
        var query = new GetPipelineDefinitionsQuery
        {
            ProductId = null,
            RepositoryId = repositoryId
        };

        var expectedDefinitions = new List<PipelineDefinitionDto>
        {
            new PipelineDefinitionDto
            {
                PipelineDefinitionId = 2,
                RepositoryId = repositoryId,
                RepoId = "repo-guid-2",
                RepoName = "TestRepo2",
                Name = "TestPipeline2",
                LastSyncedUtc = DateTimeOffset.UtcNow
            }
        };

        _mockLiveProvider
            .Setup(p => p.GetDefinitionsByRepositoryIdAsync(repositoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDefinitions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count());
        _mockLiveProvider.Verify(p => p.GetDefinitionsByRepositoryIdAsync(repositoryId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithBothProductIdAndRepositoryId_PrefersProductId()
    {
        // Arrange
        var productId = 123;
        var repositoryId = 456;
        var query = new GetPipelineDefinitionsQuery
        {
            ProductId = productId,
            RepositoryId = repositoryId
        };

        var expectedDefinitions = new List<PipelineDefinitionDto>();

        _mockLiveProvider
            .Setup(p => p.GetDefinitionsByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDefinitions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLiveProvider.Verify(p => p.GetDefinitionsByProductIdAsync(productId, It.IsAny<CancellationToken>()), Times.Once);
        _mockLiveProvider.Verify(p => p.GetDefinitionsByRepositoryIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
