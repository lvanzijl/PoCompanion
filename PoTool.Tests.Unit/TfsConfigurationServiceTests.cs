using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;

namespace PoTool.Tests.Unit;

/// <summary>
/// Tests for TfsConfigurationService.
/// Note: PAT is no longer stored server-side, so PAT encryption tests have been removed.
/// See docs/PAT_STORAGE_BEST_PRACTICES.md for details on the new client-side PAT storage approach.
/// </summary>
[TestClass]
public class TfsConfigurationServiceTests
{
    private PoToolDbContext _context = null!;
    private TfsConfigurationService _service = null!;
    private Mock<ILogger<TfsConfigurationService>> _loggerMock = null!;

    [TestInitialize]
    public void Setup()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new PoToolDbContext(options);
        
        _loggerMock = new Mock<ILogger<TfsConfigurationService>>();
        
        // Note: TfsConfigurationService no longer requires IDataProtectionProvider
        // PAT is stored client-side using MAUI SecureStorage
        _service = new TfsConfigurationService(_context, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    public async Task SaveConfigAsync_NewConfig_SavesUrlAndProject()
    {
        // Arrange
        const string url = "https://dev.azure.com/myorg";
        const string project = "MyProject";

        // Act - Note: PAT parameter removed from SaveConfigAsync
        await _service.SaveConfigAsync(url, project, "TestProject\\Team");

        // Assert
        var savedEntity = await _context.TfsConfigs.FirstOrDefaultAsync();
        Assert.IsNotNull(savedEntity);
        Assert.AreEqual(url, savedEntity.Url);
        Assert.AreEqual(project, savedEntity.Project);
        // PAT is no longer stored in the entity
    }

    [TestMethod]
    public async Task SaveConfigAsync_UpdateExisting_UpdatesUrlAndProject()
    {
        // Arrange
        const string originalUrl = "https://dev.azure.com/org1";
        const string originalProject = "Project1";
        await _service.SaveConfigAsync(originalUrl, originalProject, "TestProject\\Team");

        const string newUrl = "https://dev.azure.com/org2";
        const string newProject = "Project2";

        // Act
        await _service.SaveConfigAsync(newUrl, newProject, "TestProject\\Team");

        // Assert
        var configs = await _context.TfsConfigs.ToListAsync();
        Assert.HasCount(1, configs, "Should only have one config");
        
        var config = configs[0];
        Assert.AreEqual(newUrl, config.Url);
        Assert.AreEqual(newProject, config.Project);
    }

    [TestMethod]
    public async Task GetConfigAsync_WhenConfigExists_ReturnsConfigWithoutPat()
    {
        // Arrange
        const string url = "https://dev.azure.com/myorg";
        const string project = "MyProject";
        await _service.SaveConfigAsync(url, project, "TestProject\\Team");

        // Act
        var config = await _service.GetConfigAsync();

        // Assert
        Assert.IsNotNull(config);
        Assert.AreEqual(url, config.Url);
        Assert.AreEqual(project, config.Project);
        // PAT is never included in server-side config
    }

    [TestMethod]
    public async Task GetConfigAsync_WhenNoConfigExists_ReturnsNull()
    {
        // Act
        var config = await _service.GetConfigAsync();

        // Assert
        Assert.IsNull(config);
    }

    [TestMethod]
    public async Task SaveConfigAsync_EmptyUrlAndProject_SavesEmptyStrings()
    {
        // Arrange
        const string url = "";
        const string project = "";

        // Act
        await _service.SaveConfigAsync(url, project, "TestProject\\Team");

        // Assert
        var entity = await _service.GetConfigEntityAsync();
        Assert.IsNotNull(entity);
        Assert.AreEqual("", entity.Url);
        Assert.AreEqual("", entity.Project);
    }

    [TestMethod]
    public async Task SaveConfigAsync_SpecialCharactersInUrl_SavesCorrectly()
    {
        // Arrange
        const string url = "https://dev.azure.com/my-org_123";
        const string project = "Project-Name_123";

        // Act
        await _service.SaveConfigAsync(url, project, "TestProject\\Team");

        // Assert
        var entity = await _service.GetConfigEntityAsync();
        Assert.IsNotNull(entity);
        Assert.AreEqual(url, entity.Url);
        Assert.AreEqual(project, entity.Project);
    }

    [TestMethod]
    public async Task GetConfigEntityAsync_ReturnsLatestConfig_WhenMultipleExist()
    {
        // Arrange
        await _service.SaveConfigAsync("url1", "project1", "TestProject\\Team");
        // Small delay to ensure different timestamps. DateTimeOffset.UtcNow typically has
        // ~10-15ms resolution on Windows, sub-ms on Linux. 10ms is sufficient.
        await Task.Delay(10);
        await _service.SaveConfigAsync("url2", "project2", "TestProject\\Team");

        // Act
        var entity = await _service.GetConfigEntityAsync();

        // Assert
        Assert.IsNotNull(entity);
        Assert.AreEqual("url2", entity.Url, "Should return most recently updated config");
    }

    [TestMethod]
    public async Task SaveConfigAsync_NullValues_SavesEmptyStrings()
    {
        // Act
        await _service.SaveConfigAsync(null!, null!, "TestProject\\Team");

        // Assert
        var entity = await _service.GetConfigEntityAsync();
        Assert.IsNotNull(entity);
        Assert.AreEqual(string.Empty, entity.Url);
        Assert.AreEqual(string.Empty, entity.Project);
    }

    [TestMethod]
    public async Task SaveConfigEntityAsync_UpdatesEntity()
    {
        // Arrange
        await _service.SaveConfigAsync("url", "project", "TestProject\\Team");
        var entity = await _service.GetConfigEntityAsync();
        Assert.IsNotNull(entity);
        
        entity.Url = "updated-url";
        entity.Project = "updated-project";

        // Act
        await _service.SaveConfigEntityAsync(entity);

        // Assert
        var updatedEntity = await _service.GetConfigEntityAsync();
        Assert.IsNotNull(updatedEntity);
        Assert.AreEqual("updated-url", updatedEntity.Url);
        Assert.AreEqual("updated-project", updatedEntity.Project);
    }

}
