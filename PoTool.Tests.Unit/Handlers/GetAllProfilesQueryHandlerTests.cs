using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Settings;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;
using PoTool.Core.Settings.Queries;

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
                AreaPaths: new List<string> { "Project\\A" },
                TeamName: "Team Alpha",
                GoalIds: new List<int> { 1 },
                CreatedAt: DateTimeOffset.UtcNow,
                LastModified: DateTimeOffset.UtcNow
            ),
            new ProfileDto(
                Id: 2,
                Name: "Profile 2",
                AreaPaths: new List<string> { "Project\\B" },
                TeamName: "Team Beta",
                GoalIds: new List<int> { 2, 3 },
                CreatedAt: DateTimeOffset.UtcNow,
                LastModified: DateTimeOffset.UtcNow
            ),
            new ProfileDto(
                Id: 3,
                Name: "Profile 3",
                AreaPaths: new List<string> { "Project\\C", "Project\\D" },
                TeamName: "Team Gamma",
                GoalIds: new List<int>(),
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
        Assert.AreEqual(3, profileList.Count);
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
                AreaPaths: new List<string> { "Project\\ProductA", "Project\\ProductB" },
                TeamName: "Complete Team",
                GoalIds: new List<int> { 10, 20, 30 },
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
        Assert.AreEqual(2, profile.AreaPaths.Count);
        Assert.AreEqual("Project\\ProductA", profile.AreaPaths[0]);
        Assert.AreEqual("Complete Team", profile.TeamName);
        Assert.AreEqual(3, profile.GoalIds.Count);
    }
}
