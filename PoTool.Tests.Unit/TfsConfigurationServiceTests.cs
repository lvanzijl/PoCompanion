using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Services;

namespace PoTool.Tests.Unit;

[TestClass]
public class TfsConfigurationServiceTests
{
    private PoToolDbContext _context = null!;
    private IDataProtectionProvider _dataProtectionProvider = null!;
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

        // Use ephemeral data protection provider for testing
        _dataProtectionProvider = DataProtectionProvider.Create("PoToolTests");
        
        _loggerMock = new Mock<ILogger<TfsConfigurationService>>();
        
        _service = new TfsConfigurationService(_context, _dataProtectionProvider, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    public async Task SaveConfigAsync_NewConfig_SavesEncryptedPat()
    {
        // Arrange
        const string url = "https://dev.azure.com/myorg";
        const string project = "MyProject";
        const string pat = "my-secret-pat-token";

        // Act
        await _service.SaveConfigAsync(url, project, pat);

        // Assert
        var savedEntity = await _context.TfsConfigs.FirstOrDefaultAsync();
        Assert.IsNotNull(savedEntity);
        Assert.AreEqual(url, savedEntity.Url);
        Assert.AreEqual(project, savedEntity.Project);
        Assert.IsNotNull(savedEntity.ProtectedPat);
        Assert.AreNotEqual(pat, savedEntity.ProtectedPat, "PAT should be encrypted, not stored in plain text");
        Assert.IsTrue(savedEntity.ProtectedPat.Length > pat.Length, "Encrypted PAT should be longer than original");
    }

    [TestMethod]
    public async Task SaveConfigAsync_UpdateExisting_UpdatesEncryptedPat()
    {
        // Arrange
        const string originalUrl = "https://dev.azure.com/org1";
        const string originalProject = "Project1";
        const string originalPat = "original-pat";
        await _service.SaveConfigAsync(originalUrl, originalProject, originalPat);

        const string newUrl = "https://dev.azure.com/org2";
        const string newProject = "Project2";
        const string newPat = "new-pat";

        // Act
        await _service.SaveConfigAsync(newUrl, newProject, newPat);

        // Assert
        var configs = await _context.TfsConfigs.ToListAsync();
        Assert.AreEqual(1, configs.Count, "Should only have one config");
        
        var config = configs[0];
        Assert.AreEqual(newUrl, config.Url);
        Assert.AreEqual(newProject, config.Project);
        Assert.AreNotEqual(newPat, config.ProtectedPat, "New PAT should be encrypted");
    }

    [TestMethod]
    public async Task GetConfigAsync_WhenConfigExists_ReturnsConfigWithoutPat()
    {
        // Arrange
        const string url = "https://dev.azure.com/myorg";
        const string project = "MyProject";
        const string pat = "my-secret-pat";
        await _service.SaveConfigAsync(url, project, pat);

        // Act
        var config = await _service.GetConfigAsync();

        // Assert
        Assert.IsNotNull(config);
        Assert.AreEqual(url, config.Url);
        Assert.AreEqual(project, config.Project);
        // PAT should not be exposed in the public config object
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
    public async Task UnprotectPatEntity_ValidEntity_ReturnsDecryptedPat()
    {
        // Arrange
        const string url = "https://dev.azure.com/myorg";
        const string project = "MyProject";
        const string originalPat = "my-secret-pat-123";
        await _service.SaveConfigAsync(url, project, originalPat);

        var entity = await _service.GetConfigEntityAsync();

        // Act
        var unprotectedPat = _service.UnprotectPatEntity(entity);

        // Assert
        Assert.IsNotNull(unprotectedPat);
        Assert.AreEqual(originalPat, unprotectedPat, "Decrypted PAT should match original");
    }

    [TestMethod]
    public void UnprotectPatEntity_NullEntity_ReturnsNull()
    {
        // Act
        var unprotectedPat = _service.UnprotectPatEntity(null);

        // Assert
        Assert.IsNull(unprotectedPat);
    }

    [TestMethod]
    public async Task UnprotectPatEntity_InvalidProtectedPat_ReturnsNullAndLogsWarning()
    {
        // Arrange
        await _service.SaveConfigAsync("url", "project", "pat");
        var entity = await _service.GetConfigEntityAsync();
        
        // Corrupt the protected PAT
        entity!.ProtectedPat = "corrupted-invalid-base64-string!@#$%";

        // Act
        var unprotectedPat = _service.UnprotectPatEntity(entity);

        // Assert
        Assert.IsNull(unprotectedPat, "Should return null for corrupted PAT");
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log warning when unprotect fails");
    }

    [TestMethod]
    public async Task SaveConfigAsync_EmptyPat_EncryptsEmptyString()
    {
        // Arrange
        const string url = "https://dev.azure.com/myorg";
        const string project = "MyProject";
        const string emptyPat = "";

        // Act
        await _service.SaveConfigAsync(url, project, emptyPat);

        // Assert
        var entity = await _service.GetConfigEntityAsync();
        Assert.IsNotNull(entity);
        Assert.IsNotNull(entity.ProtectedPat);
        Assert.AreNotEqual("", entity.ProtectedPat, "Even empty PAT should be encrypted");
        
        var unprotected = _service.UnprotectPatEntity(entity);
        Assert.AreEqual("", unprotected, "Decrypted empty PAT should be empty string");
    }

    [TestMethod]
    public async Task SaveConfigAsync_SpecialCharactersInPat_EncryptsAndDecryptsCorrectly()
    {
        // Arrange
        const string url = "https://dev.azure.com/myorg";
        const string project = "MyProject";
        const string patWithSpecialChars = "p@t!#$%^&*()_+-=[]{}|;':\",./<>?`~";

        // Act
        await _service.SaveConfigAsync(url, project, patWithSpecialChars);

        // Assert
        var entity = await _service.GetConfigEntityAsync();
        var unprotected = _service.UnprotectPatEntity(entity);
        Assert.AreEqual(patWithSpecialChars, unprotected, "Special characters should survive encryption/decryption");
    }

    [TestMethod]
    public async Task SaveConfigAsync_VeryLongPat_EncryptsAndDecryptsCorrectly()
    {
        // Arrange
        const string url = "https://dev.azure.com/myorg";
        const string project = "MyProject";
        var longPat = new string('x', 1000); // 1000 character PAT

        // Act
        await _service.SaveConfigAsync(url, project, longPat);

        // Assert
        var entity = await _service.GetConfigEntityAsync();
        var unprotected = _service.UnprotectPatEntity(entity);
        Assert.AreEqual(longPat, unprotected, "Long PAT should survive encryption/decryption");
    }

    [TestMethod]
    public async Task GetConfigEntityAsync_ReturnsLatestConfig_WhenMultipleExist()
    {
        // Arrange
        await _service.SaveConfigAsync("url1", "project1", "pat1");
        await Task.Delay(50); // Ensure different timestamps
        await _service.SaveConfigAsync("url2", "project2", "pat2");

        // Act
        var entity = await _service.GetConfigEntityAsync();

        // Assert
        Assert.IsNotNull(entity);
        Assert.AreEqual("url2", entity.Url, "Should return most recently updated config");
    }
}
