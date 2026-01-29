using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class PipelineServiceTests
{
    private Mock<IPipelinesClient> _mockClient = null!;
    private PipelineService _service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _mockClient = new Mock<IPipelinesClient>();
        _service = new PipelineService(_mockClient.Object);
    }

    [TestMethod]
    public async Task GetRunsForProductsAsync_WithNullProductIds_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetRunsForProductsAsync(null);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
        
        // Verify GetDefinitionsAsync was not called (which would throw if both params are null)
        _mockClient.Verify(c => c.GetDefinitionsAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [TestMethod]
    public async Task GetRunsForProductsAsync_WithEmptyProductIds_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetRunsForProductsAsync("");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
        
        // Verify GetDefinitionsAsync was not called
        _mockClient.Verify(c => c.GetDefinitionsAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [TestMethod]
    public async Task GetRunsForProductsAsync_WithWhitespaceProductIds_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetRunsForProductsAsync("   ");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
        
        // Verify GetDefinitionsAsync was not called
        _mockClient.Verify(c => c.GetDefinitionsAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [TestMethod]
    public async Task GetRunsForProductsAsync_WithInvalidProductId_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetRunsForProductsAsync("invalid");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
        
        // Verify GetDefinitionsAsync was not called (would throw if called with null)
        _mockClient.Verify(c => c.GetDefinitionsAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [TestMethod]
    public async Task GetRunsForProductsAsync_WithNegativeProductId_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetRunsForProductsAsync("-123");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
        
        // Verify GetDefinitionsAsync was not called
        _mockClient.Verify(c => c.GetDefinitionsAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [TestMethod]
    public async Task GetRunsForProductsAsync_WithZeroProductId_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetRunsForProductsAsync("0");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
        
        // Verify GetDefinitionsAsync was not called
        _mockClient.Verify(c => c.GetDefinitionsAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [TestMethod]
    public async Task GetRunsForProductsAsync_WithCommasOnly_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetRunsForProductsAsync(",,,");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
        
        // Verify GetDefinitionsAsync was not called
        _mockClient.Verify(c => c.GetDefinitionsAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [TestMethod]
    public async Task GetRunsForProductsAsync_WithValidProductId_CallsGetDefinitionsAsync()
    {
        // Arrange
        var productId = 123;
        var productIdsStr = productId.ToString();
        
        var mockDefinitions = new List<PipelineDefinitionDto>
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

        _mockClient
            .Setup(c => c.GetDefinitionsAsync(productId, null))
            .ReturnsAsync((ICollection<PipelineDefinitionDto>)mockDefinitions);

        _mockClient
            .Setup(c => c.GetRunsAsync(1, 100))
            .ReturnsAsync((ICollection<PipelineRunDto>)new List<PipelineRunDto>
            {
                new PipelineRunDto
                {
                    PipelineId = 1,
                    PipelineName = "TestPipeline",
                    RunId = 101,
                    Result = PipelineRunResult.Succeeded,
                    StartTime = DateTimeOffset.UtcNow.AddHours(-1),
                    FinishTime = DateTimeOffset.UtcNow,
                    RetrievedAt = DateTimeOffset.UtcNow
                }
            });

        // Act
        var result = await _service.GetRunsForProductsAsync(productIdsStr);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count());
        
        // Verify GetDefinitionsAsync was called with correct productId
        _mockClient.Verify(c => c.GetDefinitionsAsync(productId, null), Times.Once);
        _mockClient.Verify(c => c.GetRunsAsync(1, 100), Times.Once);
    }
}
