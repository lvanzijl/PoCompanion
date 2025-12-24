using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class ProfileFilterServiceTests
{
    private Mock<ISettingsRepository> _mockSettingsRepository = null!;
    private Mock<IProfileRepository> _mockProfileRepository = null!;
    private Mock<ILogger<ProfileFilterService>> _mockLogger = null!;
    private ProfileFilterService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockSettingsRepository = new Mock<ISettingsRepository>();
        _mockProfileRepository = new Mock<IProfileRepository>();
        _mockLogger = new Mock<ILogger<ProfileFilterService>>();
        _service = new ProfileFilterService(
            _mockSettingsRepository.Object,
            _mockProfileRepository.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task GetActiveProfileAreaPathsAsync_WithNoActiveProfile_ReturnsNull()
    {
        // Arrange
        var settings = new SettingsDto(
            Id: 1,
            DataMode: DataMode.Tfs,
            ConfiguredGoalIds: new List<int>(),
            ActiveProfileId: null,
            LastModified: DateTimeOffset.UtcNow
        );

        _mockSettingsRepository.Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.GetActiveProfileAreaPathsAsync();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetActiveProfileAreaPathsAsync_WithActiveProfile_ReturnsAreaPaths()
    {
        // Arrange
        var settings = new SettingsDto(
            Id: 1,
            DataMode: DataMode.Tfs,
            ConfiguredGoalIds: new List<int>(),
            ActiveProfileId: 10,
            LastModified: DateTimeOffset.UtcNow
        );

        var profile = new ProfileDto(
            Id: 10,
            Name: "Test Profile",
            AreaPaths: new List<string> { "Project\\ProductA", "Project\\ProductB" },
            TeamName: "Team Alpha",
            GoalIds: new List<int>(),
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow
        );

        _mockSettingsRepository.Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockProfileRepository.Setup(r => r.GetProfileByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _service.GetActiveProfileAreaPathsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result);
        Assert.AreEqual("Project\\ProductA", result[0]);
        Assert.AreEqual("Project\\ProductB", result[1]);
    }

    [TestMethod]
    public async Task GetActiveProfileAreaPathsAsync_WithProfileNotFound_ReturnsNull()
    {
        // Arrange
        var settings = new SettingsDto(
            Id: 1,
            DataMode: DataMode.Tfs,
            ConfiguredGoalIds: new List<int>(),
            ActiveProfileId: 999,
            LastModified: DateTimeOffset.UtcNow
        );

        _mockSettingsRepository.Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockProfileRepository.Setup(r => r.GetProfileByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileDto?)null);

        // Act
        var result = await _service.GetActiveProfileAreaPathsAsync();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetActiveProfileAreaPathsAsync_WithEmptyAreaPaths_ReturnsNull()
    {
        // Arrange
        var settings = new SettingsDto(
            Id: 1,
            DataMode: DataMode.Tfs,
            ConfiguredGoalIds: new List<int>(),
            ActiveProfileId: 10,
            LastModified: DateTimeOffset.UtcNow
        );

        var profile = new ProfileDto(
            Id: 10,
            Name: "Empty Profile",
            AreaPaths: new List<string>(),
            TeamName: "Team Alpha",
            GoalIds: new List<int>(),
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow
        );

        _mockSettingsRepository.Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _mockProfileRepository.Setup(r => r.GetProfileByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _service.GetActiveProfileAreaPathsAsync();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void MatchesAreaPathFilter_WithNullFilter_ReturnsTrue()
    {
        // Arrange
        var workItemAreaPath = "Project\\ProductA\\Feature1";

        // Act
        var result = _service.MatchesAreaPathFilter(workItemAreaPath, null);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void MatchesAreaPathFilter_WithEmptyFilter_ReturnsTrue()
    {
        // Arrange
        var workItemAreaPath = "Project\\ProductA\\Feature1";
        var filter = new List<string>();

        // Act
        var result = _service.MatchesAreaPathFilter(workItemAreaPath, filter);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void MatchesAreaPathFilter_WithExactMatch_ReturnsTrue()
    {
        // Arrange
        var workItemAreaPath = "Project\\ProductA";
        var filter = new List<string> { "Project\\ProductA" };

        // Act
        var result = _service.MatchesAreaPathFilter(workItemAreaPath, filter);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void MatchesAreaPathFilter_WithHierarchicalMatch_ReturnsTrue()
    {
        // Arrange
        var workItemAreaPath = "Project\\ProductA\\Feature1\\Component2";
        var filter = new List<string> { "Project\\ProductA" };

        // Act
        var result = _service.MatchesAreaPathFilter(workItemAreaPath, filter);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void MatchesAreaPathFilter_WithNoMatch_ReturnsFalse()
    {
        // Arrange
        var workItemAreaPath = "Project\\ProductB\\Feature1";
        var filter = new List<string> { "Project\\ProductA" };

        // Act
        var result = _service.MatchesAreaPathFilter(workItemAreaPath, filter);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void MatchesAreaPathFilter_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var workItemAreaPath = "PROJECT\\PRODUCTA\\Feature1";
        var filter = new List<string> { "project\\producta" };

        // Act
        var result = _service.MatchesAreaPathFilter(workItemAreaPath, filter);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void MatchesAreaPathFilter_WithMultipleFilters_MatchesAny()
    {
        // Arrange
        var workItemAreaPath = "Project\\ProductB\\Feature1";
        var filter = new List<string> 
        { 
            "Project\\ProductA",
            "Project\\ProductB",
            "Project\\ProductC"
        };

        // Act
        var result = _service.MatchesAreaPathFilter(workItemAreaPath, filter);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void MatchesAreaPathFilter_ParentChildRelationship_WorksCorrectly()
    {
        // Arrange
        var filter = new List<string> { "MyProject\\Product" };

        // Act & Assert
        Assert.IsTrue(_service.MatchesAreaPathFilter("MyProject\\Product", filter));
        Assert.IsTrue(_service.MatchesAreaPathFilter("MyProject\\Product\\Feature", filter));
        Assert.IsTrue(_service.MatchesAreaPathFilter("MyProject\\Product\\Feature\\SubFeature", filter));
        Assert.IsFalse(_service.MatchesAreaPathFilter("MyProject\\ProductOther", filter));
        Assert.IsFalse(_service.MatchesAreaPathFilter("MyProject", filter));
        Assert.IsFalse(_service.MatchesAreaPathFilter("OtherProject\\Product", filter));
    }
}
