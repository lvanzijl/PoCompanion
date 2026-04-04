using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.DataState;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public class DataStateHttpClientHelperTests
{
    [TestMethod]
    public async Task GetDataStateAsync_ValidEnvelope_UnwrapsSuccess()
    {
        var client = CreateHttpClient("""
            {
              "state": 2,
              "data": { "value": 42 }
            }
            """);

        var result = await DataStateHttpClientHelper.GetDataStateAsync<TestPayload>(client, "api/test");

        Assert.AreEqual(CacheBackedClientState.Success, result.State);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(42, result.Data.Value);
    }

    [TestMethod]
    public async Task GetDataStateAsync_NotReadyEnvelope_ReturnsNotReady()
    {
        var client = CreateHttpClient("""
            {
              "state": 4,
              "reason": "Cache not built",
              "retryAfterSeconds": 30
            }
            """);

        var result = await DataStateHttpClientHelper.GetDataStateAsync<TestPayload>(client, "api/test");

        Assert.AreEqual(CacheBackedClientState.NotReady, result.State);
        Assert.AreEqual("Cache not built", result.Reason);
        Assert.AreEqual(30, result.RetryAfterSeconds);
        Assert.IsNull(result.Data);
    }

    [TestMethod]
    public async Task GetDataStateAsync_MalformedJson_DoesNotReturnSuccess()
    {
        var client = CreateHttpClient("{ this is not json");

        var result = await DataStateHttpClientHelper.GetDataStateAsync<TestPayload>(client, "api/test");

        Assert.AreNotEqual(CacheBackedClientState.Success, result.State);
        Assert.AreEqual(CacheBackedClientState.Failed, result.State);
        StringAssert.Contains(result.Reason ?? string.Empty, "malformed JSON");
    }

    private static HttpClient CreateHttpClient(string content)
    {
        return new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    private sealed record TestPayload(int Value);

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
