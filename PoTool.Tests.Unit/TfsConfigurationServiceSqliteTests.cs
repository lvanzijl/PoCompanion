using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Services;

namespace PoTool.Tests.Unit;

/// <summary>
/// Unit tests for TfsConfigurationService using actual SQLite provider to verify DateTimeOffset ordering fix.
/// Uses SQLite database provider (instead of InMemory) to test the actual database behavior.
/// Note: PAT is no longer stored server-side - these tests verify non-sensitive config storage only.
/// </summary>
[TestClass]
public class TfsConfigurationServiceSqliteTests
{
    private PoToolDbContext _context = null!;
    private TfsConfigurationService _service = null!;
    private Mock<ILogger<TfsConfigurationService>> _loggerMock = null!;
    private string _dbPath = null!;

    [TestInitialize]
    public void Setup()
    {
        // Create SQLite database (this will test the actual SQLite provider behavior)
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _context = new PoToolDbContext(options);
        _context.Database.EnsureCreated();
        
        _loggerMock = new Mock<ILogger<TfsConfigurationService>>();
        
        // Note: TfsConfigurationService no longer requires IDataProtectionProvider
        _service = new TfsConfigurationService(_context, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [TestMethod]
    public async Task GetConfigAsync_WithSqlite_OrdersByUpdatedAtCorrectly()
    {
        // Arrange - Create multiple configs with different timestamps
        // Note: PAT parameter removed from SaveConfigAsync
        await _service.SaveConfigAsync("url1", "project1", "TestProject\\Team");
        await Task.Delay(100); // Ensure different timestamps
        await _service.SaveConfigAsync("url2", "project2", "TestProject\\Team");
        await Task.Delay(100);
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
        await Task.Delay(100);
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
