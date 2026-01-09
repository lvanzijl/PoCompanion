using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Services;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit;

/// <summary>
/// Unit tests for TfsConfigurationService using actual SQLite provider to verify DateTimeOffset ordering fix.
/// Uses in-memory SQLite database (instead of disk-based) for faster test execution without I/O overhead.
/// Note: PAT is no longer stored server-side - these tests verify non-sensitive config storage only.
/// </summary>
[TestClass]
public class TfsConfigurationServiceSqliteTests
{
    private PoToolDbContext _context = null!;
    private TfsConfigurationService _service = null!;
    private Mock<ILogger<TfsConfigurationService>> _loggerMock = null!;

    [TestInitialize]
    public void Setup()
    {
        // Use in-memory SQLite database for faster test execution without disk I/O.
        // The :memory: connection string creates a temporary database that exists
        // only for the lifetime of the connection, eliminating file cleanup needs.
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _context = new PoToolDbContext(options);
        // Important: Keep connection open for in-memory database to persist
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        
        _loggerMock = new Mock<ILogger<TfsConfigurationService>>();
        
        // Note: TfsConfigurationService no longer requires IDataProtectionProvider
        _service = new TfsConfigurationService(_context, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Close connection to dispose of in-memory database
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [TestMethod]
    public async Task GetConfigAsync_WithSqlite_OrdersByUpdatedAtCorrectly()
    {
        // Arrange - Create multiple configs with different timestamps
        // Note: PAT parameter removed from SaveConfigAsync
        await _service.SaveConfigAsync("url1", "project1", "TestProject\\Team");
        // Small delay to ensure different timestamps. DateTimeOffset.UtcNow typically has
        // ~10-15ms resolution on Windows, sub-ms on Linux. 10ms is sufficient.
        await Task.Delay(10);
        await _service.SaveConfigAsync("url2", "project2", "TestProject\\Team");
        await Task.Delay(10);
        await _service.SaveConfigAsync("url3", "project3", "TestProject\\Team");

        // Act - This would fail with the old code (OrderBy DateTimeOffset in SQL)
        var config = await _service.GetConfigAsync();

        // Assert - Should get the most recent config
        Assert.IsNotNull(config);
        Assert.AreEqual("url3", config.Url, "Should return most recently updated config");
        Assert.AreEqual("project3", config.Project);
    }

    [TestMethod]
    public async Task GetConfigEntityAsync_WithSqlite_OrdersByUpdatedAtCorrectly()
    {
        // Arrange - Create multiple configs
        await _service.SaveConfigAsync("url1", "project1", "TestProject\\Team");
        // Small delay to ensure different timestamps. DateTimeOffset.UtcNow typically has
        // ~10-15ms resolution on Windows, sub-ms on Linux. 10ms is sufficient.
        await Task.Delay(10);
        await _service.SaveConfigAsync("url2", "project2", "TestProject\\Team");

        // Act - This would fail with the old code
        var entity = await _service.GetConfigEntityAsync();

        // Assert
        Assert.IsNotNull(entity);
        Assert.AreEqual("url2", entity.Url, "Should return most recently updated config");
    }

    [TestMethod]
    public async Task GetConfigAsync_WithSingleConfig_ReturnsConfig()
    {
        // Arrange
        await _service.SaveConfigAsync("https://dev.azure.com/test", "TestProject", "TestProject\\Team");

        // Act
        var config = await _service.GetConfigAsync();

        // Assert
        Assert.IsNotNull(config);
        Assert.AreEqual("https://dev.azure.com/test", config.Url);
        Assert.AreEqual("TestProject", config.Project);
    }

    [TestMethod]
    public async Task GetConfigEntityAsync_WithNoConfigs_ReturnsNull()
    {
        // Act
        var entity = await _service.GetConfigEntityAsync();

        // Assert
        Assert.IsNull(entity);
    }
}
