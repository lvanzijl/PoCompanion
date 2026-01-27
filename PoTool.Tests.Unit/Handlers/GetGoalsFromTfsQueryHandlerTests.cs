using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetGoalsFromTfsQueryHandlerTests
{
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private HttpClient _httpClient = null!;
    private PoToolDbContext _dbContext = null!;
    private TfsConfigurationService _configService = null!;
    private Mock<ILogger<GetGoalsFromTfsQueryHandler>> _loggerMock = null!;
    private GetGoalsFromTfsQueryHandler _handler = null!;

    [TestInitialize]
    public async Task Setup()
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
        var gateMock = new Mock<IEfConcurrencyGate>();
        gateMock.Setup(g => g.ExecuteAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((func, ct) => func());
        gateMock.Setup(g => g.ExecuteAsync<It.IsAnyType>(It.IsAny<Func<Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<It.IsAnyType>>, CancellationToken>((func, ct) => func());
        _configService = new TfsConfigurationService(_dbContext, configLogger.Object, gateMock.Object);

        // Set up TFS configuration
        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        _loggerMock = new Mock<ILogger<GetGoalsFromTfsQueryHandler>>();

        // Create mock IHttpClientFactory
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _handler = new GetGoalsFromTfsQueryHandler(
            mockFactory.Object,
            _configService,
            _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        _httpClient.Dispose();
    }

    [TestMethod]
    public async Task Handle_WithNoGoals_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetGoalsFromTfsQuery();

        // Mock WIQL response with no results
        var wiqlResponse = new
        {
            workItems = Array.Empty<object>()
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_WithMultipleGoals_ReturnsAll()
    {
        // Arrange
        var query = new GetGoalsFromTfsQuery();

        // Mock WIQL response with goal IDs
        var wiqlResponse = new
        {
            workItems = new[]
            {
                new { id = 100 },
                new { id = 200 },
                new { id = 300 }
            }
        };

        // Mock batch work items response
        var batchResponse = new
        {
            value = new[]
            {
                new
                {
                    id = 100,
                    fields = new Dictionary<string, object>
                    {
                        ["System.Id"] = 100,
                        ["System.Title"] = "Goal Alpha",
                        ["System.WorkItemType"] = WorkItemType.Goal
                    }
                },
                new
                {
                    id = 200,
                    fields = new Dictionary<string, object>
                    {
                        ["System.Id"] = 200,
                        ["System.Title"] = "Goal Beta",
                        ["System.WorkItemType"] = WorkItemType.Goal
                    }
                },
                new
                {
                    id = 300,
                    fields = new Dictionary<string, object>
                    {
                        ["System.Id"] = 300,
                        ["System.Title"] = "Goal Gamma",
                        ["System.WorkItemType"] = WorkItemType.Goal
                    }
                }
            }
        };

        // Setup sequence of responses: first WIQL, then batch
        SetupHttpResponseSequence(
            (HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse)),
            (HttpStatusCode.OK, JsonSerializer.Serialize(batchResponse)));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var goalsList = result.ToList();
        Assert.HasCount(3, goalsList);
        Assert.AreEqual("Goal Alpha", goalsList[0].Title);
        Assert.AreEqual(100, goalsList[0].TfsId);
        Assert.AreEqual("Goal Beta", goalsList[1].Title);
        Assert.AreEqual(200, goalsList[1].TfsId);
        Assert.AreEqual("Goal Gamma", goalsList[2].Title);
        Assert.AreEqual(300, goalsList[2].TfsId);
    }

    [TestMethod]
    public async Task Handle_WhenWiqlFails_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetGoalsFromTfsQuery();

        // Mock WIQL response failure (e.g., work item type not found)
        SetupHttpResponse(HttpStatusCode.BadRequest, $"Work item type '{WorkItemType.Goal}' does not exist");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - should return empty list instead of throwing
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_WhenNoConfiguration_ReturnsEmptyList()
    {
        // Arrange
        // Clear the database to simulate no configuration
        _dbContext.Database.EnsureDeleted();
        _dbContext.Database.EnsureCreated();

        var query = new GetGoalsFromTfsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_WhenBatchFetchFails_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetGoalsFromTfsQuery();

        // Mock successful WIQL response, then batch fetch failure
        var wiqlResponse = new
        {
            workItems = new[]
            {
                new { id = 100 }
            }
        };

        SetupHttpResponseSequence(
            (HttpStatusCode.OK, JsonSerializer.Serialize(wiqlResponse)),
            (HttpStatusCode.InternalServerError, "Server error"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - should return empty list instead of throwing
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    private void SetupHttpResponseSequence(params (HttpStatusCode statusCode, string content)[] responses)
    {
        var sequence = _httpMessageHandlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        foreach (var (statusCode, content) in responses)
        {
            sequence = sequence.ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
        }
    }
}
