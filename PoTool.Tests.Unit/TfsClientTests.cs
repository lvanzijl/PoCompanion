using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PoTool.Api.Services;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Tests.Unit;

[TestClass]
public class TfsClientTests
{
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private HttpClient _httpClient = null!;
    private TfsConfigurationService _configService = null!;
    private PoToolDbContext _dbContext = null!;
    private Mock<ILogger<TfsClient>> _loggerMock = null!;
    private TfsClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _responseIndex = 0;
        _responses.Clear();
        
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        
        // Create in-memory database for config service
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new PoToolDbContext(options);
        
        // Create real config service with ephemeral data protection
        var dataProtectionProvider = DataProtectionProvider.Create("PoToolTests");
        var configLogger = new Mock<ILogger<TfsConfigurationService>>();
        _configService = new TfsConfigurationService(_dbContext, dataProtectionProvider, configLogger.Object);
        
        _loggerMock = new Mock<ILogger<TfsClient>>();
        
        _client = new TfsClient(_httpClient, _configService, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _httpClient.Dispose();
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_ParsesParentId_WhenParentExists()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "test-pat");


        // Mock WIQL response
        var wiqlResponse = new
        {
            workItems = new[]
            {
                new { id = 100 },
                new { id = 200 }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        // Mock work items response with parent relationship
        var workItemsResponse = new
        {
            value = new[]
            {
                new
                {
                    id = 100,
                    fields = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Feature",
                        ["System.Title"] = "Parent Feature",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject\\Area1",
                        ["System.IterationPath"] = "TestProject\\Sprint1"
                        // No parent - this is a root item
                    }
                },
                new
                {
                    id = 200,
                    fields = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "User Story",
                        ["System.Title"] = "Child Story",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject\\Area1",
                        ["System.IterationPath"] = "TestProject\\Sprint1",
                        ["System.Parent"] = "https://dev.azure.com/testorg/_apis/wit/workItems/100"
                    }
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(workItemsResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject\\Area1")).ToList();

        // Assert
        Assert.AreEqual(2, results.Count);
        
        var parentItem = results.First(x => x.TfsId == 100);
        Assert.IsNull(parentItem.ParentTfsId, "Root item should have no parent");
        Assert.AreEqual("Parent Feature", parentItem.Title);
        
        var childItem = results.First(x => x.TfsId == 200);
        Assert.IsNotNull(childItem.ParentTfsId, "Child item should have parent");
        Assert.AreEqual(100, childItem.ParentTfsId.Value);
        Assert.AreEqual("Child Story", childItem.Title);
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_HandlesMultipleLevelsOfHierarchy()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "test-pat");


        // Mock WIQL response - Epic -> Feature -> User Story
        var wiqlResponse = new
        {
            workItems = new[]
            {
                new { id = 1 },  // Epic
                new { id = 2 },  // Feature
                new { id = 3 }   // User Story
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        // Mock work items with 3-level hierarchy
        var workItemsResponse = new
        {
            value = new[]
            {
                new
                {
                    id = 1,
                    fields = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Epic",
                        ["System.Title"] = "Epic 1",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject"
                    }
                },
                new
                {
                    id = 2,
                    fields = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Feature",
                        ["System.Title"] = "Feature 1",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject",
                        ["System.Parent"] = "https://dev.azure.com/testorg/_apis/wit/workItems/1"
                    }
                },
                new
                {
                    id = 3,
                    fields = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "User Story",
                        ["System.Title"] = "Story 1",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject",
                        ["System.Parent"] = "https://dev.azure.com/testorg/_apis/wit/workItems/2"
                    }
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(workItemsResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert
        Assert.AreEqual(3, results.Count);
        
        var epic = results.First(x => x.TfsId == 1);
        Assert.IsNull(epic.ParentTfsId, "Epic should have no parent");
        
        var feature = results.First(x => x.TfsId == 2);
        Assert.AreEqual(1, feature.ParentTfsId, "Feature should be child of Epic");
        
        var story = results.First(x => x.TfsId == 3);
        Assert.AreEqual(2, story.ParentTfsId, "Story should be child of Feature");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_HandlesEmptyParentField()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "test-pat");


        var wiqlResponse = new { workItems = new[] { new { id = 100 } } };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        var workItemsResponse = new
        {
            value = new[]
            {
                new
                {
                    id = 100,
                    fields = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Feature",
                        ["System.Title"] = "Test Feature",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject",
                        ["System.Parent"] = ""  // Empty string parent
                    }
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(workItemsResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert
        Assert.AreEqual(1, results.Count);
        Assert.IsNull(results[0].ParentTfsId, "Empty parent should be treated as null");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_HandlesInvalidParentUrl()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "test-pat");


        var wiqlResponse = new { workItems = new[] { new { id = 100 } } };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        var workItemsResponse = new
        {
            value = new[]
            {
                new
                {
                    id = 100,
                    fields = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Feature",
                        ["System.Title"] = "Test Feature",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject",
                        ["System.Parent"] = "invalid-url-no-id"  // Invalid parent URL
                    }
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(workItemsResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert
        Assert.AreEqual(1, results.Count);
        Assert.IsNull(results[0].ParentTfsId, "Invalid parent URL should be treated as null");
    }

    private int _responseIndex = 0;
    private readonly List<(HttpStatusCode statusCode, string content)> _responses = new();

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _responses.Add((statusCode, content));
        
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var (code, responseContent) = _responses[_responseIndex];
                _responseIndex++;
                return new HttpResponseMessage
                {
                    StatusCode = code,
                    Content = new StringContent(responseContent)
                };
            });
    }
}
