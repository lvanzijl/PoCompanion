using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class WorkItemHierarchyBacklogPriorityTests
{
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private TfsConfigurationService _configService = null!;
    private PoToolDbContext _dbContext = null!;
    private RealTfsClient _client = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        var dbOptions = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"HierarchyPriority_{Guid.NewGuid()}")
            .Options;

        _dbContext = new PoToolDbContext(dbOptions);
        _configService = new TfsConfigurationService(
            _dbContext,
            Mock.Of<ILogger<TfsConfigurationService>>(),
            new EfConcurrencyGate(Mock.Of<ILogger<EfConcurrencyGate>>()));

        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Area",
            true);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _client = new RealTfsClient(
            factory.Object,
            _configService,
            Mock.Of<ILogger<RealTfsClient>>(),
            new TfsRequestThrottler(Mock.Of<ILogger<TfsRequestThrottler>>(), readConcurrency: 4, writeConcurrency: 4),
            new TfsRequestSender(Mock.Of<ILogger<TfsRequestSender>>()));
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task GetWorkItemsByRootIdsAsync_MapsBacklogPriorityFromHierarchyFields()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(CreateJsonResponse(new
        {
            queryType = "WorkItemLink",
            queryResultType = "workItemLink",
            workItemRelations = new object[]
            {
                new
                {
                    rel = "System.LinkTypes.Hierarchy-Forward",
                    source = new { id = 200 },
                    target = new { id = 300 }
                }
            }
        }));

        responses.Enqueue(CreateJsonResponse(new
        {
            count = 2,
            value = new object[]
            {
                CreateHierarchyWorkItem(200, "Objective", "Objective 1", 12345d),
                CreateHierarchyWorkItem(300, "Epic", "Epic 1", 23456d)
            }
        }));

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => responses.Dequeue());

        var results = (await _client.GetWorkItemsByRootIdsAsync(new[] { 200 })).ToList();

        Assert.HasCount(2, results);
        Assert.AreEqual(12345d, results.Single(item => item.TfsId == 200).BacklogPriority);
        Assert.AreEqual(23456d, results.Single(item => item.TfsId == 300).BacklogPriority);
    }

    private static HttpResponseMessage CreateJsonResponse(object payload) =>
        new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(payload))
        };

    private static Dictionary<string, object> CreateHierarchyWorkItem(int id, string type, string title, double backlogPriority)
    {
        return new Dictionary<string, object>
        {
            ["id"] = id,
            ["fields"] = new Dictionary<string, object>
            {
                ["System.WorkItemType"] = type,
                ["System.Title"] = title,
                ["System.State"] = "Active",
                ["System.AreaPath"] = "TestProject",
                ["System.IterationPath"] = "TestProject",
                ["Microsoft.VSTS.Common.BacklogPriority"] = backlogPriority
            }
        };
    }
}
