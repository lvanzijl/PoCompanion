using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Client.Models;
using System.Text.Json;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class TfsFieldParserServiceTests
{
    private TfsFieldParserService _service = null!;
    private Mock<ILogger<TfsFieldParserService>> _mockLogger = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _mockLogger = new Mock<ILogger<TfsFieldParserService>>();
        _service = new TfsFieldParserService(_mockLogger.Object);
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

    #region MapPriorityToSeverity Tests

    [TestMethod]
    public void MapPriorityToSeverity_Priority1_ReturnsCritical()
    {
        // Act
        var result = _service.MapPriorityToSeverity("1");

        // Assert
        Assert.AreEqual(BugSeverity.Critical, result);
    }

    [TestMethod]
    public void MapPriorityToSeverity_Priority2_ReturnsHigh()
    {
        // Act
        var result = _service.MapPriorityToSeverity("2");

        // Assert
        Assert.AreEqual(BugSeverity.High, result);
    }

    [TestMethod]
    public void MapPriorityToSeverity_Priority3_ReturnsMedium()
    {
        // Act
        var result = _service.MapPriorityToSeverity("3");

        // Assert
        Assert.AreEqual(BugSeverity.Medium, result);
    }

    [TestMethod]
    public void MapPriorityToSeverity_Priority4_ReturnsLow()
    {
        // Act
        var result = _service.MapPriorityToSeverity("4");

        // Assert
        Assert.AreEqual(BugSeverity.Low, result);
    }

    [TestMethod]
    public void MapPriorityToSeverity_NullPriority_ReturnsMedium()
    {
        // Act
        var result = _service.MapPriorityToSeverity(null);

        // Assert
        Assert.AreEqual(BugSeverity.Medium, result);
    }

    [TestMethod]
    public void MapPriorityToSeverity_UnknownPriority_ReturnsMedium()
    {
        // Act
        var result = _service.MapPriorityToSeverity("5");

        // Assert
        Assert.AreEqual(BugSeverity.Medium, result);
    }

    #endregion

    #region NormalizeSeverity Tests

    [TestMethod]
    public void NormalizeSeverity_CriticalWithNumber_ReturnsCritical()
    {
        // Act
        var result = _service.NormalizeSeverity("1 - Critical");

        // Assert
        Assert.AreEqual(BugSeverity.Critical, result);
    }

    [TestMethod]
    public void NormalizeSeverity_CriticalPlain_ReturnsCritical()
    {
        // Act
        var result = _service.NormalizeSeverity("Critical");

        // Assert
        Assert.AreEqual(BugSeverity.Critical, result);
    }

    [TestMethod]
    public void NormalizeSeverity_HighWithNumber_ReturnsHigh()
    {
        // Act
        var result = _service.NormalizeSeverity("2 - High");

        // Assert
        Assert.AreEqual(BugSeverity.High, result);
    }

    [TestMethod]
    public void NormalizeSeverity_MediumWithNumber_ReturnsMedium()
    {
        // Act
        var result = _service.NormalizeSeverity("3 - Medium");

        // Assert
        Assert.AreEqual(BugSeverity.Medium, result);
    }

    [TestMethod]
    public void NormalizeSeverity_LowWithNumber_ReturnsLow()
    {
        // Act
        var result = _service.NormalizeSeverity("4 - Low");

        // Assert
        Assert.AreEqual(BugSeverity.Low, result);
    }

    [TestMethod]
    public void NormalizeSeverity_NullSeverity_ReturnsMedium()
    {
        // Act
        var result = _service.NormalizeSeverity(null);

        // Assert
        Assert.AreEqual(BugSeverity.Medium, result);
    }

    [TestMethod]
    public void NormalizeSeverity_UnknownSeverity_ReturnsMedium()
    {
        // Act
        var result = _service.NormalizeSeverity("Unknown");

        // Assert
        Assert.AreEqual(BugSeverity.Medium, result);
    }

    #endregion

    #region GetBugSeverity Tests

    [TestMethod]
    public void GetBugSeverity_WithSeverity_UsesSeverity()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "Microsoft.VSTS.Common.Severity", "1 - Critical" },
            { "Microsoft.VSTS.Common.Priority", 4 } // Should be ignored (fallback only)
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetBugSeverity(bug);

        // Assert
        Assert.AreEqual(BugSeverity.Critical, result); // From Severity, not Priority
    }

    [TestMethod]
    public void GetBugSeverity_WithOnlyPriority_FallsToPriority()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "Microsoft.VSTS.Common.Priority", 2 }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetBugSeverity(bug);

        // Assert
        Assert.AreEqual(BugSeverity.High, result); // Falls back to Priority
    }

    [TestMethod]
    public void GetBugSeverity_WithNoFields_ReturnsDefaultMedium()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "System.Title", "Test Bug" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetBugSeverity(bug);

        // Assert
        Assert.AreEqual(BugSeverity.Medium, result);
    }

    #endregion

    #region MapSeverityToPriority Tests

    [TestMethod]
    public void MapSeverityToPriority_Critical_Returns1()
    {
        // Act
        var result = _service.MapSeverityToPriority(BugSeverity.Critical);

        // Assert
        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public void MapSeverityToPriority_High_Returns2()
    {
        // Act
        var result = _service.MapSeverityToPriority(BugSeverity.High);

        // Assert
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void MapSeverityToPriority_Medium_Returns3()
    {
        // Act
        var result = _service.MapSeverityToPriority(BugSeverity.Medium);

        // Assert
        Assert.AreEqual(3, result);
    }

    [TestMethod]
    public void MapSeverityToPriority_Low_Returns4()
    {
        // Act
        var result = _service.MapSeverityToPriority(BugSeverity.Low);

        // Assert
        Assert.AreEqual(4, result);
    }

    [TestMethod]
    public void MapSeverityToPriority_Unknown_Returns3()
    {
        // Act
        var result = _service.MapSeverityToPriority("Unknown");

        // Assert
        Assert.AreEqual(3, result); // Should default to Medium (3)
    }

    #endregion
}
