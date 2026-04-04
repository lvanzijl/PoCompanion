using System.Net;
using System.Text;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class ReleaseNotesServiceTests
{
    [TestMethod]
    public async Task GetReleaseNotesAsync_ReturnsDeserializedEntries()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                [
                  {
                    "date": "2026-03-09",
                    "type": "Changed",
                    "title": "Release notes button on profiles landing",
                    "impact": "Profiles landing now exposes a What's New button for release notes.",
                    "area": "Home",
                    "link": "/profiles"
                  }
                ]
                """, Encoding.UTF8, "application/json")
        }, request => capturedRequest = request);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var service = new ReleaseNotesService(new SettingsClient(httpClient));

        var entries = await service.GetReleaseNotesAsync();

        Assert.HasCount(1, entries);
        Assert.AreEqual("Release notes button on profiles landing", entries[0].Title);
        Assert.AreEqual("/profiles", entries[0].Link);
        Assert.AreEqual("/api/Settings/release-notes", capturedRequest?.RequestUri?.AbsolutePath);
    }

    [TestMethod]
    public async Task GetReleaseNotesAsync_CachesEntriesAfterFirstRequest()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var service = new ReleaseNotesService(new SettingsClient(httpClient));

        await service.GetReleaseNotesAsync();
        await service.GetReleaseNotesAsync();

        Assert.AreEqual(1, requestCount);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
        private readonly Action<HttpRequestMessage>? _onRequest;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
            Action<HttpRequestMessage>? onRequest = null)
        {
            _responseFactory = responseFactory;
            _onRequest = onRequest;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onRequest?.Invoke(request);
            return Task.FromResult(_responseFactory(request));
        }
    }
}
