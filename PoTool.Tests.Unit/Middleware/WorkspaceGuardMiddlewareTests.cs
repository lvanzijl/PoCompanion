using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Middleware;
using PoTool.Core.Configuration;

namespace PoTool.Tests.Unit.Middleware;

/// <summary>
/// Unit tests for WorkspaceGuardMiddleware.
/// Verifies that the guard throws exceptions when workspace routes use Live mode (development-time enforcement).
/// </summary>
[TestClass]
public class WorkspaceGuardMiddlewareTests
{
    private Mock<IDataSourceModeProvider> _mockModeProvider = null!;
    private Mock<ILogger<WorkspaceGuardMiddleware>> _mockLogger = null!;
    private bool _nextCalled;

    [TestInitialize]
    public void Initialize()
    {
        _mockModeProvider = new Mock<IDataSourceModeProvider>();
        _mockLogger = new Mock<ILogger<WorkspaceGuardMiddleware>>();
        _nextCalled = false;
    }

    [TestMethod]
    public async Task InvokeAsync_WorkspaceRoute_LiveMode_ThrowsException()
    {
        // Arrange
        var context = CreateHttpContext("/api/workitems");
        var middleware = new WorkspaceGuardMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockModeProvider.Setup(p => p.Mode).Returns(DataSourceMode.Live);

        // Act & Assert
        try
        {
            await middleware.InvokeAsync(context, _mockModeProvider.Object);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            // Expected exception - verify it mentions workspace
            if (!ex.Message.Contains("workspace", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail("Exception should mention workspace");
            }
        }
    }

    [TestMethod]
    public async Task InvokeAsync_WorkspaceRoute_CacheMode_DoesNotThrow()
    {
        // Arrange
        var context = CreateHttpContext("/api/pullrequests");
        var middleware = new WorkspaceGuardMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockModeProvider.Setup(p => p.Mode).Returns(DataSourceMode.Cache);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        // Assert
        Assert.IsTrue(_nextCalled, "Next middleware should be called");
    }

    [TestMethod]
    public async Task InvokeAsync_SettingsRoute_LiveMode_DoesNotThrow()
    {
        // Arrange
        var context = CreateHttpContext("/api/settings");
        var middleware = new WorkspaceGuardMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockModeProvider.Setup(p => p.Mode).Returns(DataSourceMode.Live);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        // Assert
        Assert.IsTrue(_nextCalled, "Next middleware should be called for settings routes");
    }

    [TestMethod]
    public async Task InvokeAsync_TfsConfigRoute_LiveMode_DoesNotThrow()
    {
        // Arrange
        var context = CreateHttpContext("/api/tfsconfig");
        var middleware = new WorkspaceGuardMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockModeProvider.Setup(p => p.Mode).Returns(DataSourceMode.Live);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        // Assert
        Assert.IsTrue(_nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_PipelinesRoute_LiveMode_ThrowsException()
    {
        // Arrange
        var context = CreateHttpContext("/api/pipelines/123");
        var middleware = new WorkspaceGuardMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockModeProvider.Setup(p => p.Mode).Returns(DataSourceMode.Live);

        // Act & Assert
        try
        {
            await middleware.InvokeAsync(context, _mockModeProvider.Object);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public async Task InvokeAsync_ReleasePlanningRoute_LiveMode_ThrowsException()
    {
        // Arrange
        var context = CreateHttpContext("/api/releaseplanning");
        var middleware = new WorkspaceGuardMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockModeProvider.Setup(p => p.Mode).Returns(DataSourceMode.Live);

        // Act & Assert
        try
        {
            await middleware.InvokeAsync(context, _mockModeProvider.Object);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public async Task InvokeAsync_ReleasePlanningRoute_CacheMode_DoesNotThrow()
    {
        // Arrange
        var context = CreateHttpContext("/api/releaseplanning/objectives");
        var middleware = new WorkspaceGuardMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            logger: _mockLogger.Object);

        _mockModeProvider.Setup(p => p.Mode).Returns(DataSourceMode.Cache);

        // Act
        await middleware.InvokeAsync(context, _mockModeProvider.Object);

        // Assert
        Assert.IsTrue(_nextCalled);
    }

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }
}
