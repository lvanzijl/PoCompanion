using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Exceptions;
using PoTool.Api.Middleware;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;

namespace PoTool.Tests.Unit.Middleware;

/// <summary>
/// Unit tests for DataSourceModeMiddleware.
/// Verifies that the middleware correctly sets Cache or Live mode based on route and cache state.
/// </summary>
[TestClass]
public class DataSourceModeMiddlewareTests
{
    private Mock<IDataSourceModeProvider> _mockModeProvider = null!;
    private Mock<ICurrentProfileProvider> _mockProfileProvider = null!;
    private Mock<ILogger<DataSourceModeMiddleware>> _mockLogger = null!;
    private readonly List<MemoryStream> _responseBodies = [];
    private bool _nextCalled;

    [TestInitialize]
    public void Initialize()
    {
        _mockModeProvider = new Mock<IDataSourceModeProvider>();
        _mockProfileProvider = new Mock<ICurrentProfileProvider>();
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
    public async Task InvokeAsync_WorkspaceRoute_WithCache_SetsCacheMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/workitems");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockProfileProvider
            .Setup(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockModeProvider
            .Setup(p => p.GetModeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataSourceMode.Cache);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        Assert.IsTrue(_nextCalled, "Next middleware should be called");
    }

    [TestMethod]
    public async Task InvokeAsync_CacheOnlyRoute_WithoutCache_ReturnsConflictAndDoesNotCallNext()
    {
        // Arrange
        var context = CreateHttpContext("/api/pullrequests");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockProfileProvider
            .Setup(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockModeProvider
            .Setup(p => p.GetModeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataSourceMode.Live);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        Assert.AreEqual(StatusCodes.Status409Conflict, context.Response.StatusCode);
        _mockModeProvider.Verify(p => p.SetCurrentMode(It.IsAny<DataSourceMode>()), Times.Never);
        Assert.IsFalse(_nextCalled);
        var body = await ReadResponseBodyAsync(context);
        StringAssert.Contains(body, "Cache not ready");
    }

    [TestMethod]
    public async Task InvokeAsync_CacheOnlyRoute_NoActiveProfile_ReturnsConflictAndDoesNotCallNext()
    {
        // Arrange
        var context = CreateHttpContext("/api/pipelines");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockProfileProvider
            .Setup(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        Assert.AreEqual(StatusCodes.Status409Conflict, context.Response.StatusCode);
        _mockModeProvider.Verify(p => p.SetCurrentMode(It.IsAny<DataSourceMode>()), Times.Never);
        Assert.IsFalse(_nextCalled);
        var body = await ReadResponseBodyAsync(context);
        StringAssert.Contains(body, "active profile");
    }

    [TestMethod]
    public async Task InvokeAsync_CacheStateAwareRoute_SetsCacheModeWithoutBlocking()
    {
        var context = CreateHttpContext("/api/workitems/state/validation-queue");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        _mockProfileProvider.Verify(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_SettingsRoute_SetsLiveMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/settings");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        _mockProfileProvider.Verify(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()), Times.Never, 
            "Profile provider should not be called for non-workspace routes");
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_RootPath_BypassesClassificationAndCallsNext()
    {
        // Arrange
        var context = CreateHttpContext("/");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(It.IsAny<DataSourceMode>()), Times.Never);
        _mockProfileProvider.Verify(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_TfsConfigRoute_SetsLiveMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/tfsconfig");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_HubRoute_SetsLiveMode()
    {
        // Arrange
        var context = CreateHttpContext("/hubs/cachesync");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        _mockProfileProvider.Verify(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_LiveAllowedDiscoveryRoute_UnderWorkspacePrefix_SetsLiveMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/workitems/area-paths/from-tfs");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        _mockProfileProvider.Verify(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_TfsValidateRoute_SetsLiveMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/tfsvalidate");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        _mockProfileProvider.Verify(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_WorkItemParameterizedLiveRoute_SetsLiveMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/workitems/123/revisions");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        _mockProfileProvider.Verify(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_WorkItemStateTimelineRoute_ThrowsNotSupported()
    {
        // Arrange
        var context = CreateHttpContext("/api/workitems/123/state-timeline");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        // Assert
        try
        {
            await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);
            Assert.Fail("Expected NotSupportedException was not thrown.");
        }
        catch (NotSupportedException)
        {
            // Expected path
        }
        _mockModeProvider.Verify(p => p.SetCurrentMode(It.IsAny<DataSourceMode>()), Times.Never);
        Assert.IsFalse(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_PipelineDefinitionsDiscoveryRoute_SetsLiveMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/pipelines/definitions");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Live), Times.Once);
        _mockProfileProvider.Verify(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_WorkItemAnalyticalChildRoute_WithCache_SetsCacheMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/workitems/validation-triage");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockProfileProvider
            .Setup(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockModeProvider
            .Setup(p => p.GetModeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataSourceMode.Cache);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_ReleasePlanningRoute_WithCache_SetsCacheMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/releaseplanning/objectives");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockProfileProvider
            .Setup(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockModeProvider
            .Setup(p => p.GetModeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataSourceMode.Cache);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_MetricsRoute_WithCache_SetsCacheMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/metrics/multi-iteration-health");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockProfileProvider
            .Setup(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockModeProvider
            .Setup(p => p.GetModeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataSourceMode.Cache);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_BuildQualityRoute_WithCache_SetsCacheMode()
    {
        // Arrange
        var context = CreateHttpContext("/api/buildquality/rolling");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockProfileProvider
            .Setup(p => p.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockModeProvider
            .Setup(p => p.GetModeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataSourceMode.Cache);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);

        // Assert
        _mockModeProvider.Verify(p => p.SetCurrentMode(DataSourceMode.Cache), Times.Once);
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_UnknownRoute_ThrowsRouteNotClassifiedException()
    {
        // Arrange
        var context = CreateHttpContext("/api/unclassified-route");
        var middleware = new DataSourceModeMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        // Act / Assert
        try
        {
            await middleware.InvokeAsync(context, _mockModeProvider.Object, _mockProfileProvider.Object);
            Assert.Fail("Expected RouteNotClassifiedException was not thrown.");
        }
        catch (RouteNotClassifiedException)
        {
            // Expected path
        }

        _mockModeProvider.Verify(p => p.SetCurrentMode(It.IsAny<DataSourceMode>()), Times.Never);
        Assert.IsFalse(_nextCalled);
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

    private static async Task<string> ReadResponseBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
