using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Client.Models;
using System.Text.Json;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class TfsFieldParserServiceTests
{
    private TfsFieldParserService _service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _service = new TfsFieldParserService();
    }

    private WorkItemWithValidationDto CreateBugWithJsonPayload(Dictionary<string, object> fields)
    {
        var jsonPayload = JsonSerializer.Serialize(fields);
        return new WorkItemWithValidationDto
        {
            TfsId = 1,
            Type = "Bug",
            Title = "Test Bug",
            ParentTfsId = null,
            AreaPath = "Project\\Team",
            IterationPath = "Project\\Sprint 1",
            State = "Active",
            JsonPayload = jsonPayload,
            RetrievedAt = DateTimeOffset.UtcNow,
            Effort = null,
            Description = "Test Description",
            CreatedDate = DateTimeOffset.UtcNow,
            ClosedDate = null,
            ValidationIssues = new List<ValidationIssue>()
        };
    }

    #region GetPriority Tests

    [TestMethod]
    public void GetPriority_WithValidPriority_ReturnsPriorityValue()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "Microsoft.VSTS.Common.Priority", 1 }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetPriority(bug);

        // Assert
        Assert.AreEqual("1", result);
    }

    [TestMethod]
    public void GetPriority_WithSystemPriority_ReturnsPriorityValue()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "System.Priority", 2 }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetPriority(bug);

        // Assert
        Assert.AreEqual("2", result);
    }

    [TestMethod]
    public void GetPriority_WithMissingField_ReturnsNull()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "System.Title", "Test Bug" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetPriority(bug);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetPriority_WithEmptyJsonPayload_ReturnsNull()
    {
        // Arrange
        var bug = new WorkItemWithValidationDto
        {
            TfsId = 1,
            Type = "Bug",
            Title = "Test Bug",
            ParentTfsId = null,
            AreaPath = "Project\\Team",
            IterationPath = "Project\\Sprint 1",
            State = "Active",
            JsonPayload = "",
            RetrievedAt = DateTimeOffset.UtcNow,
            Effort = null,
            Description = null,
            CreatedDate = null,
            ClosedDate = null,
            ValidationIssues = new List<ValidationIssue>()
        };

        // Act
        var result = _service.GetPriority(bug);

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region GetSeverity Tests

    [TestMethod]
    public void GetSeverity_WithValidSeverity_ReturnsSeverityValue()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "Microsoft.VSTS.Common.Severity", "1 - Critical" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetSeverity(bug);

        // Assert
        Assert.AreEqual("1 - Critical", result);
    }

    [TestMethod]
    public void GetSeverity_WithMissingField_ReturnsNull()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "System.Title", "Test Bug" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetSeverity(bug);

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region GetTags Tests

    [TestMethod]
    public void GetTags_WithValidTags_ReturnsTagList()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "System.Tags", "Tag1; Tag2; Tag3" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetTags(bug);

        // Assert
        Assert.HasCount(3, result);
        Assert.Contains("Tag1", result);
        Assert.Contains("Tag2", result);
        Assert.Contains("Tag3", result);
    }

    [TestMethod]
    public void GetTags_WithExtraWhitespace_TrimsWhitespace()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "System.Tags", "  Tag1  ;  Tag2  ;  Tag3  " }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetTags(bug);

        // Assert
        Assert.HasCount(3, result);
        Assert.Contains("Tag1", result);
        Assert.Contains("Tag2", result);
        Assert.Contains("Tag3", result);
    }

    [TestMethod]
    public void GetTags_WithEmptyTags_ReturnsEmptyList()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "System.Tags", "" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetTags(bug);

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void GetTags_WithMissingField_ReturnsEmptyList()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "System.Title", "Test Bug" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetTags(bug);

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void GetTags_WithSingleTag_ReturnsSingleItemList()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "System.Tags", "SingleTag" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetTags(bug);

        // Assert
        Assert.HasCount(1, result);
        Assert.AreEqual("SingleTag", result[0]);
    }

    #endregion

    #region MapPriorityToCriticality Tests

    [TestMethod]
    public void MapPriorityToCriticality_Priority1_ReturnsCritical()
    {
        // Act
        var result = _service.MapPriorityToCriticality("1");

        // Assert
        Assert.AreEqual(BugCriticality.Critical, result);
    }

    [TestMethod]
    public void MapPriorityToCriticality_Priority2_ReturnsHigh()
    {
        // Act
        var result = _service.MapPriorityToCriticality("2");

        // Assert
        Assert.AreEqual(BugCriticality.High, result);
    }

    [TestMethod]
    public void MapPriorityToCriticality_Priority3_ReturnsMedium()
    {
        // Act
        var result = _service.MapPriorityToCriticality("3");

        // Assert
        Assert.AreEqual(BugCriticality.Medium, result);
    }

    [TestMethod]
    public void MapPriorityToCriticality_Priority4_ReturnsLow()
    {
        // Act
        var result = _service.MapPriorityToCriticality("4");

        // Assert
        Assert.AreEqual(BugCriticality.Low, result);
    }

    [TestMethod]
    public void MapPriorityToCriticality_NullPriority_ReturnsMedium()
    {
        // Act
        var result = _service.MapPriorityToCriticality(null);

        // Assert
        Assert.AreEqual(BugCriticality.Medium, result);
    }

    [TestMethod]
    public void MapPriorityToCriticality_UnknownPriority_ReturnsMedium()
    {
        // Act
        var result = _service.MapPriorityToCriticality("5");

        // Assert
        Assert.AreEqual(BugCriticality.Medium, result);
    }

    #endregion

    #region MapSeverityToCriticality Tests

    [TestMethod]
    public void MapSeverityToCriticality_CriticalWithNumber_ReturnsCritical()
    {
        // Act
        var result = _service.MapSeverityToCriticality("1 - Critical");

        // Assert
        Assert.AreEqual(BugCriticality.Critical, result);
    }

    [TestMethod]
    public void MapSeverityToCriticality_CriticalPlain_ReturnsCritical()
    {
        // Act
        var result = _service.MapSeverityToCriticality("Critical");

        // Assert
        Assert.AreEqual(BugCriticality.Critical, result);
    }

    [TestMethod]
    public void MapSeverityToCriticality_HighWithNumber_ReturnsHigh()
    {
        // Act
        var result = _service.MapSeverityToCriticality("2 - High");

        // Assert
        Assert.AreEqual(BugCriticality.High, result);
    }

    [TestMethod]
    public void MapSeverityToCriticality_MediumWithNumber_ReturnsMedium()
    {
        // Act
        var result = _service.MapSeverityToCriticality("3 - Medium");

        // Assert
        Assert.AreEqual(BugCriticality.Medium, result);
    }

    [TestMethod]
    public void MapSeverityToCriticality_LowWithNumber_ReturnsLow()
    {
        // Act
        var result = _service.MapSeverityToCriticality("4 - Low");

        // Assert
        Assert.AreEqual(BugCriticality.Low, result);
    }

    [TestMethod]
    public void MapSeverityToCriticality_NullSeverity_ReturnsMedium()
    {
        // Act
        var result = _service.MapSeverityToCriticality(null);

        // Assert
        Assert.AreEqual(BugCriticality.Medium, result);
    }

    [TestMethod]
    public void MapSeverityToCriticality_UnknownSeverity_ReturnsMedium()
    {
        // Act
        var result = _service.MapSeverityToCriticality("Unknown");

        // Assert
        Assert.AreEqual(BugCriticality.Medium, result);
    }

    #endregion

    #region GetBugCriticality Tests

    [TestMethod]
    public void GetBugCriticality_WithPriority_UsesPriority()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "Microsoft.VSTS.Common.Priority", 1 },
            { "Microsoft.VSTS.Common.Severity", "4 - Low" } // Should be ignored
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetBugCriticality(bug);

        // Assert
        Assert.AreEqual(BugCriticality.Critical, result); // From Priority 1, not Severity
    }

    [TestMethod]
    public void GetBugCriticality_WithOnlySeverity_UsesSeverity()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "Microsoft.VSTS.Common.Severity", "2 - High" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetBugCriticality(bug);

        // Assert
        Assert.AreEqual(BugCriticality.High, result);
    }

    [TestMethod]
    public void GetBugCriticality_WithNoFields_ReturnsDefaultMedium()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "System.Title", "Test Bug" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetBugCriticality(bug);

        // Assert
        Assert.AreEqual(BugCriticality.Medium, result);
    }

    #endregion

    #region MapCriticalityToPriority Tests

    [TestMethod]
    public void MapCriticalityToPriority_Critical_Returns1()
    {
        // Act
        var result = _service.MapCriticalityToPriority(BugCriticality.Critical);

        // Assert
        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public void MapCriticalityToPriority_High_Returns2()
    {
        // Act
        var result = _service.MapCriticalityToPriority(BugCriticality.High);

        // Assert
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void MapCriticalityToPriority_Medium_Returns3()
    {
        // Act
        var result = _service.MapCriticalityToPriority(BugCriticality.Medium);

        // Assert
        Assert.AreEqual(3, result);
    }

    [TestMethod]
    public void MapCriticalityToPriority_Low_Returns4()
    {
        // Act
        var result = _service.MapCriticalityToPriority(BugCriticality.Low);

        // Assert
        Assert.AreEqual(4, result);
    }

    [TestMethod]
    public void MapCriticalityToPriority_Unknown_Returns3()
    {
        // Act
        var result = _service.MapCriticalityToPriority("Unknown");

        // Assert
        Assert.AreEqual(3, result); // Should default to Medium (3)
    }

    #endregion
}
