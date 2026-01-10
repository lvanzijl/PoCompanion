using PoTool.Api.Services;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Unit tests for WorkItemClassificationService.
/// </summary>
[TestClass]
public class WorkItemClassificationServiceTests
{
    private WorkItemClassificationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new WorkItemClassificationService();
    }

    [TestMethod]
    public void ClassifyWorkItem_ExactMatch_ReturnsMatchingTeam()
    {
        // Arrange
        var teams = new List<TeamDto>
        {
            CreateTeam(1, "Team A", @"\Project\AreaA"),
            CreateTeam(2, "Team B", @"\Project\AreaB")
        };

        // Act
        var result = _service.ClassifyWorkItem(@"\Project\AreaA", teams);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Id);
        Assert.AreEqual("Team A", result.Name);
    }

    [TestMethod]
    public void ClassifyWorkItem_ParentMatch_ReturnsMatchingTeam()
    {
        // Arrange
        var teams = new List<TeamDto>
        {
            CreateTeam(1, "Team A", @"\Project\AreaA"),
            CreateTeam(2, "Team B", @"\Project\AreaB")
        };

        // Act
        var result = _service.ClassifyWorkItem(@"\Project\AreaA\SubArea\Deeper", teams);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Id);
    }

    [TestMethod]
    public void ClassifyWorkItem_MostSpecificMatchWins()
    {
        // Arrange
        var teams = new List<TeamDto>
        {
            CreateTeam(1, "Team General", @"\Project\Area"),
            CreateTeam(2, "Team Specific", @"\Project\Area\SubArea")
        };

        // Act
        var result = _service.ClassifyWorkItem(@"\Project\Area\SubArea\Deeper", teams);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Id);
        Assert.AreEqual("Team Specific", result.Name);
    }

    [TestMethod]
    public void ClassifyWorkItem_NoMatch_ReturnsNull()
    {
        // Arrange
        var teams = new List<TeamDto>
        {
            CreateTeam(1, "Team A", @"\Project\AreaA"),
            CreateTeam(2, "Team B", @"\Project\AreaB")
        };

        // Act
        var result = _service.ClassifyWorkItem(@"\Project\AreaC", teams);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ClassifyWorkItem_EmptyTeams_ReturnsNull()
    {
        // Arrange
        var teams = new List<TeamDto>();

        // Act
        var result = _service.ClassifyWorkItem(@"\Project\AreaA", teams);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ClassifyWorkItem_EmptyWorkItemPath_ReturnsNull()
    {
        // Arrange
        var teams = new List<TeamDto>
        {
            CreateTeam(1, "Team A", @"\Project\AreaA")
        };

        // Act
        var result = _service.ClassifyWorkItem(string.Empty, teams);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ClassifyWorkItem_CaseInsensitiveMatch()
    {
        // Arrange
        var teams = new List<TeamDto>
        {
            CreateTeam(1, "Team A", @"\Project\AREAA")
        };

        // Act
        var result = _service.ClassifyWorkItem(@"\project\areaa\subarea", teams);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Id);
    }

    [TestMethod]
    public void ClassifyWorkItem_TrailingBackslash_StillMatches()
    {
        // Arrange
        var teams = new List<TeamDto>
        {
            CreateTeam(1, "Team A", @"\Project\AreaA\")
        };

        // Act
        var result = _service.ClassifyWorkItem(@"\Project\AreaA", teams);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Id);
    }

    [TestMethod]
    public void ClassifyWorkItem_PartialNameMatch_DoesNotMatch()
    {
        // Arrange
        var teams = new List<TeamDto>
        {
            CreateTeam(1, "Team A", @"\Project\Area")
        };

        // Act - "AreaB" starts with "Area" but is not under "Area\"
        var result = _service.ClassifyWorkItem(@"\Project\AreaB", teams);

        // Assert
        Assert.IsNull(result);
    }

    private static TeamDto CreateTeam(int id, string name, string teamAreaPath)
    {
        return new TeamDto(
            id,
            name,
            teamAreaPath,
            false,
            TeamPictureType.Default,
            0,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }
}
