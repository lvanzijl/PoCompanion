using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Configuration;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Unit tests for DataSourceModeProvider.
/// Verifies the logic that determines whether to use Cache or Live mode based on sync state.
/// </summary>
[TestClass]
public class DataSourceModeProviderTests
{
    private PoToolDbContext _context = null!;
    private DataSourceModeProvider _provider = null!;
    private Mock<ILogger<DataSourceModeProvider>> _mockLogger = null!;

    [TestInitialize]
    public void Initialize()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new PoToolDbContext(options);
        PersistenceTestGraph.EnsureProfile(_context, 1, "PO 1");
        
        _mockLogger = new Mock<ILogger<DataSourceModeProvider>>();
        _provider = new DataSourceModeProvider(_context, _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [TestMethod]
    public async Task GetModeAsync_NoCacheState_ReturnsLiveMode()
    {
        // Arrange
        int productOwnerId = 1;

        // Act
        var mode = await _provider.GetModeAsync(productOwnerId);

        // Assert
        Assert.AreEqual(DataSourceMode.Live, mode);
    }

    [TestMethod]
    public async Task GetModeAsync_NoSuccessfulSync_ReturnsLiveMode()
    {
        // Arrange
        int productOwnerId = 1;
        _context.ProductOwnerCacheStates.Add(new ProductOwnerCacheStateEntity
        {
            ProductOwnerId = productOwnerId,
            SyncStatus = CacheSyncStatus.Idle,
            LastSuccessfulSync = null
        });
        await _context.SaveChangesAsync();

        // Act
        var mode = await _provider.GetModeAsync(productOwnerId);

        // Assert
        Assert.AreEqual(DataSourceMode.Live, mode);
    }

    [TestMethod]
    public async Task GetModeAsync_SuccessfulSyncExists_ReturnsCacheMode()
    {
        // Arrange
        int productOwnerId = 1;
        _context.ProductOwnerCacheStates.Add(new ProductOwnerCacheStateEntity
        {
            ProductOwnerId = productOwnerId,
            SyncStatus = CacheSyncStatus.Success,
            LastSuccessfulSync = DateTimeOffset.UtcNow.AddHours(-1),
            WorkItemCount = 100
        });
        await _context.SaveChangesAsync();

        // Act
        var mode = await _provider.GetModeAsync(productOwnerId);

        // Assert
        Assert.AreEqual(DataSourceMode.Cache, mode);
    }

    [TestMethod]
    public async Task GetModeAsync_SyncInProgress_WithPreviousSuccessfulSync_ReturnsCacheMode()
    {
        // Arrange - This is the key scenario fixed by this change
        // When a new sync is in progress but we have a previous successful sync,
        // we should use the cached data from the previous sync
        int productOwnerId = 1;
        _context.ProductOwnerCacheStates.Add(new ProductOwnerCacheStateEntity
        {
            ProductOwnerId = productOwnerId,
            SyncStatus = CacheSyncStatus.InProgress,  // Currently syncing
            LastSuccessfulSync = DateTimeOffset.UtcNow.AddHours(-1),  // But had successful sync before
            WorkItemCount = 100,
            CurrentSyncStage = "Syncing Work Items"
        });
        await _context.SaveChangesAsync();

        // Act
        var mode = await _provider.GetModeAsync(productOwnerId);

        // Assert
        Assert.AreEqual(DataSourceMode.Cache, mode, 
            "Should use Cache mode when sync is in progress but a previous successful sync exists");
    }

    [TestMethod]
    public async Task GetModeAsync_SyncFailed_WithPreviousSuccessfulSync_ReturnsCacheMode()
    {
        // Arrange - If the latest sync failed but we have a previous successful sync,
        // we should still use the cached data
        int productOwnerId = 1;
        _context.ProductOwnerCacheStates.Add(new ProductOwnerCacheStateEntity
        {
            ProductOwnerId = productOwnerId,
            SyncStatus = CacheSyncStatus.Failed,  // Latest sync failed
            LastSuccessfulSync = DateTimeOffset.UtcNow.AddHours(-2),  // But had successful sync before
            WorkItemCount = 100,
            LastErrorMessage = "Network error"
        });
        await _context.SaveChangesAsync();

        // Act
        var mode = await _provider.GetModeAsync(productOwnerId);

        // Assert
        Assert.AreEqual(DataSourceMode.Cache, mode,
            "Should use Cache mode when sync failed but a previous successful sync exists");
    }

    [TestMethod]
    public async Task GetModeAsync_SyncInProgress_NoPreviousSuccessfulSync_ReturnsLiveMode()
    {
        // Arrange - First sync ever is in progress, no previous successful sync
        int productOwnerId = 1;
        _context.ProductOwnerCacheStates.Add(new ProductOwnerCacheStateEntity
        {
            ProductOwnerId = productOwnerId,
            SyncStatus = CacheSyncStatus.InProgress,
            LastSuccessfulSync = null,  // Never had a successful sync
            CurrentSyncStage = "Syncing Work Items"
        });
        await _context.SaveChangesAsync();

        // Act
        var mode = await _provider.GetModeAsync(productOwnerId);

        // Assert
        Assert.AreEqual(DataSourceMode.Live, mode,
            "Should use Live mode when first sync is in progress with no previous successful sync");
    }

    [TestMethod]
    public async Task GetModeAsync_IdleAfterSuccessfulSync_ReturnsCacheMode()
    {
        // Arrange
        int productOwnerId = 1;
        _context.ProductOwnerCacheStates.Add(new ProductOwnerCacheStateEntity
        {
            ProductOwnerId = productOwnerId,
            SyncStatus = CacheSyncStatus.Idle,
            LastSuccessfulSync = DateTimeOffset.UtcNow.AddMinutes(-30),
            WorkItemCount = 50
        });
        await _context.SaveChangesAsync();

        // Act
        var mode = await _provider.GetModeAsync(productOwnerId);

        // Assert
        Assert.AreEqual(DataSourceMode.Cache, mode,
            "Should use Cache mode when idle with a previous successful sync");
    }

    [TestMethod]
    public void Mode_WhenNotSet_Throws()
    {
        try
        {
            _ = _provider.Mode;
            Assert.Fail("Expected InvalidOperationException was not thrown.");
        }
        catch (InvalidOperationException)
        {
            // Expected path
        }
    }

    [TestMethod]
    public void SetCurrentMode_SetsReadableMode()
    {
        _provider.SetCurrentMode(DataSourceMode.Live);

        Assert.AreEqual(DataSourceMode.Live, _provider.Mode);
    }
}
