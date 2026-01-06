using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
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
    private TfsAuthenticationProvider _authProvider = null!;
    private PoToolDbContext _dbContext = null!;
    private Mock<ILogger<RealTfsClient>> _loggerMock = null!;
    private RealTfsClient _client = null!;

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
        
        // Create config service (no longer requires data protection)
        var configLogger = new Mock<ILogger<TfsConfigurationService>>();
        _configService = new TfsConfigurationService(_dbContext, configLogger.Object);
        
        _authProvider = new TfsAuthenticationProvider();
        _loggerMock = new Mock<ILogger<RealTfsClient>>();
        
        // Create mock PatAccessor that returns null (tests don't need PAT auth for mock responses)
        var mockPatAccessor = new Mock<PatAccessor>(Mock.Of<IHttpContextAccessor>());
        mockPatAccessor.Setup(p => p.GetPat()).Returns((string?)null);
        
        // Create mock IHttpClientFactory
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        
        _client = new RealTfsClient(_httpClient, mockFactory.Object, _configService, _authProvider, mockPatAccessor.Object, _loggerMock.Object);
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
        // Arrange - Note: PAT parameter removed
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            TfsAuthMode.Pat);


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
        Assert.HasCount(2, results);
        
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
        // Arrange - Note: PAT parameter removed
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            TfsAuthMode.Pat);


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

        // Mock work items with 3-level test scenario (Epic → Feature → User Story)
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
        Assert.HasCount(3, results);
        
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
        // Arrange - Note: PAT parameter removed
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            TfsAuthMode.Pat);


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
        Assert.HasCount(1, results);
        Assert.IsNull(results[0].ParentTfsId, "Empty parent should be treated as null");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_HandlesInvalidParentUrl()
    {
        // Arrange - Note: PAT parameter removed
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            TfsAuthMode.Pat);


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
        Assert.HasCount(1, results);
        Assert.IsNull(results[0].ParentTfsId, "Invalid parent URL should be treated as null");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_EmptyResponse_ReturnsEmptyList()
    {
        // Arrange
        var emptyResponse = new
        {
            count = 0,
            value = Array.Empty<object>()
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(emptyResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert
        Assert.IsEmpty(results, "Empty response should return empty list");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_NullFields_HandledGracefully()
    {
        // Arrange
        var workItemsResponse = new
        {
            count = 1,
            value = new[]
            {
                new
                {
                    id = 123,
                    url = "http://test.com/123",
                    fields = new Dictionary<string, object?>
                    {
                        ["System.WorkItemType"] = "Task",
                        ["System.Title"] = null,  // Null title
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = null,  // Null area path
                        ["System.IterationPath"] = "TestProject"
                    }
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(workItemsResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(string.Empty, results[0].Title, "Null title should be converted to empty string");
        Assert.IsNotNull(results[0].AreaPath, "Null area path should be handled");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_VeryLargeWorkItemCount_ReturnsAllItems()
    {
        // Arrange - Simulate large batch of work items
        var items = Enumerable.Range(1, 500).Select(i => new
        {
            id = i,
            url = $"http://test.com/{i}",
            fields = new Dictionary<string, object>
            {
                ["System.WorkItemType"] = "Task",
                ["System.Title"] = $"Task {i}",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "TestProject",
                ["System.IterationPath"] = "TestProject"
            }
        }).ToArray();

        var workItemsResponse = new
        {
            count = items.Length,
            value = items
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(workItemsResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert
        Assert.HasCount(500, results, "Should handle large result sets");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_SpecialCharactersInFields_HandledCorrectly()
    {
        // Arrange
        var workItemsResponse = new
        {
            count = 1,
            value = new[]
            {
                new
                {
                    id = 123,
                    url = "http://test.com/123",
                    fields = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Task",
                        ["System.Title"] = "Test with \"quotes\" and 'apostrophes' & <html> tags",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "Project\\Area\\SubArea",
                        ["System.IterationPath"] = "Sprint\\2024\\Q4"
                    }
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(workItemsResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert
        Assert.HasCount(1, results);
        Assert.Contains("quotes", results[0].Title, "Should preserve special characters in title");
        Assert.Contains("\\", results[0].AreaPath, "Should preserve backslashes in paths");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_MixedValidAndInvalidData_ProcessesValidItems()
    {
        // Arrange - Mix of valid and items with missing required fields
        var workItemsResponse = new
        {
            count = 3,
            value = new[]
            {
                new
                {
                    id = 1,
                    url = "http://test.com/1",
                    fields = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Task",
                        ["System.Title"] = "Valid Task 1",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject"
                    }
                },
                new
                {
                    id = 2,
                    url = "http://test.com/2",
                    fields = new Dictionary<string, object>
                    {
                        // Missing WorkItemType - should be skipped or handled
                        ["System.Title"] = "Invalid Task",
                        ["System.State"] = "Active"
                    }
                },
                new
                {
                    id = 3,
                    url = "http://test.com/3",
                    fields = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Task",
                        ["System.Title"] = "Valid Task 2",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject"
                    }
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(workItemsResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert - Should process at least the 2 valid items (may process all 3)
        Assert.IsGreaterThanOrEqualTo(results.Count, 2, "Should process at least the 2 valid items");
        Assert.IsLessThanOrEqualTo(results.Count, 3, "Should not process more than 3 items total");
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
