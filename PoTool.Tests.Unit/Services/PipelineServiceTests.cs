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
    public async Task GetRunsForProductsAsync_WithValidProductId_CallsEnvelopeEndpoint()
    {
        // Arrange
        var productId = 123;
        var productIdsStr = productId.ToString();

        var response = new PoTool.Shared.Pipelines.PipelineQueryResponseDto<IReadOnlyList<PipelineRunDto>>
        {
            Data = new List<PipelineRunDto>
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
            },
            RequestedFilter = new PoTool.Shared.Pipelines.PipelineFilterContextDto
            {
                ProductIds = new PoTool.Shared.Metrics.FilterSelectionDto<int> { IsAll = false, Values = new[] { productId } },
                TeamIds = new PoTool.Shared.Metrics.FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
                RepositoryNames = new PoTool.Shared.Metrics.FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
                Time = new PoTool.Shared.Metrics.FilterTimeSelectionDto
                {
                    Mode = PoTool.Shared.Metrics.FilterTimeSelectionModeDto.None,
                    SprintIds = Array.Empty<int>()
                }
            },
            EffectiveFilter = new PoTool.Shared.Pipelines.PipelineFilterContextDto
            {
                ProductIds = new PoTool.Shared.Metrics.FilterSelectionDto<int> { IsAll = false, Values = new[] { productId } },
                TeamIds = new PoTool.Shared.Metrics.FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
                RepositoryNames = new PoTool.Shared.Metrics.FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
                Time = new PoTool.Shared.Metrics.FilterTimeSelectionDto
                {
                    Mode = PoTool.Shared.Metrics.FilterTimeSelectionModeDto.None,
                    SprintIds = Array.Empty<int>()
                }
            },
            InvalidFields = Array.Empty<string>(),
            ValidationMessages = Array.Empty<PoTool.Shared.Metrics.FilterValidationIssueDto>()
        };

        _mockClient
            .Setup(c => c.GetRunsForProductsEnvelopeAsync(productIdsStr, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _service.GetRunsForProductsAsync(productIdsStr);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count());

        _mockClient.Verify(c => c.GetRunsForProductsEnvelopeAsync(productIdsStr, null, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockClient.Verify(c => c.GetDefinitionsAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
        _mockClient.Verify(c => c.GetRunsAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
    }
}
