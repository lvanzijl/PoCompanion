using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Pipelines;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetPipelineMetricsQueryHandlerTests
{
    private Mock<IPipelineReadProvider> _mockProvider = null!;
    private GetPipelineMetricsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockProvider = new Mock<IPipelineReadProvider>();
        _handler = new GetPipelineMetricsQueryHandler(_mockProvider.Object);
    }

    [TestMethod]
    public async Task Handle_WithProductIds_UsesProductFilteringAndBranchFilter()
    {
        // Arrange
        var productIds = new List<int> { 1, 2 };
        var query = new GetPipelineMetricsQuery(productIds);

        var pipelineDefinitions = new List<PipelineDefinitionDto>
        {
            new() { PipelineDefinitionId = 101, ProductId = 1, RepositoryId = 10, RepoId = "repo1", RepoName = "TestRepo1", Name = "Pipeline1", LastSyncedUtc = DateTimeOffset.UtcNow },
            new() { PipelineDefinitionId = 102, ProductId = 2, RepositoryId = 20, RepoId = "repo2", RepoName = "TestRepo2", Name = "Pipeline2", LastSyncedUtc = DateTimeOffset.UtcNow }
        };

        var pipelines = new List<PipelineDto>
        {
            new(101, "Pipeline1", PipelineType.Build, null, DateTimeOffset.UtcNow),
            new(102, "Pipeline2", PipelineType.Build, null, DateTimeOffset.UtcNow)
        };

        var runs = new List<PipelineRunDto>
        {
            new(1, 101, "Pipeline1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-1).AddHours(1),
                TimeSpan.FromHours(1), PipelineRunResult.Succeeded, PipelineRunTrigger.ContinuousIntegration, 
                null, "refs/heads/main", "user1", DateTimeOffset.UtcNow),
            new(2, 102, "Pipeline2", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-2).AddHours(1),
                TimeSpan.FromHours(1), PipelineRunResult.Failed, PipelineRunTrigger.ContinuousIntegration,
                null, "refs/heads/main", "user2", DateTimeOffset.UtcNow)
        };

        // Setup mocks
        _mockProvider.Setup(p => p.GetDefinitionsByProductIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pipelineDefinitions[0] });
        _mockProvider.Setup(p => p.GetDefinitionsByProductIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pipelineDefinitions[1] });

        _mockProvider.Setup(p => p.GetRunsForPipelinesAsync(
            It.Is<IEnumerable<int>>(ids => ids.Contains(101) && ids.Contains(102)),
            "refs/heads/main",
            It.IsAny<DateTimeOffset>(),
            100,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        _mockProvider.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.ToList();
        Assert.HasCount(2, metrics);
        
        // Verify product filtering was called
        _mockProvider.Verify(p => p.GetDefinitionsByProductIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _mockProvider.Verify(p => p.GetDefinitionsByProductIdAsync(2, It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify efficient runs fetching with filters was called
        _mockProvider.Verify(p => p.GetRunsForPipelinesAsync(
            It.IsAny<IEnumerable<int>>(),
            "refs/heads/main",
            It.IsAny<DateTimeOffset>(),
            100,
            It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify we did NOT call the old inefficient GetAllRunsAsync
        _mockProvider.Verify(p => p.GetAllRunsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_WithoutProductIds_FetchesAllPipelines()
    {
        // Arrange
        var query = new GetPipelineMetricsQuery(null);

        var pipelines = new List<PipelineDto>
        {
            new(101, "Pipeline1", PipelineType.Build, null, DateTimeOffset.UtcNow)
        };

        var runs = new List<PipelineRunDto>
        {
            new(1, 101, "Pipeline1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-1).AddHours(1),
                TimeSpan.FromHours(1), PipelineRunResult.Succeeded, PipelineRunTrigger.ContinuousIntegration,
                null, "refs/heads/main", "user1", DateTimeOffset.UtcNow)
        };

        // Setup mocks
        _mockProvider.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        _mockProvider.Setup(p => p.GetRunsForPipelinesAsync(
            It.Is<IEnumerable<int>>(ids => ids.Contains(101)),
            "refs/heads/main",
            It.IsAny<DateTimeOffset>(),
            100,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.ToList();
        Assert.HasCount(1, metrics);
        
        // Verify efficient runs fetching with filters was used
        _mockProvider.Verify(p => p.GetRunsForPipelinesAsync(
            It.IsAny<IEnumerable<int>>(),
            "refs/heads/main",
            It.IsAny<DateTimeOffset>(),
            100,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithProductIdsButNoPipelines_ReturnsEmpty()
    {
        // Arrange
        var productIds = new List<int> { 999 };
        var query = new GetPipelineMetricsQuery(productIds);

        // Setup mocks - no definitions for this product
        _mockProvider.Setup(p => p.GetDefinitionsByProductIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PipelineDefinitionDto>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.ToList();
        Assert.IsEmpty(metrics);
        
        // Verify we did not fetch runs since there are no pipelines
        _mockProvider.Verify(p => p.GetRunsForPipelinesAsync(
            It.IsAny<IEnumerable<int>>(),
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
