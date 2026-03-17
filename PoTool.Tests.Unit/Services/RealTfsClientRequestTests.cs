using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class RealTfsClientRequestTests
{
    [TestMethod]
    public async Task GetAreaPathsAsync_UsesConfiguredTimeout()
    {
        var config = CreateConfig(timeoutSeconds: 0);
        var handler = new DelayedHttpMessageHandler(TimeSpan.FromSeconds(5));
        var client = CreateClient(config, handler);

        await AssertThrowsAsync<TimeoutException>(() => client.GetAreaPathsAsync());
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_UsesUtcForChangedDateFilter()
    {
        var config = CreateConfig();
        string? capturedBody = null;

        var handler = new CaptureHttpMessageHandler(async (request, ct) =>
        {
            capturedBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"workItems\": []}")
            };
        });

        var client = CreateClient(config, handler);
        var since = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.FromHours(2));

        var results = await client.GetWorkItemsAsync("TestProject\\Area", since);

        Assert.IsNotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var query = doc.RootElement.GetProperty("query").GetString();
        var expected = since.ToUniversalTime().ToString("O");
        StringAssert.Contains(query, expected);
        Assert.IsFalse(results.Any());
    }

    [TestMethod]
    public async Task GetWorkItemsByTypeAsync_UsesTypeFilteredWiqlAndBatchRequest()
    {
        var config = CreateConfig();
        var requestBodies = new List<string>();

        var handler = new CaptureHttpMessageHandler(async (request, ct) =>
        {
            if (request.Content != null)
            {
                requestBodies.Add(await request.Content.ReadAsStringAsync(ct));
            }

            if (request.RequestUri!.AbsoluteUri.Contains("_apis/wit/wiql", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"workItems\":[{\"id\":101},{\"id\":102}]}")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":[{\"id\":101,\"fields\":{\"System.Title\":\"Goal A\",\"System.WorkItemType\":\"goal\"}},{\"id\":102,\"fields\":{\"System.Title\":\"Goal B\",\"System.WorkItemType\":\"goal\"}}]}")
            };
        });

        var client = CreateClient(config, handler);

        var results = (await client.GetWorkItemsByTypeAsync("goal", "TestProject\\Area")).ToList();

        Assert.HasCount(2, requestBodies);

        using (var wiqlDoc = JsonDocument.Parse(requestBodies[0]))
        {
            var query = wiqlDoc.RootElement.GetProperty("query").GetString();
            StringAssert.Contains(query, "[System.WorkItemType] = 'goal'");
            StringAssert.Contains(query, "[System.AreaPath] UNDER 'TestProject\\Area'");
        }

        using (var batchDoc = JsonDocument.Parse(requestBodies[1]))
        {
            var fields = batchDoc.RootElement.GetProperty("fields").EnumerateArray().Select(element => element.GetString()).ToList();
            CollectionAssert.AreEquivalent(
                new[] { "System.Id", "System.Title", "System.WorkItemType" },
                fields.Where(value => value != null).Cast<string>().ToList());
        }

        Assert.AreEqual("Goal A", results[0].Title);
        Assert.AreEqual("Goal B", results[1].Title);
    }

    [TestMethod]
    public async Task GetPullRequestsAsync_UsesUtcForMinAndMaxTime()
    {
        var config = CreateConfig();
        string? capturedQuery = null;

        var handler = new CaptureHttpMessageHandler((request, _) =>
        {
            var uri = request.RequestUri!.ToString();
            if (uri.Contains("_apis/git/repositories", StringComparison.OrdinalIgnoreCase) &&
                !uri.Contains("pullrequests", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\": [{\"name\": \"Repo1\", \"id\": \"1\"}]}")
                });
            }

            if (uri.Contains("pullrequests", StringComparison.OrdinalIgnoreCase))
            {
                capturedQuery = request.RequestUri!.Query;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\": []}")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}")
            });
        });

        var client = CreateClient(config, handler);
        var from = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.FromHours(2));
        var to = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));

        await client.GetPullRequestsAsync(null, from, to);

        Assert.IsNotNull(capturedQuery);
        var expectedMin = Uri.EscapeDataString(from.ToUniversalTime().ToString("O"));
        var expectedMax = Uri.EscapeDataString(to.ToUniversalTime().ToString("O"));
        StringAssert.Contains(capturedQuery, $"searchCriteria.minTime={expectedMin}");
        StringAssert.Contains(capturedQuery, $"searchCriteria.maxTime={expectedMax}");
    }

    private static RealTfsClient CreateClient(TfsConfigEntity config, HttpMessageHandler handler)
    {
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

    private static TfsConfigEntity CreateConfig(int timeoutSeconds = 30)
    {
        return new TfsConfigEntity
        {
            Url = "https://dev.azure.com/testorg",
            Project = "TestProject",
            ApiVersion = "7.0",
            TimeoutSeconds = timeoutSeconds,
            UseDefaultCredentials = true,
            DefaultAreaPath = "TestProject\\Area"
        };
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }

        Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
        return null!;
    }

    private sealed class DelayedHttpMessageHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        public DelayedHttpMessageHandler(TimeSpan delay)
        {
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\": []}")
            };
        }
    }

    private sealed class CaptureHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public CaptureHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
