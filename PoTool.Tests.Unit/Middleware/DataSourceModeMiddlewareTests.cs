using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Exceptions;
using PoTool.Api.Middleware;
using PoTool.Core.Configuration;

namespace PoTool.Tests.Unit.Middleware;

/// <summary>
/// Unit tests for DataSourceModeMiddleware.
/// Verifies that the middleware correctly sets Cache or Live mode based on route classification.
/// </summary>
[TestClass]
public class DataSourceModeMiddlewareTests
{
    private Mock<IDataSourceModeProvider> _mockModeProvider = null!;
    private Mock<ILogger<DataSourceModeMiddleware>> _mockLogger = null!;
    private readonly List<MemoryStream> _responseBodies = [];
    private bool _nextCalled;

    [TestInitialize]
    public void Initialize()
    {
        _mockModeProvider = new Mock<IDataSourceModeProvider>();
        _mockLogger = new Mock<ILogger<DataSourceModeMiddleware>>();
        _nextCalled = false;
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var responseBody in _responseBodies)
        {
            responseBody.Dispose();
        }

        _responseBodies.Clear();
    }

    [TestMethod]
    public async Task InvokeAsync_CacheOnlyRoute_SetsCacheMode()
    {
        var context = CreateHttpContext("/api/workitems");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_CacheStateAwareRoute_SetsCacheModeWithoutBlocking()
    {
        var context = CreateHttpContext("/api/workitems/validation-queue");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_SettingsRoute_SetsLiveMode()
    {
        var context = CreateHttpContext("/api/settings");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_StartupStateRoute_SetsLiveMode()
    {
        var context = CreateHttpContext("/api/startup-state");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_OnboardingRoute_SetsLiveMode()
    {
        var context = CreateHttpContext("/api/onboarding/status");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_RootPath_BypassesClassificationAndCallsNext()
    {
        var context = CreateHttpContext("/");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(It.IsAny<DataSourceMode>()), Times.Never);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_HubRoute_SetsLiveMode()
    {
        var context = CreateHttpContext("/hubs/cachesync");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_LiveAllowedDiscoveryRoute_UnderWorkspacePrefix_SetsLiveMode()
    {
        var context = CreateHttpContext("/api/workitems/area-paths/from-tfs");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_WorkItemParameterizedLiveRoute_SetsLiveMode()
    {
        var context = CreateHttpContext("/api/workitems/123/revisions");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_WorkItemStateTimelineRoute_ThrowsNotSupported()
    {
        var context = CreateHttpContext("/api/workitems/123/state-timeline");
        var middleware = CreateMiddleware();

        await Assert.ThrowsExactlyAsync<NotSupportedException>(
            async () => await middleware.InvokeAsync(context, _mockModeProvider.Object));

        _mockModeProvider.Verify(p => p.SetCurrentMode(It.IsAny<DataSourceMode>()), Times.Never);
        Assert.IsFalse(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_PipelineDefinitionsDiscoveryRoute_SetsLiveMode()
    {
        var context = CreateHttpContext("/api/pipelines/definitions");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_ReleasePlanningRoute_WithCache_SetsCacheMode()
    {
        var context = CreateHttpContext("/api/releaseplanning/objectives");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_MetricsRoute_WithCache_SetsCacheMode()
    {
        var context = CreateHttpContext("/api/metrics/multi-iteration-health");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_BuildQualityRoute_WithCache_SetsCacheMode()
    {
        var context = CreateHttpContext("/api/buildquality/rolling");
        var middleware = CreateMiddleware();

        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_UnknownRoute_ThrowsRouteNotClassifiedException()
    {
        var context = CreateHttpContext("/api/unclassified-route");
        var middleware = CreateMiddleware();

        await Assert.ThrowsExactlyAsync<RouteNotClassifiedException>(
            async () => await middleware.InvokeAsync(context, _mockModeProvider.Object));

        _mockModeProvider.Verify(p => p.SetCurrentMode(It.IsAny<DataSourceMode>()), Times.Never);
        Assert.IsFalse(_nextCalled);
    }

    private DataSourceModeMiddleware CreateMiddleware()
    {
        return new DataSourceModeMiddleware(
            next: _ =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            },
            logger: _mockLogger.Object);
    }

    private DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var responseBody = new MemoryStream();
        _responseBodies.Add(responseBody);
        context.Response.Body = responseBody;
        return context;
    }
}
