using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PoTool.Tests.Integration.Support;

namespace PoTool.Tests.Integration;

/// <summary>
/// Integration tests for TFS configuration API endpoints.
/// Tests the full HTTP round-trip to verify AuthMode persistence.
/// </summary>
[TestClass]
public class TfsConfigApiTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _factory = new IntegrationTestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [TestMethod]
    public async Task PostTfsConfig_WithNtlmAuthMode_ReturnsOkAndPersists()
    {
        // Arrange
        var request = new
        {
            url = "https://tfs.mycompany.com",
            project = "MyProject",
            pat = (string?)null,
            authMode = 1, // NTLM
            useDefaultCredentials = true,
            timeoutSeconds = 30,
            apiVersion = "7.0"
        };

        // Act - Post config
        var postResponse = await _client.PostAsJsonAsync("/api/tfsconfig", request);

        // Assert - Post succeeded
        Assert.AreEqual(HttpStatusCode.OK, postResponse.StatusCode, "POST should return 200 OK");

        // Act - Get config to verify persistence
        var getResponse = await _client.GetAsync("/api/tfsconfig");

        // Assert - Get succeeded
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode, "GET should return 200 OK");

        var content = await getResponse.Content.ReadAsStringAsync();
        var config = JsonSerializer.Deserialize<TfsConfigResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.IsNotNull(config, "Config should not be null");
        Assert.AreEqual("https://tfs.mycompany.com", config.Url);
        Assert.AreEqual("MyProject", config.Project);
        Assert.AreEqual(1, config.AuthMode, "AuthMode should be 1 (NTLM)");
        Assert.AreEqual(true, config.UseDefaultCredentials);
    }

    [TestMethod]
    public async Task PostTfsConfig_SwitchingFromPatToNtlm_UpdatesAuthMode()
    {
        // Arrange - First save with PAT
        var patRequest = new
        {
            url = "https://dev.azure.com/org",
            project = "Project",
            pat = (string?)null,
            authMode = 0, // PAT
            useDefaultCredentials = false,
            timeoutSeconds = 30,
            apiVersion = "7.0"
        };

        await _client.PostAsJsonAsync("/api/tfsconfig", patRequest);

        // Arrange - Then switch to NTLM
        var ntlmRequest = new
        {
            url = "https://tfs.mycompany.com",
            project = "Project",
            pat = (string?)null,
            authMode = 1, // NTLM
            useDefaultCredentials = true,
            timeoutSeconds = 30,
            apiVersion = "7.0"
        };

        // Act
        await _client.PostAsJsonAsync("/api/tfsconfig", ntlmRequest);

        // Assert
        var getResponse = await _client.GetAsync("/api/tfsconfig");
        var content = await getResponse.Content.ReadAsStringAsync();
        var config = JsonSerializer.Deserialize<TfsConfigResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.IsNotNull(config);
        Assert.AreEqual(1, config.AuthMode, "AuthMode should be updated to 1 (NTLM)");
    }

    private class TfsConfigResponse
    {
        public string? Url { get; set; }
        public string? Project { get; set; }
        public int AuthMode { get; set; }
        public bool UseDefaultCredentials { get; set; }
        public int TimeoutSeconds { get; set; }
        public string? ApiVersion { get; set; }
    }
}
