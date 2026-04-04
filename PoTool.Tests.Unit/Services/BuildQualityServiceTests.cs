using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class BuildQualityServiceTests
{
    [TestMethod]
    public async Task GetRollingWindowAsync_NotReadyEnvelope_ReturnsNotReadyState()
    {
        var expectedPath = "/api/buildquality/rolling";
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.AreEqual(expectedPath, request.RequestUri?.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "state": 4,
                      "reason": "Cache not built"
                    }
                    """, Encoding.UTF8, "application/json")
            };
        }))
        {
            BaseAddress = new Uri("http://localhost")
        };

        var service = new BuildQualityService(httpClient);

        var result = await service.GetRollingWindowAsync(
            7,
            DateTimeOffset.Parse("2026-04-01T00:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-04-02T00:00:00Z", CultureInfo.InvariantCulture));

        Assert.AreEqual(CacheBackedClientState.NotReady, result.State);
        Assert.IsNull(result.Data);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
