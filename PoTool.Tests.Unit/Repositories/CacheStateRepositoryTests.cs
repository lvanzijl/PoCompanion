using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Repositories;

namespace PoTool.Tests.Unit.Repositories;

/// <summary>
/// Tests for CacheStateRepository to ensure proper validation of ProductOwner existence.
/// </summary>
[TestClass]
public class CacheStateRepositoryTests
{
    private PoToolDbContext _context = null!;
    private CacheStateRepository _repository = null!;
    private ProfileRepository _profileRepository = null!;

    [TestInitialize]
    public void Setup()
    {
        // Use in-memory SQLite database for real query translation testing
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite("Data Source=:memory:", sqliteOptions =>
            {
                sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .Options;
        _context = new PoToolDbContext(options);
        // Important: Keep connection open for in-memory database to persist
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new CacheStateRepository(_context);
        _profileRepository = new ProfileRepository(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Close connection to dispose of in-memory database
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    /// <summary>
    /// Verifies that GetOrCreateCacheStateAsync throws InvalidOperationException
    /// when ProductOwner does not exist.
    /// </summary>
    [TestMethod]
    public async Task GetOrCreateCacheStateAsync_ThrowsException_WhenProductOwnerDoesNotExist()
    {
        // Arrange
        const int nonExistentProductOwnerId = 999;

        // Act & Assert
        try
        {
            await _repository.GetOrCreateCacheStateAsync(nonExistentProductOwnerId);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "ProductOwner does not exist");
            StringAssert.Contains(ex.Message, nonExistentProductOwnerId.ToString());
        }
    }

    /// <summary>
    /// Verifies that GetOrCreateCacheStateAsync creates cache state successfully
    /// when ProductOwner exists.
    /// </summary>
    [TestMethod]
    public async Task GetOrCreateCacheStateAsync_CreatesSuccessfully_WhenProductOwnerExists()
    {
        // Arrange
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());

        // Act
        var cacheState = await _repository.GetOrCreateCacheStateAsync(profile.Id);

        // Assert
        Assert.IsNotNull(cacheState);
        Assert.AreEqual(profile.Id, cacheState.ProductOwnerId);
        Assert.AreEqual(Shared.Settings.CacheSyncStatusDto.Idle, cacheState.SyncStatus);
    }

    /// <summary>
    /// Verifies that GetOrCreateCacheStateAsync returns existing cache state
    /// when called multiple times.
    /// </summary>
    [TestMethod]
    public async Task GetOrCreateCacheStateAsync_ReturnsExisting_WhenCalledMultipleTimes()
    {
        // Arrange
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());
        var firstCall = await _repository.GetOrCreateCacheStateAsync(profile.Id);

        // Act
        var secondCall = await _repository.GetOrCreateCacheStateAsync(profile.Id);

        // Assert
        Assert.AreEqual(firstCall.ProductOwnerId, secondCall.ProductOwnerId);
        Assert.AreEqual(firstCall.SyncStatus, secondCall.SyncStatus);
    }

    /// <summary>
    /// Verifies that UpdateSyncStatusAsync throws InvalidOperationException
    /// when ProductOwner does not exist.
    /// </summary>
    [TestMethod]
    public async Task UpdateSyncStatusAsync_ThrowsException_WhenProductOwnerDoesNotExist()
    {
        // Arrange
        const int nonExistentProductOwnerId = 999;

        // Act & Assert
        try
        {
            await _repository.UpdateSyncStatusAsync(
                nonExistentProductOwnerId,
                Shared.Settings.CacheSyncStatusDto.InProgress,
                "Test Stage",
                50);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "ProductOwner does not exist");
        }
    }

    /// <summary>
    /// Verifies that MarkSyncSuccessAsync throws InvalidOperationException
    /// when ProductOwner does not exist.
    /// </summary>
    [TestMethod]
    public async Task MarkSyncSuccessAsync_ThrowsException_WhenProductOwnerDoesNotExist()
    {
        // Arrange
        const int nonExistentProductOwnerId = 999;

        // Act & Assert
        try
        {
            await _repository.MarkSyncSuccessAsync(
                nonExistentProductOwnerId,
                100, 50, 25,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "ProductOwner does not exist");
        }
    }

    /// <summary>
    /// Verifies that MarkSyncFailedAsync throws InvalidOperationException
    /// when ProductOwner does not exist.
    /// </summary>
    [TestMethod]
    public async Task MarkSyncFailedAsync_ThrowsException_WhenProductOwnerDoesNotExist()
    {
        // Arrange
        const int nonExistentProductOwnerId = 999;

        // Act & Assert
        try
        {
            await _repository.MarkSyncFailedAsync(
                nonExistentProductOwnerId,
                "Test error message",
                "Test stage");
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "ProductOwner does not exist");
        }
    }

    /// <summary>
    /// Verifies that ResetCacheStateAsync throws InvalidOperationException
    /// when ProductOwner does not exist.
    /// </summary>
    [TestMethod]
    public async Task ResetCacheStateAsync_ThrowsException_WhenProductOwnerDoesNotExist()
    {
        // Arrange
        const int nonExistentProductOwnerId = 999;

        // Act & Assert
        try
        {
            await _repository.ResetCacheStateAsync(nonExistentProductOwnerId);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "ProductOwner does not exist");
        }
    }

    /// <summary>
    /// Verifies that GetWatermarksAsync returns null values
    /// when ProductOwner does not have cache state.
    /// </summary>
    [TestMethod]
    public async Task GetWatermarksAsync_ReturnsNullValues_WhenCacheStateDoesNotExist()
    {
        // Arrange
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());

        // Act
        var watermarks = await _repository.GetWatermarksAsync(profile.Id);

        // Assert
        Assert.IsNull(watermarks.WorkItem);
        Assert.IsNull(watermarks.PullRequest);
        Assert.IsNull(watermarks.Pipeline);
    }
}
