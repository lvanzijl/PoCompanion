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
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Settings;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public class TfsClientTests
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
        var gateLogger = new Mock<ILogger<EfConcurrencyGate>>();
        var gate = new EfConcurrencyGate(gateLogger.Object); // Use real implementation instead of mock
        _configService = new TfsConfigurationService(_dbContext, configLogger.Object, gate);

        _loggerMock = new Mock<ILogger<RealTfsClient>>();

        // Create throttler (use real implementation for tests)
        var throttlerLogger = new Mock<ILogger<TfsRequestThrottler>>();
        var throttler = new TfsRequestThrottler(throttlerLogger.Object, readConcurrency: 10, writeConcurrency: 10);

        // Create request sender (use real implementation for tests)
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
    public async Task GetWorkItemsAsync_ParsesParentId_WhenParentExists()
    {
        // Arrange - Note: PAT parameter removed
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);


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

        // Mock work items response with parent relationship using relations array
        // (Updated to use System.LinkTypes.Hierarchy-Reverse relation instead of System.Parent field)
        var workItemsResponse = new
        {
            value = new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 100,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Feature",
                        ["System.Title"] = "Parent Feature",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject\\Area1",
                        ["System.IterationPath"] = "TestProject\\Sprint1"
                    }
                    // No relations - this is a root item
                },
                new Dictionary<string, object>
                {
                    ["id"] = 200,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "User Story",
                        ["System.Title"] = "Child Story",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject\\Area1",
                        ["System.IterationPath"] = "TestProject\\Sprint1"
                    },
                    ["relations"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["rel"] = "System.LinkTypes.Hierarchy-Reverse",
                            ["url"] = "https://dev.azure.com/testorg/_apis/wit/workItems/100"
                        }
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
            true);


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

        // Mock work items with 3-level hierarchy (Epic → Feature → User Story)
        // Updated to use relations array instead of System.Parent field
        var workItemsResponse = new
        {
            value = new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 1,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Epic",
                        ["System.Title"] = "Epic 1",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject"
                    }
                },
                new Dictionary<string, object>
                {
                    ["id"] = 2,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Feature",
                        ["System.Title"] = "Feature 1",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject"
                    },
                    ["relations"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["rel"] = "System.LinkTypes.Hierarchy-Reverse",
                            ["url"] = "https://dev.azure.com/testorg/_apis/wit/workItems/1"
                        }
                    }
                },
                new Dictionary<string, object>
                {
                    ["id"] = 3,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "User Story",
                        ["System.Title"] = "Story 1",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject"
                    },
                    ["relations"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["rel"] = "System.LinkTypes.Hierarchy-Reverse",
                            ["url"] = "https://dev.azure.com/testorg/_apis/wit/workItems/2"
                        }
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
    public async Task GetWorkItemsAsync_HandlesEmptyRelations()
    {
        // Arrange - Note: PAT parameter removed
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);


        var wiqlResponse = new { workItems = new[] { new { id = 100 } } };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        // Work item with empty relations array (no parent)
        var workItemsResponse = new
        {
            value = new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 100,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Feature",
                        ["System.Title"] = "Test Feature",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject"
                    },
                    ["relations"] = new object[] { }  // Empty relations array
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(workItemsResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert
        Assert.HasCount(1, results);
        Assert.IsNull(results[0].ParentTfsId, "Empty relations should result in null parent");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_HandlesInvalidRelationUrl()
    {
        // Arrange - Note: PAT parameter removed
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);


        var wiqlResponse = new { workItems = new[] { new { id = 100 } } };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        // Work item with invalid URL in hierarchy-reverse relation
        var workItemsResponse = new
        {
            value = new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 100,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Feature",
                        ["System.Title"] = "Test Feature",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject"
                    },
                    ["relations"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["rel"] = "System.LinkTypes.Hierarchy-Reverse",
                            ["url"] = "invalid-url-no-id"  // Invalid parent URL
                        }
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
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        // Mock empty WIQL response (no work items found)
        var wiqlResponse = new
        {
            workItems = Array.Empty<object>()
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert
        Assert.IsEmpty(results, "Empty response should return empty list");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_NullFields_HandledGracefully()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        // Mock WIQL response
        var wiqlResponse = new
        {
            workItems = new[] { new { id = 123 } }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        // Mock work items response with null fields
        var workItemsResponse = new
        {
            count = 1,
            value = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 123,
                    ["url"] = "http://test.com/123",
                    ["fields"] = new Dictionary<string, object?>
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
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        // Mock WIQL response with 500 work item IDs
        var wiqlResponse = new
        {
            workItems = Enumerable.Range(1, 500).Select(i => new { id = i }).ToArray()
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        // Simulate large batch of work items response
        // The implementation uses batch size of 200, so 500 items = 3 batches (200 + 200 + 100)
        // Two-phase retrieval: 3 batches for Phase 1 (relations) + 3 batches for Phase 2 (fields)

        // Phase 1: Fetch relations (3 batches)
        // For this test, we'll mock empty relations since we're testing count, not hierarchy
        for (int i = 0; i < 3; i++)
        {
            var start = i * RealTfsClient.WorkItemBatchSize + 1;
            var count = Math.Min(RealTfsClient.WorkItemBatchSize, 500 - i * RealTfsClient.WorkItemBatchSize);
            var relationsBatch = Enumerable.Range(start, count)
                .Select(id => new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["relations"] = Array.Empty<object>() // No relations for simplicity
                }).ToArray();
            SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new { count, value = relationsBatch }));
        }

        // Phase 2: Fetch fields (3 batches)
        // Batch 1: items 1-200
        var batch1Items = CreateMockWorkItemBatch(1, RealTfsClient.WorkItemBatchSize);
        var batch1Response = new { count = batch1Items.Length, value = batch1Items };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(batch1Response));

        // Batch 2: items 201-400
        var batch2Items = CreateMockWorkItemBatch(201, RealTfsClient.WorkItemBatchSize);
        var batch2Response = new { count = batch2Items.Length, value = batch2Items };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(batch2Response));

        // Batch 3: items 401-500
        var batch3Items = CreateMockWorkItemBatch(401, 100);
        var batch3Response = new { count = batch3Items.Length, value = batch3Items };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(batch3Response));

        // Act
        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        // Assert
        Assert.HasCount(500, results, "Should handle large result sets");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_SpecialCharactersInFields_HandledCorrectly()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        // Mock WIQL response
        var wiqlResponse = new
        {
            workItems = new[] { new { id = 123 } }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        // Mock work items response with special characters
        var workItemsResponse = new
        {
            count = 1,
            value = new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 123,
                    ["url"] = "http://test.com/123",
                    ["fields"] = new Dictionary<string, object>
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
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        // Mock WIQL response
        var wiqlResponse = new
        {
            workItems = new[] { new { id = 1 }, new { id = 2 }, new { id = 3 } }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        // Mix of valid and items with missing required fields
        var workItemsResponse = new
        {
            count = 3,
            value = new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 1,
                    ["url"] = "http://test.com/1",
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Task",
                        ["System.Title"] = "Valid Task 1",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "TestProject"
                    }
                },
                new Dictionary<string, object>
                {
                    ["id"] = 2,
                    ["url"] = "http://test.com/2",
                    ["fields"] = new Dictionary<string, object>
                    {
                        // Missing WorkItemType - should be handled (empty string)
                        ["System.Title"] = "Invalid Task",
                        ["System.State"] = "Active"
                    }
                },
                new Dictionary<string, object>
                {
                    ["id"] = 3,
                    ["url"] = "http://test.com/3",
                    ["fields"] = new Dictionary<string, object>
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

        // Assert - All items should be processed (missing fields become empty strings)
        Assert.HasCount(3, results, "All items should be processed");
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_PreservesStoryPointsEffortAndBusinessValueSeparately()
    {
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        var wiqlResponse = new
        {
            workItems = new[] { new { id = 42 } }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        var workItemsResponse = new
        {
            count = 1,
            value = new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 42,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Product Backlog Item",
                        ["System.Title"] = "Separate estimate fields",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "Sprint 1",
                        ["Microsoft.VSTS.Scheduling.Effort"] = 13,
                        ["Microsoft.VSTS.Scheduling.StoryPoints"] = 8,
                        ["Microsoft.VSTS.Common.BusinessValue"] = 21,
                        ["Microsoft.VSTS.Common.TimeCriticality"] = 72.5,
                        ["Rhodium.Funding.ProjectNumber"] = "PRJ-100",
                        ["Rhodium.Funding.ProjectElement"] = "ELM-200"
                    }
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(workItemsResponse));

        var results = (await _client.GetWorkItemsAsync("TestProject")).ToList();

        Assert.HasCount(1, results);
        Assert.AreEqual(13, results[0].Effort);
        Assert.AreEqual(8, results[0].StoryPoints);
        Assert.AreEqual(21, results[0].BusinessValue);
        Assert.AreEqual(72.5d, results[0].TimeCriticality!.Value, 0.001d);
        Assert.AreEqual("PRJ-100", results[0].ProjectNumber);
        Assert.AreEqual("ELM-200", results[0].ProjectElement);
    }

    [TestMethod]
    public async Task GetWorkItemsByRootIdsAsync_DoesNotFallbackStoryPointsIntoEffort()
    {
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        var recursiveHierarchyResponse = new
        {
            workItemRelations = Array.Empty<object>()
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(recursiveHierarchyResponse));

        var fieldsResponse = new
        {
            count = 1,
            value = new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 501,
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["System.WorkItemType"] = "Product Backlog Item",
                        ["System.Title"] = "Hierarchy estimate fields",
                        ["System.State"] = "Active",
                        ["System.AreaPath"] = "TestProject",
                        ["System.IterationPath"] = "Sprint 1",
                        ["Microsoft.VSTS.Scheduling.StoryPoints"] = 3,
                        ["Microsoft.VSTS.Common.BusinessValue"] = 5
                    }
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(fieldsResponse));

        var results = (await _client.GetWorkItemsByRootIdsAsync(new[] { 501 })).ToList();

        Assert.HasCount(1, results);
        Assert.IsNull(results[0].Effort);
        Assert.AreEqual(3, results[0].StoryPoints);
        Assert.AreEqual(5, results[0].BusinessValue);
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
                // Use modulo to wrap around or clamp to last response if index exceeds list size
                var index = Math.Min(_responseIndex, _responses.Count - 1);
                var (code, responseContent) = _responses[index];
                _responseIndex++;
                return new HttpResponseMessage
                {
                    StatusCode = code,
                    Content = new StringContent(responseContent)
                };
            });
    }

    /// <summary>
    /// Helper to create a batch of mock work items for testing.
    /// </summary>
    private static Dictionary<string, object>[] CreateMockWorkItemBatch(int startId, int count)
    {
        return Enumerable.Range(startId, count)
            .Select(i => new Dictionary<string, object>
            {
                ["id"] = i,
                ["url"] = $"http://test.com/{i}",
                ["fields"] = new Dictionary<string, object>
                {
                    ["System.WorkItemType"] = "Task",
                    ["System.Title"] = $"Task {i}",
                    ["System.State"] = "Active",
                    ["System.AreaPath"] = "TestProject",
                    ["System.IterationPath"] = "TestProject"
                }
            })
            .ToArray();
    }
}
