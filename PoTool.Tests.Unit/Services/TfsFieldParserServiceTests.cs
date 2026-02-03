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

    #region GetBugSeverity Tests

    [TestMethod]
    public void GetBugSeverity_WithSeverity_ReturnsSeverity()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "Microsoft.VSTS.Common.Severity", "1 - Critical" },
            { "Microsoft.VSTS.Common.Priority", 4 } // Should be ignored
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetBugSeverity(bug);

        // Assert
        Assert.AreEqual("1 - Critical", result); // Raw TFS value, no normalization
    }

    [TestMethod]
    public void GetBugSeverity_WithOnlyPriority_ReturnsNull()
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
        Assert.IsNull(result); // No fallback to Priority
    }

    [TestMethod]
    public void GetBugSeverity_WithNoFields_ReturnsNull()
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
        Assert.IsNull(result); // No default to Medium
    }
    
    [TestMethod]
    public void GetBugSeverity_WithDifferentSeverityFormats_ReturnsRawValue()
    {
        // Arrange - test different TFS severity formats
        var testCases = new[]
        {
            ("1 - Critical", "1 - Critical"),
            ("2 - High", "2 - High"),
            ("3 - Medium", "3 - Medium"),
            ("4 - Low", "4 - Low")
        };

        foreach (var (input, expected) in testCases)
        {
            var fields = new Dictionary<string, object>
            {
                { "Microsoft.VSTS.Common.Severity", input }
            };
            var bug = CreateBugWithJsonPayload(fields);

            // Act
            var result = _service.GetBugSeverity(bug);

            // Assert
            Assert.AreEqual(expected, result, $"Failed for input: {input}");
        }
    }

    #endregion
}
