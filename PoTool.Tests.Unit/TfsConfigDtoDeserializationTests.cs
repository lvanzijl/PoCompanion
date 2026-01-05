using System.Text.Json;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit;

/// <summary>
/// Tests for TfsConfigDto JSON deserialization.
/// Verifies that the configuration DTO correctly deserializes from API JSON responses.
/// </summary>
[TestClass]
public class TfsConfigDtoDeserializationTests
{
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [TestMethod]
    public void Deserialize_WithCamelCaseAuthMode_CorrectlyParsesNtlm()
    {
        // Arrange - JSON with camelCase property names (as returned by API)
        var json = """
            {
                "url": "https://tfs.mycompany.com",
                "project": "MyProject",
                "authMode": 1,
                "useDefaultCredentials": true,
                "timeoutSeconds": 30,
                "apiVersion": "7.0"
            }
            """;

        // Act
        var config = JsonSerializer.Deserialize<TfsConfigDto>(json, CaseInsensitiveOptions);

        // Assert
        Assert.IsNotNull(config);
        Assert.AreEqual("https://tfs.mycompany.com", config.Url);
        Assert.AreEqual("MyProject", config.Project);
        Assert.AreEqual(TfsAuthMode.Ntlm, config.AuthMode, "AuthMode should be NTLM (1), not default PAT (0)");
        Assert.IsTrue(config.UseDefaultCredentials, "UseDefaultCredentials should be true");
        Assert.AreEqual(30, config.TimeoutSeconds);
        Assert.AreEqual("7.0", config.ApiVersion);
    }

    [TestMethod]
    public void Deserialize_WithCamelCaseAuthMode_CorrectlyParsesPat()
    {
        // Arrange - JSON with camelCase property names (as returned by API)
        var json = """
            {
                "url": "https://dev.azure.com/myorg",
                "project": "MyProject",
                "authMode": 0,
                "useDefaultCredentials": false,
                "timeoutSeconds": 60,
                "apiVersion": "7.0"
            }
            """;

        // Act
        var config = JsonSerializer.Deserialize<TfsConfigDto>(json, CaseInsensitiveOptions);

        // Assert
        Assert.IsNotNull(config);
        Assert.AreEqual(TfsAuthMode.Pat, config.AuthMode, "AuthMode should be PAT (0)");
    }

    [TestMethod]
    public void Deserialize_WithoutCaseInsensitiveOption_DefaultsToPatIncorrectly()
    {
        // Arrange - JSON with camelCase property names (as returned by API)
        // Without PropertyNameCaseInsensitive = true, authMode won't match AuthMode
        var json = """
            {
                "url": "https://tfs.mycompany.com",
                "project": "MyProject",
                "authMode": 1,
                "useDefaultCredentials": true,
                "timeoutSeconds": 30,
                "apiVersion": "7.0"
            }
            """;

        // Act - Deserialize without case-insensitive option (default behavior)
        var config = JsonSerializer.Deserialize<TfsConfigDto>(json);

        // Assert - Without case-insensitive matching, AuthMode defaults to Pat (0)
        Assert.IsNotNull(config);
        // This demonstrates the bug: authMode (camelCase) doesn't match AuthMode (PascalCase)
        Assert.AreEqual(TfsAuthMode.Pat, config.AuthMode, 
            "Without case-insensitive option, AuthMode defaults to Pat because 'authMode' doesn't match 'AuthMode'");
    }

    [TestMethod]
    public void Deserialize_WithPascalCaseAuthMode_WorksWithoutCaseInsensitiveOption()
    {
        // Arrange - JSON with PascalCase property names (not typical from ASP.NET Core API)
        var json = """
            {
                "Url": "https://tfs.mycompany.com",
                "Project": "MyProject",
                "AuthMode": 1,
                "UseDefaultCredentials": true,
                "TimeoutSeconds": 30,
                "ApiVersion": "7.0"
            }
            """;

        // Act - Deserialize without case-insensitive option
        var config = JsonSerializer.Deserialize<TfsConfigDto>(json);

        // Assert - With PascalCase matching, AuthMode is correctly parsed
        Assert.IsNotNull(config);
        Assert.AreEqual(TfsAuthMode.Ntlm, config.AuthMode, "AuthMode should be NTLM when property names match exactly");
    }
}
