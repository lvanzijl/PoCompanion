using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Contracts;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Tests to verify that work item hierarchy retrieval correctly fetches ancestors (missing parents)
/// to complete the hierarchy graph.
/// </summary>
[TestClass]
public class WorkItemAncestorCompletionTests
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

        var configLogger = new Mock<ILogger<TfsConfigurationService>>();
        var gateMock = new Mock<IEfConcurrencyGate>();
        gateMock.Setup(g => g.ExecuteAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((func, ct) => func());
        gateMock.Setup(g => g.ExecuteAsync<It.IsAnyType>(It.IsAny<Func<Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<It.IsAnyType>>, CancellationToken>((func, ct) => func());
        _configService = new TfsConfigurationService(_dbContext, configLogger.Object, gateMock.Object);

        _loggerMock = new Mock<ILogger<RealTfsClient>>();

        // Create throttler and request sender
        var throttlerLogger = new Mock<ILogger<TfsRequestThrottler>>();
        var throttler = new TfsRequestThrottler(throttlerLogger.Object, readConcurrency: 10, writeConcurrency: 10);
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

    /// <summary>
    /// Tests that ancestor completion correctly fetches missing parents.
    /// 
    /// Scenario:
    /// - Root ID: 300 (Epic)
    /// - Child: 400 (Feature, parent=300)
    /// - Epic 300 has parent 200 (Objective) which is NOT in the initial set
    /// - Objective 200 has parent 100 (Goal) which is also NOT in the initial set
    /// 
    /// Expected:
    /// - Starting from root 300, descendants (400) are discovered
    /// - Ancestors (200, 100) are fetched to complete the hierarchy
    /// - Final result includes: 100, 200, 300, 400 (complete chain)
    /// </summary>
    [TestMethod]
    public async Task GetWorkItemsByRootIdsAsync_CompletesAncestors_WhenRootHasParents()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        var responses = new Queue<(HttpStatusCode status, string content)>();

        // Response 1: WIQL query for descendants of Epic 300
        // Returns Feature 400 as child
        responses.Enqueue((HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            queryType = "WorkItemLink",
            queryResultType = "workItemLink",
            workItemRelations = new[]
            {
                new { source = new { id = 300 }, target = new { id = 400 } }
            }
        })));

        // Response 2: WIQL query for descendants of Feature 400 (has no children)
        responses.Enqueue((HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            queryType = "WorkItemLink",
            queryResultType = "workItemLink",
            workItemRelations = Array.Empty<object>()
        })));

        // Response 3: Phase 1 - Fetch relations for Epic 300 and Feature 400
        // Epic 300 has parent Objective 200
        // Feature 400 has parent Epic 300
        responses.Enqueue((HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            count = 2,
            value = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 300,
                    ["relations"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["rel"] = "System.LinkTypes.Hierarchy-Reverse",
                            ["url"] = "https://dev.azure.com/testorg/_apis/wit/workItems/200"
                        }
                    }
                },
                new Dictionary<string, object>
                {
                    ["id"] = 400,
                    ["relations"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["rel"] = "System.LinkTypes.Hierarchy-Reverse",
                            ["url"] = "https://dev.azure.com/testorg/_apis/wit/workItems/300"
                        }
                    }
                }
            }
        })));

        // Response 4: Phase 1.5 - Fetch relations for missing parent Objective 200
        // Objective 200 has parent Goal 100
        responses.Enqueue((HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            count = 1,
            value = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 200,
                    ["relations"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["rel"] = "System.LinkTypes.Hierarchy-Reverse",
                            ["url"] = "https://dev.azure.com/testorg/_apis/wit/workItems/100"
                        }
                    }
                }
            }
        })));

        // Response 5: Phase 1.5 - Fetch relations for missing parent Goal 100
        // Goal 100 has no parent (top of hierarchy)
        responses.Enqueue((HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            count = 1,
            value = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 100,
                    ["relations"] = Array.Empty<object>()
                }
            }
        })));

        // Response 6: Phase 2 - Fetch fields for all items (100, 200, 300, 400)
        responses.Enqueue((HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            count = 4,
            value = new[]
            {
                CreateWorkItem(100, "Goal", "Goal 1", "TestProject"),
                CreateWorkItem(200, "Objective", "Objective 1", "TestProject"),
                CreateWorkItem(300, "Epic", "Epic 1", "TestProject"),
                CreateWorkItem(400, "Feature", "Feature 1", "TestProject")
            }
        })));

        SetupMockHttpResponses(responses);

        // Act
        var results = (await _client.GetWorkItemsByRootIdsAsync(
            new[] { 300 }, // Start from Epic 300
            since: null,
            progressCallback: null,
            cancellationToken: default)).ToList();

        // Assert
        Assert.HasCount(4, results, "Should fetch all 4 items: Goal, Objective, Epic, Feature");

        var resultIds = results.Select(wi => wi.TfsId).OrderBy(id => id).ToList();
        CollectionAssert.AreEqual(new[] { 100, 200, 300, 400 }, resultIds,
            "Should include all items in the hierarchy chain");

        // Verify hierarchy is correctly connected
        var goal = results.First(w => w.TfsId == 100);
        var objective = results.First(w => w.TfsId == 200);
        var epic = results.First(w => w.TfsId == 300);
        var feature = results.First(w => w.TfsId == 400);

        Assert.IsNull(goal.ParentTfsId, "Goal should have no parent");
        Assert.AreEqual(100, objective.ParentTfsId, "Objective should have Goal as parent");
        Assert.AreEqual(200, epic.ParentTfsId, "Epic should have Objective as parent");
        Assert.AreEqual(300, feature.ParentTfsId, "Feature should have Epic as parent");
    }

    /// <summary>
    /// Tests that Phase 1 handles items with missing 'relations' property gracefully.
    /// </summary>
    [TestMethod]
    public async Task GetWorkItemsByRootIdsAsync_HandlesItemsWithMissingRelations()
    {
        // Arrange
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        var responses = new Queue<(HttpStatusCode status, string content)>();

        // Response 1: WIQL query for descendants (no children)
        responses.Enqueue((HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            queryType = "WorkItemLink",
            queryResultType = "workItemLink",
            workItemRelations = Array.Empty<object>()
        })));

        // Response 2: Phase 1 - Fetch relations, but item 500 is missing 'relations' property
        responses.Enqueue((HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            count = 1,
            value = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = 500,
                    ["url"] = "https://dev.azure.com/testorg/_apis/wit/workItems/500"
                    // NOTE: No 'relations' property
                }
            }
        })));

        // Response 3: Phase 2 - Fetch fields
        responses.Enqueue((HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            count = 1,
            value = new[]
            {
                CreateWorkItem(500, "Task", "Task 1", "TestProject")
            }
        })));

        SetupMockHttpResponses(responses);

        // Act
        var results = (await _client.GetWorkItemsByRootIdsAsync(
            new[] { 500 },
            since: null,
            progressCallback: null,
            cancellationToken: default)).ToList();

        // Assert
        Assert.HasCount(1, results, "Should return the work item");
        Assert.AreEqual(500, results[0].TfsId);
        Assert.IsNull(results[0].ParentTfsId, "Item without relations should have null parent");

        // Verify diagnostic logging was called (check that no exception was thrown)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("missing 'relations' property")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Should log diagnostic info about missing relations");
    }

    private static Dictionary<string, object> CreateWorkItem(int id, string type, string title, string areaPath)
    {
        return new Dictionary<string, object>
        {
            ["id"] = id,
            ["url"] = $"https://dev.azure.com/testorg/_apis/wit/workItems/{id}",
            ["fields"] = new Dictionary<string, object>
            {
                ["System.WorkItemType"] = type,
                ["System.Title"] = title,
                ["System.State"] = "Active",
                ["System.AreaPath"] = areaPath,
                ["System.IterationPath"] = areaPath
            }
        };
    }

    private void SetupMockHttpResponses(Queue<(HttpStatusCode status, string content)> responses)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                if (responses.Count == 0)
                {
                    throw new InvalidOperationException("No more mocked responses available");
                }

                var (status, content) = responses.Dequeue();
                return new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
                };
            });
    }
}
