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
        var page = await client.GetRevisionsAsync(
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            scopedWorkItemIds: [10, 11]);

        Assert.HasCount(2, page.Revisions);
        Assert.AreEqual("https://analytics/page2", page.ContinuationToken);
        var firstRequest = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(firstRequest, "$orderby=ChangedDate asc,WorkItemId asc,Revision asc");
        StringAssert.Contains(firstRequest, "ChangedDate ge '2026-01-01T00:00:00.0000000Z'");
        StringAssert.Contains(firstRequest, "WorkItemId ge 10 and WorkItemId le 11");
        StringAssert.Contains(firstRequest, "$select=WorkItemId,Revision,ChangedDate,WorkItemType,Title,State,Reason,IterationPath,AreaPath,CreatedDate,ClosedDate,Effort,Tags,Severity,ChangedBy");
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

    [TestMethod]
    public async Task GetRevisionsAsync_ParsesDotStyleFieldAliases()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "System.Id": 7, "System.Rev": 3, "System.ChangedDate": "2026-01-03T00:00:00Z", "System.Title": "Alias", "System.WorkItemType": "Task", "System.State": "Active", "System.IterationPath": "I", "System.AreaPath": "A" }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var page = await client.GetRevisionsAsync();

        Assert.HasCount(1, page.Revisions);
        Assert.AreEqual(7, page.Revisions[0].WorkItemId);
        Assert.AreEqual(3, page.Revisions[0].RevisionNumber);
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenScopeModeIsIdList_UsesIdListFilter()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 7, "Revision": 1, "ChangedDate": "2026-01-03T00:00:00Z", "Title": "Alias", "WorkItemType": "Task", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);

        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            MaxTotalPages = 10,
            MaxTotalRows = 100,
            MaxEmptyPages = 2,
            ODataScopeMode = ODataRevisionScopeMode.IdList,
            ODataMaxUrlLength = 4000
        });

        _ = await client.GetRevisionsAsync(scopedWorkItemIds: [7, 9, 11]);

        StringAssert.Contains(Uri.UnescapeDataString(handler.RequestUris[0]), "(WorkItemId eq 7 or WorkItemId eq 9 or WorkItemId eq 11)");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenQuotedDateLiteralsDisabled_UsesCompatibilityLiteral()
    {
        var handler = new QueueMessageHandler(["""{ "value": [] }"""]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataUseQuotedDateLiterals = false
        });

        _ = await client.GetRevisionsAsync(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(request, "ChangedDate ge 2026-01-01T00:00:00.0000000Z");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenFullSelectAndOrderByDisabled_OmitsProjectionAndOrder()
    {
        var handler = new QueueMessageHandler(["""{ "value": [] }"""]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataSelectMode = ODataRevisionSelectMode.Full,
            ODataOrderByEnabled = false
        });

        _ = await client.GetRevisionsAsync();

        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        Assert.IsFalse(request.Contains("$select=", StringComparison.Ordinal));
        Assert.IsFalse(request.Contains("$orderby=", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenNextLinkMissingAndPageIsFull_UsesSeekContinuationFallback()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" },
                { "WorkItemId": 42, "Revision": 2, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """,
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 3, "ChangedDate": "2026-01-03T00:00:00Z", "Title": "C", "WorkItemType": "Bug", "State": "Closed", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);

        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            MaxTotalPages = 10,
            MaxTotalRows = 100,
            MaxEmptyPages = 2,
            ODataTop = 2,
            ODataEnableSeekPagingFallback = true
        });

        var first = await client.GetRevisionsAsync(
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            scopedWorkItemIds: [42]);
        var second = await client.GetRevisionsAsync(
            continuationToken: first.ContinuationToken,
            scopedWorkItemIds: [42]);

        Assert.IsNotNull(first.ContinuationToken);
        StringAssert.StartsWith(first.ContinuationToken, "seek:");
        Assert.IsNull(second.ContinuationToken);
        var secondRequest = Uri.UnescapeDataString(handler.RequestUris[1]);
        StringAssert.Contains(secondRequest, "ChangedDate gt '2026-01-02T00:00:00.0000000Z'");
        StringAssert.Contains(secondRequest, "WorkItemId eq 42 and Revision gt 2");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_SeekFallbackDetectsNoProgressAndThrows()
    {
        var repeatedPage = """
                           {
                             "value": [
                               { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" },
                               { "WorkItemId": 42, "Revision": 2, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
                             ]
                           }
                           """;
        var handler = new QueueMessageHandler([repeatedPage, repeatedPage]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataTop = 2,
            ODataSeekPageSize = 2,
            ODataEnableSeekPagingFallback = true,
            MaxNoProgressPages = 1
        });

        var first = await client.GetRevisionsAsync(scopedWorkItemIds: [42]);
        var exception = await AssertThrowsAsync<InvalidOperationException>(() =>
            client.GetRevisionsAsync(continuationToken: first.ContinuationToken, scopedWorkItemIds: [42]));

        StringAssert.Contains(exception.Message, "no progress");
    }

    [TestMethod]
    public async Task GetWorkItemRevisionsAsync_UsesServerSideEqFilter()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);
        var client = CreateClient(handler);

        var revisions = await client.GetWorkItemRevisionsAsync(42);

        Assert.HasCount(1, revisions);
        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(request, "WorkItemId eq 42");
        Assert.IsFalse(request.Contains("WorkItemId ge 42 and WorkItemId le 42", StringComparison.Ordinal));
    }

    private static RealODataRevisionTfsClient CreateClient(
        QueueMessageHandler handler,
        RevisionIngestionPaginationOptions? options = null)
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
        paginationOptions.Setup(current => current.CurrentValue).Returns(options ?? new RevisionIngestionPaginationOptions
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

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
            throw new InvalidOperationException("Unreachable");
        }
        catch (TException ex)
        {
            return ex;
        }
    }
}
