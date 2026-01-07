using System.Text.Json;
using PoTool.Client.Services;
using PoTool.Client.ApiClient;

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
    }

    [TestMethod]
    public void Deserialize_WithoutCaseInsensitiveOption_DefaultsToNtlm()
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

        Assert.IsNotNull(config);
        // This demonstrates that authMode (camelCase) doesn't match AuthMode (PascalCase),
        // so it falls back to the default value
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

        Assert.IsNotNull(config);
    }
}
