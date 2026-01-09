using PoTool.Api.Helpers;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public class InputValidatorTests
{
    [TestMethod]
    public void SanitizeFilter_WithNullInput_ReturnsEmptyString()
    {
        // Act
        var result = InputValidator.SanitizeFilter(null);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void SanitizeFilter_WithEmptyInput_ReturnsEmptyString()
    {
        // Act
        var result = InputValidator.SanitizeFilter("");

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void SanitizeFilter_WithWhitespaceInput_ReturnsEmptyString()
    {
        // Act
        var result = InputValidator.SanitizeFilter("   ");

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void SanitizeFilter_WithValidInput_ReturnsUnchanged()
    {
        // Arrange
        var input = "ValidFilterText123";

        // Act
        var result = InputValidator.SanitizeFilter(input);

        // Assert
        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void SanitizeFilter_RemovesDangerousCharacters()
    {
        // Arrange
        var input = "Filter<script>alert('xss')</script>";

        // Act
        var result = InputValidator.SanitizeFilter(input);

        // Assert
        
#pragma warning disable MSTEST0037
        Assert.IsFalse(result.Contains('<'));
        
#pragma warning disable MSTEST0037
        Assert.IsFalse(result.Contains('>'));
#pragma warning disable MSTEST0037
        Assert.IsFalse(result.Contains('\''));
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.Contains("Filter"));
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.Contains("script"));
    }

    [TestMethod]
    public void SanitizeFilter_RemovesSQLInjectionAttempts()
    {
        // Arrange
        var input = "test'; DROP TABLE WorkItems; --";

        // Act
        var result = InputValidator.SanitizeFilter(input);

        // Assert
#pragma warning disable MSTEST0037
        Assert.IsFalse(result.Contains('\''));
        
#pragma warning disable MSTEST0037
        Assert.IsFalse(result.Contains(';'));
        Assert.Contains(result, "test");
        Assert.Contains(result, "DROP");
    }

    [TestMethod]
    public void SanitizeFilter_TruncatesLongInput()
    {
        // Arrange
        var input = new string('a', 250);

        // Act
        var result = InputValidator.SanitizeFilter(input);

        // Assert
        Assert.HasCount(200, result);
    }

    [TestMethod]
    public void SanitizeFilter_TrimsWhitespace()
    {
        // Arrange
        var input = "  test filter  ";

        // Act
        var result = InputValidator.SanitizeFilter(input);

        // Assert
        Assert.AreEqual("test filter", result);
    }

    [TestMethod]
    public void IsValidAreaPath_WithNull_ReturnsFalse()
    {
        // Act
        var result = InputValidator.IsValidAreaPath(null);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidAreaPath_WithEmpty_ReturnsFalse()
    {
        // Act
        var result = InputValidator.IsValidAreaPath("");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidAreaPath_WithValidPath_ReturnsTrue()
    {
        // Arrange
        var validPaths = new[]
        {
            "ProjectName",
            "ProjectName\\TeamA",
            "ProjectName\\TeamA\\Component-1",
            "Project_Name\\Team-A\\Component_1"
        };

        // Act & Assert
        foreach (var path in validPaths)
        {
            Assert.IsTrue(InputValidator.IsValidAreaPath(path), $"Path '{path}' should be valid");
        }
    }

    [TestMethod]
    public void IsValidAreaPath_WithInvalidCharacters_ReturnsFalse()
    {
        // Arrange
        var invalidPaths = new[]
        {
            "Project<script>",
            "Project/Team",
            "Project;DROP",
            "Project\"Team",
            "Project'Team"
        };

        // Act & Assert
        foreach (var path in invalidPaths)
        {
            Assert.IsFalse(InputValidator.IsValidAreaPath(path), $"Path '{path}' should be invalid");
        }
    }

    [TestMethod]
    public void IsValidAreaPath_WithSQLInjection_ReturnsFalse()
    {
        // Arrange
        var maliciousPath = "Project'; DROP TABLE WorkItems; --";

        // Act
        var result = InputValidator.IsValidAreaPath(maliciousPath);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsValidAreaPath_WithXSSAttempt_ReturnsFalse()
    {
        // Arrange
        var maliciousPath = "Project<script>alert('xss')</script>";

        // Act
        var result = InputValidator.IsValidAreaPath(maliciousPath);

        // Assert
        Assert.IsFalse(result);
    }
}
