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

    private WorkItemWithValidationDto CreateBugWithJsonPayload(Dictionary<string, object> fields, string? tags = null)
    {
        var jsonPayload = JsonSerializer.Serialize(fields);
        
        // Extract Severity from fields if present
        string? severity = null;
        if (fields.TryGetValue("Microsoft.VSTS.Common.Severity", out var severityObj))
        {
            severity = severityObj?.ToString();
        }
        
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
            ValidationIssues = new List<ValidationIssue>(),
            CreatedDate = DateTimeOffset.UtcNow,
            ClosedDate = null,
            Severity = severity,
            Tags = tags
        };
    }

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

    [TestMethod]
    public void GetTags_WithCachedTagsField_PrefersCachedField()
    {
        // Arrange - cached Tags field should be preferred over JsonPayload
        var fields = new Dictionary<string, object>
        {
            { "System.Tags", "JsonTag1; JsonTag2" }
        };
        var bug = CreateBugWithJsonPayload(fields, tags: "CachedTag1; CachedTag2");

        // Act
        var result = _service.GetTags(bug);

        // Assert
        Assert.HasCount(2, result);
        Assert.Contains("CachedTag1", result);
        Assert.Contains("CachedTag2", result);
        // Should NOT contain JsonPayload tags
        Assert.DoesNotContain("JsonTag1", result);
        Assert.DoesNotContain("JsonTag2", result);
    }

    [TestMethod]
    public void GetTags_WithEmptyCachedField_FallsBackToJsonPayload()
    {
        // Arrange - empty cached field should fallback to JsonPayload
        var fields = new Dictionary<string, object>
        {
            { "System.Tags", "JsonTag1; JsonTag2" }
        };
        var bug = CreateBugWithJsonPayload(fields, tags: "");

        // Act
        var result = _service.GetTags(bug);

        // Assert
        Assert.HasCount(2, result);
        Assert.Contains("JsonTag1", result);
        Assert.Contains("JsonTag2", result);
    }

    [TestMethod]
    public void GetTags_WithNullCachedField_FallsBackToJsonPayload()
    {
        // Arrange - null cached field should fallback to JsonPayload
        var fields = new Dictionary<string, object>
        {
            { "System.Tags", "JsonTag1; JsonTag2" }
        };
        var bug = CreateBugWithJsonPayload(fields, tags: null);

        // Act
        var result = _service.GetTags(bug);

        // Assert
        Assert.HasCount(2, result);
        Assert.Contains("JsonTag1", result);
        Assert.Contains("JsonTag2", result);
    }

    #endregion

    #region GetBugSeverity Tests

    [TestMethod]
    public void GetBugSeverity_WithSeverity_ReturnsSeverity()
    {
        // Arrange
        var fields = new Dictionary<string, object>
        {
            { "Microsoft.VSTS.Common.Severity", "1 - Critical" }
        };
        var bug = CreateBugWithJsonPayload(fields);

        // Act
        var result = _service.GetBugSeverity(bug);

        // Assert
        Assert.AreEqual("1 - Critical", result); // Raw TFS value, no normalization
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
