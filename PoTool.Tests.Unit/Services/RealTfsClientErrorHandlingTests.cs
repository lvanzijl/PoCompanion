using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Exceptions;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class RealTfsClientErrorHandlingTests
{
    private RealTfsClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StubHttpMessageHandler()));

        var configService = new Mock<ITfsConfigurationService>();
        var logger = new Mock<ILogger<RealTfsClient>>();
        var throttlerLogger = new Mock<ILogger<TfsRequestThrottler>>();
        var throttler = new TfsRequestThrottler(throttlerLogger.Object);
        var senderLogger = new Mock<ILogger<TfsRequestSender>>();
        var requestSender = new TfsRequestSender(senderLogger.Object);

        _client = new RealTfsClient(
            mockFactory.Object,
            configService.Object,
            logger.Object,
            throttler,
            requestSender);
    }

    [TestMethod]
    public async Task HandleHttpErrorsAsync_429_ThrowsRateLimitExceptionWithRetryAfter()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("rate limited"),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://tfs.example.com/_apis/projects")
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));

        var exception = await AssertThrowsAsync<TfsRateLimitException>(
            () => InvokeHandleHttpErrorsAsync(response));

        Assert.AreEqual(TimeSpan.FromSeconds(5), exception.RetryAfter);
    }

    [TestMethod]
    public async Task HandleHttpErrorsAsync_429_WithRetryAfterDate_UsesRemainingDelay()
    {
        var retryAt = DateTimeOffset.UtcNow.AddSeconds(30);
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("rate limited"),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://tfs.example.com/_apis/projects")
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAt);

        var exception = await AssertThrowsAsync<TfsRateLimitException>(
            () => InvokeHandleHttpErrorsAsync(response));

        Assert.IsNotNull(exception.RetryAfter);
        Assert.IsTrue(exception.RetryAfter.Value >= TimeSpan.FromSeconds(28));
        Assert.IsTrue(exception.RetryAfter.Value <= TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    public async Task HandleHttpErrorsAsync_500_ThrowsTfsExceptionWithStatusCode()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("server failure"),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://tfs.example.com/_apis/projects")
        };

        var exception = await AssertThrowsAsync<TfsException>(
            () => InvokeHandleHttpErrorsAsync(response));

        Assert.AreEqual(500, exception.StatusCode);
        Assert.AreEqual("server failure", exception.ErrorContent);
    }

    [TestMethod]
    public async Task ExecuteWithRetryAsync_RetriesOnServerError()
    {
        var attempts = 0;

        Func<Task<int>> operation = () =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new TfsException("Server error", 500, "failure");
            }

            return Task.FromResult(42);
        };

        var result = await InvokeExecuteWithRetryAsync(operation, maxRetries: 2);

        Assert.AreEqual(42, result);
        Assert.AreEqual(2, attempts);
    }

    private async Task InvokeHandleHttpErrorsAsync(HttpResponseMessage response)
    {
        var method = typeof(RealTfsClient).GetMethod(
            "HandleHttpErrorsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var task = (Task)method!.Invoke(_client, new object[] { response, CancellationToken.None })!;
        await task;
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

    private async Task<int> InvokeExecuteWithRetryAsync(Func<Task<int>> operation, int maxRetries)
    {
        var method = typeof(RealTfsClient).GetMethod(
            "ExecuteWithRetryAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var generic = method!.MakeGenericMethod(typeof(int));
        var task = (Task<int>)generic.Invoke(_client, new object[] { operation, CancellationToken.None, maxRetries, true })!;
        return await task;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
    }
}
