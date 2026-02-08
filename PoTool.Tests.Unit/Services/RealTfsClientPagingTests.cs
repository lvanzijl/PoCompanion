using System.Net;
using System.Net.Http;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class RealTfsClientPagingTests
{
    [TestMethod]
    public async Task GetGitRepositoriesAsync_AccumulatesPagedResults()
    {
        var responses = new Queue<HttpResponseMessage>();
        var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"value\": [{\"name\": \"Repo1\", \"id\": \"1\"}]}")
        };
        firstResponse.Headers.Add("x-ms-continuationtoken", "token-1");
        responses.Enqueue(firstResponse);

        responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"value\": [{\"name\": \"Repo2\", \"id\": \"2\"}]}")
        });

        var handler = new QueueHttpMessageHandler(responses);
        var client = CreateClient(handler);

        var repositories = (await client.GetGitRepositoriesAsync()).ToList();

        Assert.HasCount(2, repositories);
        Assert.IsGreaterThanOrEqualTo(handler.Requests.Count, 2);
        Assert.IsTrue(handler.Requests[1].Query.Contains("continuationToken=", StringComparison.Ordinal));
    }

    private static RealTfsClient CreateClient(HttpMessageHandler handler)
    {
        var config = new TfsConfigEntity
        {
            Url = "https://dev.azure.com/testorg",
            Project = "TestProject",
            ApiVersion = "7.0",
            TimeoutSeconds = 30,
            UseDefaultCredentials = true,
            DefaultAreaPath = "TestProject\\Area"
        };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var configService = new Mock<ITfsConfigurationService>();
        configService.Setup(cs => cs.GetConfigEntityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var logger = new Mock<ILogger<RealTfsClient>>();
        var throttlerLogger = new Mock<ILogger<TfsRequestThrottler>>();
        var throttler = new TfsRequestThrottler(throttlerLogger.Object);
        var senderLogger = new Mock<ILogger<TfsRequestSender>>();
        var requestSender = new TfsRequestSender(senderLogger.Object);

        return new RealTfsClient(
            mockFactory.Object,
            configService.Object,
            logger.Object,
            throttler,
            requestSender);
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<Uri> Requests { get; } = new();

        public QueueHttpMessageHandler(Queue<HttpResponseMessage> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
