using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Settings;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetAllProfilesQueryHandlerTests
{
    private Mock<IProfileRepository> _mockRepository = null!;
    private Mock<ILogger<GetAllProfilesQueryHandler>> _mockLogger = null!;
    private GetAllProfilesQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IProfileRepository>();
        _mockLogger = new Mock<ILogger<GetAllProfilesQueryHandler>>();
        _handler = new GetAllProfilesQueryHandler(_mockRepository.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoProfiles_ReturnsEmptyList()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProfileDto>());
        var query = new GetAllProfilesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_WithMultipleProfiles_ReturnsAll()
    {
        // Arrange
        var profiles = new List<ProfileDto>
        {
            new ProfileDto(
                Id: 1,
                Name: "Profile 1",
                GoalIds: new List<int> { 1 },
                PictureType: ProfilePictureType.Default,
                DefaultPictureId: 0,
                CustomPicturePath: null,
                CreatedAt: DateTimeOffset.UtcNow,
                LastModified: DateTimeOffset.UtcNow
            ),
            new ProfileDto(
                Id: 2,
                Name: "Profile 2",
                GoalIds: new List<int> { 2, 3 },
                PictureType: ProfilePictureType.Default,
                DefaultPictureId: 0,
                CustomPicturePath: null,
                CreatedAt: DateTimeOffset.UtcNow,
                LastModified: DateTimeOffset.UtcNow
            ),
            new ProfileDto(
                Id: 3,
                Name: "Profile 3",
                GoalIds: new List<int>(),
                PictureType: ProfilePictureType.Default,
                DefaultPictureId: 0,
                CustomPicturePath: null,
                CreatedAt: DateTimeOffset.UtcNow,
                LastModified: DateTimeOffset.UtcNow
            )
        };

        _mockRepository.Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(profiles);
        var query = new GetAllProfilesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var profileList = result.ToList();
        Assert.HasCount(3, profileList);
        Assert.AreEqual("Profile 1", profileList[0].Name);
        Assert.AreEqual("Profile 2", profileList[1].Name);
        Assert.AreEqual("Profile 3", profileList[2].Name);
    }

    [TestMethod]
    public async Task Handle_ReturnsProfilesWithAllProperties()
    {
        // Arrange
        var profiles = new List<ProfileDto>
        {
            new ProfileDto(
                Id: 100,
                Name: "Complete Profile",
                GoalIds: new List<int> { 10, 20, 30 },
                PictureType: ProfilePictureType.Default,
                DefaultPictureId: 0,
                CustomPicturePath: null,
                CreatedAt: DateTimeOffset.UtcNow,
                LastModified: DateTimeOffset.UtcNow
            )
        };

        _mockRepository.Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(profiles);
        var query = new GetAllProfilesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var profile = result.First();
        Assert.AreEqual(100, profile.Id);
        Assert.AreEqual("Complete Profile", profile.Name);
        Assert.HasCount(3, profile.GoalIds);
    }
}
