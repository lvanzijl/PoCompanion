using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class RealODataRevisionTfsClientTests
{
    [TestMethod]
    public async Task GetRevisionsAsync_ParsesNextLinkAndRows()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 10, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" },
                { "WorkItemId": 10, "Revision": 2, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ],
              "@odata.nextLink": "https://analytics/page2"
            }
            """
        ]);

        var client = CreateClient(handler);
        var page = await client.GetRevisionsAsync(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        Assert.HasCount(2, page.Revisions);
        Assert.AreEqual("https://analytics/page2", page.ContinuationToken);
    }

    [TestMethod]
    public async Task GetWorkItemRevisionsAsync_FollowsPagedNextLink()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ],
              "@odata.nextLink": "https://analytics/page2"
            }
            """,
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 2, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Closed", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var revisions = await client.GetWorkItemRevisionsAsync(42);

        Assert.HasCount(2, revisions);
        Assert.AreEqual("https://analytics/page2", handler.RequestUris[1]);
    }

    private static RealODataRevisionTfsClient CreateClient(QueueMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient("TfsClient.NTLM")).Returns(httpClient);

        var configService = new Mock<ITfsConfigurationService>();
        configService
            .Setup(service => service.GetConfigEntityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TfsConfigEntity
            {
                Url = "https://dev.azure.com/test",
                TimeoutSeconds = 30,
                RevisionSource = RevisionSource.AnalyticsODataRevisions,
                AnalyticsODataBaseUrl = "https://analytics",
                AnalyticsODataEntitySetPath = "WorkItemRevisions"
            });

        var paginationOptions = new Mock<IOptionsMonitor<RevisionIngestionPaginationOptions>>();
        paginationOptions.Setup(options => options.CurrentValue).Returns(new RevisionIngestionPaginationOptions
        {
            MaxTotalPages = 10,
            MaxTotalRows = 100,
            MaxEmptyPages = 2
        });

        return new RealODataRevisionTfsClient(
            httpClientFactory.Object,
            configService.Object,
            new TfsRequestSender(NullLogger<TfsRequestSender>.Instance),
            paginationOptions.Object,
            NullLogger<RealODataRevisionTfsClient>.Instance);
    }

    private sealed class QueueMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public QueueMessageHandler(IEnumerable<string> responses)
        {
            _responses = new Queue<string>(responses);
        }

        public List<string> RequestUris { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri?.ToString() ?? string.Empty);
            var payload = _responses.Count > 0 ? _responses.Dequeue() : "{ \"value\": [] }";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload)
            });
        }
    }
}
