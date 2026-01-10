using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Settings;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;

using PoTool.Core.WorkItems;

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
            GoalIds: new List<int> { 1, 2, 3 }
        );

        var createdProfile = new ProfileDto(
            Id: 1,
            Name: command.Name,
            GoalIds: command.GoalIds,
            PictureType: ProfilePictureType.Default,
            DefaultPictureId: 0,
            CustomPicturePath: null,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow
        );

        _mockRepository.Setup(r => r.CreateProfileAsync(
            It.IsAny<string>(),
            It.IsAny<List<int>>(),
            It.IsAny<ProfilePictureType>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdProfile);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Id);
        Assert.AreEqual("Test Profile", result.Name);
        Assert.HasCount(3, result.GoalIds);

        _mockRepository.Verify(r => r.CreateProfileAsync(
            "Test Profile",
            It.Is<List<int>>(list => list.Count == 3),
            It.IsAny<ProfilePictureType>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithEmptyAreaPaths_CreatesSuccessfully()
    {
        // Arrange
        var command = new CreateProfileCommand(
            Name: "Minimal Profile",
            GoalIds: new List<int>()
        );

        var createdProfile = new ProfileDto(
            Id: 2,
            Name: command.Name,
            GoalIds: command.GoalIds,
            PictureType: ProfilePictureType.Default,
            DefaultPictureId: 0,
            CustomPicturePath: null,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow
        );

        _mockRepository.Setup(r => r.CreateProfileAsync(
            It.IsAny<string>(),
            It.IsAny<List<int>>(),
            It.IsAny<ProfilePictureType>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdProfile);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Minimal Profile", result.Name);
        Assert.HasCount(0, result.GoalIds);
    }

    [TestMethod]
    public async Task Handle_WithMultipleGoals_PreservesOrder()
    {
        // Arrange
        var goalIds = new List<int> { 10, 20, 30 };

        var command = new CreateProfileCommand(
            Name: "Multi-Goal Profile",
            GoalIds: goalIds
        );

        var createdProfile = new ProfileDto(
            Id: 3,
            Name: command.Name,
            GoalIds: goalIds,
            PictureType: ProfilePictureType.Default,
            DefaultPictureId: 0,
            CustomPicturePath: null,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow
        );

        _mockRepository.Setup(r => r.CreateProfileAsync(
            It.IsAny<string>(),
            It.IsAny<List<int>>(),
            It.IsAny<ProfilePictureType>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdProfile);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(3, result.GoalIds);
        Assert.AreEqual(10, result.GoalIds[0]);
        Assert.AreEqual(20, result.GoalIds[1]);
        Assert.AreEqual(30, result.GoalIds[2]);
    }
}
