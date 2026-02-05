using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PoTool.Api.Services;
using PoTool.Api.Persistence;
using PoTool.Integrations.Tfs.Clients;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Tests for work item tag update operations.
/// Validates that tags can be added, removed, and cleared correctly.
/// </summary>
[TestClass]
public class WorkItemTagUpdateTests
{
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private HttpClient _httpClient = null!;
    private PoToolDbContext _dbContext = null!;
    private TfsConfigurationService _configService = null!;
    private Mock<ILogger<RealTfsClient>> _loggerMock = null!;
    private RealTfsClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        // Create in-memory database for config service
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new PoToolDbContext(options);

        // Create config service
        var configLogger = new Mock<ILogger<TfsConfigurationService>>();
        var gateLogger = new Mock<ILogger<EfConcurrencyGate>>();
        var gate = new EfConcurrencyGate(gateLogger.Object);
        _configService = new TfsConfigurationService(_dbContext, configLogger.Object, gate);

        _loggerMock = new Mock<ILogger<RealTfsClient>>();

        // Create throttler
        var throttlerLogger = new Mock<ILogger<TfsRequestThrottler>>();
        var throttler = new TfsRequestThrottler(throttlerLogger.Object, readConcurrency: 10, writeConcurrency: 10);

        // Create request sender
        var senderLogger = new Mock<ILogger<TfsRequestSender>>();
        var requestSender = new TfsRequestSender(senderLogger.Object);

        // Create mock IHttpClientFactory
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _client = new RealTfsClient(
            mockFactory.Object,
            _configService,
            _loggerMock.Object,
            throttler,
            requestSender);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _httpClient.Dispose();
    }

    [TestMethod]
    public async Task UpdateWorkItemTagsAndReturnAsync_AddingTags_SendsCorrectPatchDocument()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        string? capturedRequestBody = null;
        SetupHttpResponseWithCapture(HttpStatusCode.OK, CreateSuccessfulPatchResponse(123, "Tag1; Tag2; Tag3"), 
            (body) => capturedRequestBody = body);

        var tags = new List<string> { "Tag1", "Tag2", "Tag3" };

        // Act
        var result = await _client.UpdateWorkItemTagsAndReturnAsync(123, tags);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(123, result.TfsId);
        
        // Verify the patch document uses 'replace' and correct format
        Assert.IsNotNull(capturedRequestBody);
        var patchDoc = JsonDocument.Parse(capturedRequestBody);
        var operation = patchDoc.RootElement[0];
        
        Assert.AreEqual("replace", operation.GetProperty("op").GetString());
        Assert.AreEqual("/fields/System.Tags", operation.GetProperty("path").GetString());
        Assert.AreEqual("Tag1; Tag2; Tag3", operation.GetProperty("value").GetString());
    }

    [TestMethod]
    public async Task UpdateWorkItemTagsAndReturnAsync_RemovingSomeTags_SendsCorrectPatchDocument()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        string? capturedRequestBody = null;
        SetupHttpResponseWithCapture(HttpStatusCode.OK, CreateSuccessfulPatchResponse(123, "Tag1"), 
            (body) => capturedRequestBody = body);

        // Removing Tag2 and Tag3, keeping only Tag1
        var tags = new List<string> { "Tag1" };

        // Act
        var result = await _client.UpdateWorkItemTagsAndReturnAsync(123, tags);

        // Assert
        Assert.IsNotNull(result);
        
        // Verify the patch document uses 'replace'
        Assert.IsNotNull(capturedRequestBody);
        var patchDoc = JsonDocument.Parse(capturedRequestBody);
        var operation = patchDoc.RootElement[0];
        
        Assert.AreEqual("replace", operation.GetProperty("op").GetString());
        Assert.AreEqual("/fields/System.Tags", operation.GetProperty("path").GetString());
        Assert.AreEqual("Tag1", operation.GetProperty("value").GetString());
    }

    [TestMethod]
    public async Task UpdateWorkItemTagsAndReturnAsync_RemovingAllTags_SendsEmptyString()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        string? capturedRequestBody = null;
        SetupHttpResponseWithCapture(HttpStatusCode.OK, CreateSuccessfulPatchResponse(123, ""), 
            (body) => capturedRequestBody = body);

        var tags = new List<string>();

        // Act
        var result = await _client.UpdateWorkItemTagsAndReturnAsync(123, tags);

        // Assert
        Assert.IsNotNull(result);
        
        // Verify the patch document uses 'replace' and empty string (not null)
        Assert.IsNotNull(capturedRequestBody);
        var patchDoc = JsonDocument.Parse(capturedRequestBody);
        var operation = patchDoc.RootElement[0];
        
        Assert.AreEqual("replace", operation.GetProperty("op").GetString());
        Assert.AreEqual("/fields/System.Tags", operation.GetProperty("path").GetString());
        
        // Value should be empty string, not null
        var valueElement = operation.GetProperty("value");
        Assert.AreEqual(JsonValueKind.String, valueElement.ValueKind);
        Assert.AreEqual("", valueElement.GetString());
    }

    [TestMethod]
    public async Task UpdateWorkItemTagsAndReturnAsync_TagsWithWhitespace_NormalizesCorrectly()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        string? capturedRequestBody = null;
        SetupHttpResponseWithCapture(HttpStatusCode.OK, CreateSuccessfulPatchResponse(123, "Tag1; Tag2"), 
            (body) => capturedRequestBody = body);

        // Tags with leading/trailing whitespace
        var tags = new List<string> { "  Tag1  ", " Tag2 " };

        // Act
        var result = await _client.UpdateWorkItemTagsAndReturnAsync(123, tags);

        // Assert
        Assert.IsNotNull(result);
        
        // Verify tags are trimmed
        Assert.IsNotNull(capturedRequestBody);
        var patchDoc = JsonDocument.Parse(capturedRequestBody);
        var operation = patchDoc.RootElement[0];
        var value = operation.GetProperty("value").GetString();
        
        Assert.AreEqual("Tag1; Tag2", value);
        Assert.DoesNotContain("  ", value!, "Should not contain multiple spaces");
    }

    [TestMethod]
    public async Task UpdateWorkItemTagsAndReturnAsync_EmptyTagsInList_FiltersThemOut()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        string? capturedRequestBody = null;
        SetupHttpResponseWithCapture(HttpStatusCode.OK, CreateSuccessfulPatchResponse(123, "Tag1; Tag2"), 
            (body) => capturedRequestBody = body);

        // Tags with empty strings mixed in
        var tags = new List<string> { "Tag1", "", "  ", "Tag2" };

        // Act
        var result = await _client.UpdateWorkItemTagsAndReturnAsync(123, tags);

        // Assert
        Assert.IsNotNull(result);
        
        // Verify empty tags are filtered out
        Assert.IsNotNull(capturedRequestBody);
        var patchDoc = JsonDocument.Parse(capturedRequestBody);
        var operation = patchDoc.RootElement[0];
        var value = operation.GetProperty("value").GetString();
        
        Assert.AreEqual("Tag1; Tag2", value);
    }

    [TestMethod]
    public async Task UpdateWorkItemTagsAndReturnAsync_DuplicateTags_Deduplicates()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        string? capturedRequestBody = null;
        SetupHttpResponseWithCapture(HttpStatusCode.OK, CreateSuccessfulPatchResponse(123, "tag1; tag2"), 
            (body) => capturedRequestBody = body);

        // Duplicate tags with different casing
        var tags = new List<string> { "tag1", "Tag1", "TAG1", "tag2" };

        // Act
        var result = await _client.UpdateWorkItemTagsAndReturnAsync(123, tags);

        // Assert
        Assert.IsNotNull(result);
        
        // Verify duplicates are removed (case-insensitive)
        Assert.IsNotNull(capturedRequestBody);
        var patchDoc = JsonDocument.Parse(capturedRequestBody);
        var operation = patchDoc.RootElement[0];
        var value = operation.GetProperty("value").GetString();
        
        // Should only contain each tag once
        var resultTags = value!.Split("; ");
        Assert.HasCount(2, resultTags);
    }

    private void SetupHttpResponseWithCapture(HttpStatusCode statusCode, string content, Action<string> captureBody)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                // Capture the request body
                if (request.Content != null)
                {
                    var body = request.Content.ReadAsStringAsync().Result;
                    captureBody(body);
                }
                
                return new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                };
            });
    }

    private static string CreateSuccessfulPatchResponse(int workItemId, string tags)
    {
        var response = new
        {
            id = workItemId,
            fields = new Dictionary<string, object>
            {
                ["System.WorkItemType"] = "Task",
                ["System.Title"] = "Test Work Item",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "TestProject",
                ["System.IterationPath"] = "TestProject",
                ["System.Tags"] = tags
            }
        };
        return JsonSerializer.Serialize(response);
    }
}
