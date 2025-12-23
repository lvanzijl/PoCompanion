using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Settings;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;
using PoTool.Core.Settings.Commands;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class CreateProfileCommandHandlerTests
{
    private Mock<IProfileRepository> _mockRepository = null!;
    private Mock<ILogger<CreateProfileCommandHandler>> _mockLogger = null!;
    private CreateProfileCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IProfileRepository>();
        _mockLogger = new Mock<ILogger<CreateProfileCommandHandler>>();
        _handler = new CreateProfileCommandHandler(_mockRepository.Object);
    }

    [TestMethod]
    public async Task Handle_WithValidProfile_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreateProfileCommand(
            Name: "Test Profile",
            AreaPaths: new List<string> { "Project\\ProductA", "Project\\ProductB" },
            TeamName: "Team Alpha",
            GoalIds: new List<int> { 1, 2, 3 }
        );

        var createdProfile = new ProfileDto(
            Id: 1,
            Name: command.Name,
            AreaPaths: command.AreaPaths,
            TeamName: command.TeamName,
            GoalIds: command.GoalIds,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow
        );

        _mockRepository.Setup(r => r.CreateProfileAsync(
            It.IsAny<string>(),
            It.IsAny<List<string>>(),
            It.IsAny<string>(),
            It.IsAny<List<int>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdProfile);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Id);
        Assert.AreEqual("Test Profile", result.Name);
        Assert.AreEqual(2, result.AreaPaths.Count);
        Assert.AreEqual("Team Alpha", result.TeamName);
        Assert.AreEqual(3, result.GoalIds.Count);

        _mockRepository.Verify(r => r.CreateProfileAsync(
            "Test Profile",
            It.Is<List<string>>(list => list.Count == 2),
            "Team Alpha",
            It.Is<List<int>>(list => list.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithEmptyAreaPaths_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreateProfileCommand(
            Name: "Minimal Profile",
            AreaPaths: new List<string>(),
            TeamName: "Team Beta",
            GoalIds: new List<int>()
        );

        var createdProfile = new ProfileDto(
            Id: 2,
            Name: command.Name,
            AreaPaths: command.AreaPaths,
            TeamName: command.TeamName,
            GoalIds: command.GoalIds,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow
        );

        _mockRepository.Setup(r => r.CreateProfileAsync(
            It.IsAny<string>(),
            It.IsAny<List<string>>(),
            It.IsAny<string>(),
            It.IsAny<List<int>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdProfile);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Minimal Profile", result.Name);
        Assert.AreEqual(0, result.AreaPaths.Count);
        Assert.AreEqual(0, result.GoalIds.Count);
    }

    [TestMethod]
    public async Task Handle_WithMultipleAreaPaths_PreservesOrder()
    {
        // Arrange
        var areaPaths = new List<string> 
        { 
            "Project\\ProductA",
            "Project\\ProductA\\Mobile",
            "Project\\ProductB"
        };
        
        var command = new CreateProfileCommand(
            Name: "Multi-Path Profile",
            AreaPaths: areaPaths,
            TeamName: "Team Gamma",
            GoalIds: new List<int> { 10, 20 }
        );

        var createdProfile = new ProfileDto(
            Id: 3,
            Name: command.Name,
            AreaPaths: command.AreaPaths,
            TeamName: command.TeamName,
            GoalIds: command.GoalIds,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow
        );

        _mockRepository.Setup(r => r.CreateProfileAsync(
            It.IsAny<string>(),
            It.IsAny<List<string>>(),
            It.IsAny<string>(),
            It.IsAny<List<int>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdProfile);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.AreaPaths.Count);
        Assert.AreEqual("Project\\ProductA", result.AreaPaths[0]);
        Assert.AreEqual("Project\\ProductA\\Mobile", result.AreaPaths[1]);
        Assert.AreEqual("Project\\ProductB", result.AreaPaths[2]);
    }
}
