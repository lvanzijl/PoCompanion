using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetAreaPathsFromTfsQueryHandlerTests
{
    private Mock<ITfsClient> _mockTfsClient = null!;
    private Mock<ILogger<GetAreaPathsFromTfsQueryHandler>> _mockLogger = null!;
    private GetAreaPathsFromTfsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTfsClient = new Mock<ITfsClient>();
        _mockLogger = new Mock<ILogger<GetAreaPathsFromTfsQueryHandler>>();
        _handler = new GetAreaPathsFromTfsQueryHandler(_mockTfsClient.Object, _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoAreaPaths_ReturnsEmptyList()
    {
        // Arrange
        _mockTfsClient.Setup(c => c.GetAreaPathsAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        var query = new GetAreaPathsFromTfsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_WithMultipleAreaPaths_ReturnsAll()
    {
        // Arrange
        var areaPaths = new List<string>
        {
            "Project\\Area1",
            "Project\\Area2",
            "Project\\Area3"
        };
        _mockTfsClient.Setup(c => c.GetAreaPathsAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(areaPaths);
        var query = new GetAreaPathsFromTfsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var areaPathList = result.ToList();
        Assert.HasCount(3, areaPathList);
        Assert.AreEqual("Project\\Area1", areaPathList[0]);
        Assert.AreEqual("Project\\Area2", areaPathList[1]);
        Assert.AreEqual("Project\\Area3", areaPathList[2]);
    }

    [TestMethod]
    public async Task Handle_WithHierarchicalAreaPaths_ReturnsAllLevels()
    {
        // Arrange
        var areaPaths = new List<string>
        {
            "Project",
            "Project\\TeamA",
            "Project\\TeamA\\Product1",
            "Project\\TeamB",
            "Project\\TeamB\\Product2"
        };
        _mockTfsClient.Setup(c => c.GetAreaPathsAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(areaPaths);
        var query = new GetAreaPathsFromTfsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var areaPathList = result.ToList();
        Assert.HasCount(5, areaPathList);
        Assert.Contains("Project", areaPathList);
        Assert.Contains("Project\\TeamA", areaPathList);
        Assert.Contains("Project\\TeamA\\Product1", areaPathList);
    }

    [TestMethod]
    public async Task Handle_WhenTfsClientThrows_ReturnsEmptyList()
    {
        // Arrange
        _mockTfsClient.Setup(c => c.GetAreaPathsAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("TFS connection error"));
        var query = new GetAreaPathsFromTfsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - should return empty list instead of throwing
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_CallsTfsClientWithNullDepth()
    {
        // Arrange
        var areaPaths = new List<string> { "Project\\Area1" };
        _mockTfsClient.Setup(c => c.GetAreaPathsAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(areaPaths);
        var query = new GetAreaPathsFromTfsQuery();

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - verify depth parameter is null (get all levels)
        _mockTfsClient.Verify(
            c => c.GetAreaPathsAsync(null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
